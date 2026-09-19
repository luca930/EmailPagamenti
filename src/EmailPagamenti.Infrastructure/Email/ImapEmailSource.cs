using System.Runtime.CompilerServices;
using EmailPagamenti.Application.Abstractions;
using EmailPagamenti.Application.Models;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace EmailPagamenti.Infrastructure.Email;

/// <summary>
/// Sorgente IMAP basata su MailKit. Apre la cartella in sola lettura: il programma
/// non puo' modificare ne' cancellare nulla nella casella, per costruzione.
/// </summary>
public sealed class ImapEmailSource : IEmailSource
{
    private readonly IOptionsMonitor<ImapOptions> _options;
    private readonly ILogger<ImapEmailSource> _logger;

    public ImapEmailSource(IOptionsMonitor<ImapOptions> options, ILogger<ImapEmailSource> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options;
        _logger = logger;
    }

    public string Name => "IMAP";

    public async IAsyncEnumerable<RawEmail> FetchAsync(
        DateTimeOffset since,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;

        using var client = new ImapClient
        {
            Timeout = (int)options.Timeout.TotalMilliseconds,
        };

        var security = options.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
        await client.ConnectAsync(options.Host, options.Port, security, cancellationToken).ConfigureAwait(false);

        try
        {
            await client.AuthenticateAsync(options.UserName, options.Password, cancellationToken).ConfigureAwait(false);

            IReadOnlyList<string> folders = options.Folders.Count > 0 ? options.Folders : ["INBOX"];
            var remaining = options.MaxMessagesPerRun;

            foreach (var folderName in folders)
            {
                if (remaining <= 0)
                {
                    _logger.LogWarning(
                        "Raggiunto il tetto di {Max} email per passata: le restanti saranno lette alla prossima.",
                        options.MaxMessagesPerRun);
                    break;
                }

                var folder = await OpenFolderAsync(client, folderName, cancellationToken).ConfigureAwait(false);
                if (folder is null)
                {
                    continue;
                }

                var uids = await folder
                    .SearchAsync(SearchQuery.DeliveredAfter(since.UtcDateTime.Date), cancellationToken)
                    .ConfigureAwait(false);

                _logger.LogInformation("Cartella {Folder}: {Count} email da valutare.", folderName, uids.Count);

                foreach (var uid in uids)
                {
                    if (remaining-- <= 0)
                    {
                        break;
                    }

                    var message = await folder.GetMessageAsync(uid, cancellationToken).ConfigureAwait(false);
                    yield return Map(message, folderName);
                }

                await folder.CloseAsync(expunge: false, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(quit: true, CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    private async Task<IMailFolder?> OpenFolderAsync(
        ImapClient client,
        string folderName,
        CancellationToken cancellationToken)
    {
        try
        {
            var folder = folderName.Equals("INBOX", StringComparison.OrdinalIgnoreCase)
                ? client.Inbox
                : await client.GetFolderAsync(folderName, cancellationToken).ConfigureAwait(false);

            await folder.OpenAsync(FolderAccess.ReadOnly, cancellationToken).ConfigureAwait(false);
            return folder;
        }
        catch (FolderNotFoundException)
        {
            _logger.LogWarning("Cartella {Folder} inesistente sul server: saltata.", folderName);
            return null;
        }
    }

    private static RawEmail Map(MimeMessage message, string folderName)
    {
        var from = message.From.Mailboxes.FirstOrDefault();

        var attachments = message.Attachments
            .OfType<MimePart>()
            .Select(part => new RawAttachment(
                part.FileName ?? "(senza nome)",
                part.ContentType.MimeType))
            .ToList();

        return new RawEmail(
            // Senza Message-Id la deduplica perderebbe l'aggancio: se ne ricostruisce uno stabile.
            string.IsNullOrWhiteSpace(message.MessageId)
                ? $"{folderName}:{message.Date:O}:{message.Subject}"
                : message.MessageId,
            folderName,
            from?.Address ?? "(sconosciuto)",
            from?.Name,
            message.Subject ?? string.Empty,
            message.Date,
            message.TextBody,
            attachments);
    }
}
