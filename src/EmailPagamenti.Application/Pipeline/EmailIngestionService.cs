using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using EmailPagamenti.Application.Abstractions;
using EmailPagamenti.Application.Models;
using EmailPagamenti.Application.Options;
using EmailPagamenti.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EmailPagamenti.Application.Pipeline;

/// <summary>Numeri di una singola passata di acquisizione.</summary>
public sealed record IngestionResult(int Fetched, int Stored, int Duplicates, int Ignored, TimeSpan Elapsed);

/// <summary>
/// Orchestratore: legge dalla casella, deduplica, ricostruisce i movimenti e li salva.
/// Lavora a lotti per tenere basso l'uso di memoria anche su caselle molto grandi.
/// </summary>
public sealed class EmailIngestionService
{
    private readonly IEmailSource _source;
    private readonly ITransactionExtractor _extractor;
    private readonly ITransactionRepository _repository;
    private readonly IOptionsMonitor<IngestionOptions> _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EmailIngestionService> _logger;

    public EmailIngestionService(
        IEmailSource source,
        ITransactionExtractor extractor,
        ITransactionRepository repository,
        IOptionsMonitor<IngestionOptions> options,
        TimeProvider timeProvider,
        ILogger<EmailIngestionService> logger)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(extractor);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _source = source;
        _extractor = extractor;
        _repository = repository;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<IngestionResult> RunAsync(CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        var started = Stopwatch.GetTimestamp();
        var since = await ResolveStartingPointAsync(options, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Acquisizione da {Source} a partire da {Since:u}.", _source.Name, since);

        var fetched = 0;
        var stored = 0;
        var duplicates = 0;
        var ignored = 0;

        List<RawEmail> batch = new(options.BatchSize);

        await foreach (var email in _source.FetchAsync(since, cancellationToken).ConfigureAwait(false))
        {
            fetched++;
            batch.Add(email);

            if (batch.Count < options.BatchSize)
            {
                continue;
            }

            var counts = await ProcessBatchAsync(batch, options, cancellationToken).ConfigureAwait(false);
            stored += counts.Stored;
            duplicates += counts.Duplicates;
            ignored += counts.Ignored;
            batch.Clear();
        }

        if (batch.Count > 0)
        {
            var counts = await ProcessBatchAsync(batch, options, cancellationToken).ConfigureAwait(false);
            stored += counts.Stored;
            duplicates += counts.Duplicates;
            ignored += counts.Ignored;
        }

        var elapsed = Stopwatch.GetElapsedTime(started);
        _logger.LogInformation(
            "Acquisizione completata: {Fetched} lette, {Stored} salvate, {Duplicates} duplicate, {Ignored} scartate in {Elapsed}.",
            fetched, stored, duplicates, ignored, elapsed);

        return new IngestionResult(fetched, stored, duplicates, ignored, elapsed);
    }

    private async Task<(int Stored, int Duplicates, int Ignored)> ProcessBatchAsync(
        List<RawEmail> batch,
        IngestionOptions options,
        CancellationToken cancellationToken)
    {
        var hashes = batch.Select(e => Hash(e.MessageId)).ToArray();
        var existing = await _repository.GetExistingHashesAsync(hashes, cancellationToken).ConfigureAwait(false);

        var stored = 0;
        var duplicates = 0;
        var ignored = 0;
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        for (var i = 0; i < batch.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var raw = batch[i];
            var hash = hashes[i];

            if (existing.Contains(hash))
            {
                duplicates++;
                continue;
            }

            var extraction = _extractor.Extract(raw);

            if (extraction.Confidence < options.ReviewThreshold && !options.StoreIgnored)
            {
                ignored++;
                continue;
            }

            var entity = Transaction.Create(
                raw.MessageId,
                hash,
                raw.Folder,
                raw.Sender,
                raw.SenderDisplayName,
                raw.Subject,
                raw.SentAt,
                now,
                Snippet(raw.TextBody, options.BodySnippetLength));

            foreach (var attachment in raw.Attachments)
            {
                entity.AddAttachment(TransactionAttachment.Create(attachment.FileName, attachment.ContentType));
            }

            if (extraction.Confidence < options.ReviewThreshold)
            {
                entity.MarkIgnored(extraction.MatchedRule);
                ignored++;
            }
            else
            {
                entity.MarkClassified(
                    extraction.Direction,
                    extraction.Kind,
                    extraction.Total,
                    extraction.Merchant,
                    extraction.Category,
                    extraction.CardLast4,
                    extraction.BalanceAfter,
                    extraction.ValueDate,
                    extraction.Reference,
                    extraction.Confidence,
                    extraction.MatchedRule,
                    needsReview: extraction.Confidence < options.MinConfidence || extraction.Total is null);
                stored++;
            }

            await _repository.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        }

        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return (stored, duplicates, ignored);
    }

    private async Task<DateTime> ResolveStartingPointAsync(
        IngestionOptions options,
        CancellationToken cancellationToken)
    {
        var last = await _repository.GetLastSentAtAsync(cancellationToken).ConfigureAwait(false);
        return last is { } value
            ? value - options.Overlap
            : _timeProvider.GetUtcNow().UtcDateTime - options.InitialLookback;
    }

    private static string? Snippet(string? body, int maxLength)
    {
        if (maxLength <= 0 || string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        var collapsed = body.AsSpan().Trim();
        return collapsed.Length <= maxLength ? collapsed.ToString() : collapsed[..maxLength].ToString();
    }

    private static string Hash(string messageId)
    {
        Span<byte> destination = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(messageId), destination);
        return Convert.ToHexStringLower(destination);
    }
}
