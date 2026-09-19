using EmailPagamenti.Domain.Enums;
using EmailPagamenti.Domain.ValueObjects;

namespace EmailPagamenti.Domain.Entities;

/// <summary>
/// Una email riconosciuta (o scartata) dalla pipeline di smistamento.
/// E' la riga che l'utente ritrovera' cercando "quanto ho pagato ad Amazon a marzo".
/// </summary>
public sealed class PaymentEmail
{
    private readonly List<PaymentAttachment> _attachments = [];

    // Richiesto da EF Core per la materializzazione.
    private PaymentEmail()
    {
        MessageId = string.Empty;
        MessageIdHash = string.Empty;
        Sender = string.Empty;
        Subject = string.Empty;
        Folder = string.Empty;
    }

    public Guid Id { get; private set; }

    /// <summary>Message-Id RFC 5322, come arrivato dal server.</summary>
    public string MessageId { get; private set; }

    /// <summary>
    /// SHA-256 del Message-Id: e' questo il campo indicizzato e unico, cosi' la deduplica
    /// resta a lunghezza fissa anche con Message-Id lunghissimi.
    /// </summary>
    public string MessageIdHash { get; private set; }

    public string Folder { get; private set; }

    public string Sender { get; private set; }

    public string? SenderDisplayName { get; private set; }

    public string Subject { get; private set; }

    public DateTimeOffset SentAt { get; private set; }

    public DateTimeOffset IngestedAt { get; private set; }

    public ProcessingStatus Status { get; private set; }

    public PaymentDirection Direction { get; private set; }

    public DocumentKind Kind { get; private set; }

    /// <summary>Importo riconosciuto, se c'e'.</summary>
    public decimal? Amount { get; private set; }

    /// <summary>Valuta ISO 4217 dell'importo, se c'e'.</summary>
    public string? Currency { get; private set; }

    /// <summary>Esercente o controparte, normalizzato dalle regole di classificazione.</summary>
    public string? Merchant { get; private set; }

    /// <summary>Numero d'ordine, di fattura o riferimento della transazione.</summary>
    public string? Reference { get; private set; }

    /// <summary>Quanto la pipeline si fida di quello che ha estratto, da 0 a 1.</summary>
    public double Confidence { get; private set; }

    /// <summary>Regola che ha prodotto la classificazione: serve a capire perche' una email e' finita dov'e'.</summary>
    public string? MatchedRule { get; private set; }

    /// <summary>
    /// Estratto del corpo, troncato secondo configurazione. Il corpo completo non viene
    /// salvato: riduce la superficie di dati personali a riposo.
    /// </summary>
    public string? BodySnippet { get; private set; }

    public string? FailureReason { get; private set; }

    public IReadOnlyList<PaymentAttachment> Attachments => _attachments;

    /// <summary>Importo e valuta insieme, quando entrambi sono stati riconosciuti.</summary>
    public Money? Total =>
        Amount is { } amount && Currency is { } currency
            ? new Money(amount, currency)
            : null;

    public static PaymentEmail Create(
        string messageId,
        string messageIdHash,
        string folder,
        string sender,
        string? senderDisplayName,
        string subject,
        DateTimeOffset sentAt,
        DateTimeOffset ingestedAt,
        string? bodySnippet)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageIdHash);

        return new PaymentEmail
        {
            Id = Guid.CreateVersion7(),
            MessageId = messageId,
            MessageIdHash = messageIdHash,
            Folder = folder,
            Sender = sender,
            SenderDisplayName = senderDisplayName,
            Subject = subject,
            SentAt = sentAt,
            IngestedAt = ingestedAt,
            BodySnippet = bodySnippet,
            Status = ProcessingStatus.Pending,
        };
    }

    public void MarkClassified(
        PaymentDirection direction,
        DocumentKind kind,
        Money? money,
        string? merchant,
        string? reference,
        double confidence,
        string? matchedRule,
        bool needsReview)
    {
        Direction = direction;
        Kind = kind;
        Amount = money?.Amount;
        Currency = money?.Currency;
        Merchant = merchant;
        Reference = reference;
        Confidence = Math.Clamp(confidence, 0d, 1d);
        MatchedRule = matchedRule;
        Status = needsReview ? ProcessingStatus.NeedsReview : ProcessingStatus.Classified;
        FailureReason = null;
    }

    public void MarkIgnored(string? reason)
    {
        Status = ProcessingStatus.Ignored;
        Confidence = 0d;
        MatchedRule = reason;
    }

    public void MarkFailed(string reason)
    {
        Status = ProcessingStatus.Failed;
        FailureReason = reason;
    }

    public void AddAttachment(PaymentAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        _attachments.Add(attachment);
    }
}
