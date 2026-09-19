using EmailPagamenti.Domain.Enums;
using EmailPagamenti.Domain.ValueObjects;

namespace EmailPagamenti.Domain.Entities;

/// <summary>
/// Un movimento del conto, ricostruito dalla notifica che la banca manda via email.
/// E' la riga che si ritrova cercando "quanto ho speso al supermercato a marzo".
/// </summary>
public sealed class Transaction
{
    private readonly List<TransactionAttachment> _attachments = [];

    // Richiesto da EF Core per la materializzazione.
    private Transaction()
    {
        MessageId = string.Empty;
        MessageIdHash = string.Empty;
        Sender = string.Empty;
        Subject = string.Empty;
        Folder = string.Empty;
        Category = SpendingCategory.Uncategorized;
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

    /// <summary>
    /// Data di invio della notifica, sempre in UTC. Le date sono DateTime e non DateTimeOffset
    /// perche' SQLite non sa ne' ordinare ne' confrontare i DateTimeOffset, e SQLite e' il
    /// modo piu' semplice di far girare questa applicazione in casa.
    /// </summary>
    public DateTime SentAt { get; private set; }

    /// <summary>
    /// Data del movimento dichiarata dalla banca, quando la notifica la riporta.
    /// Puo' essere diversa da <see cref="SentAt"/>: le notifiche arrivano anche a giorni di distanza.
    /// </summary>
    public DateTime? ValueDate { get; private set; }

    /// <summary>Data usata per raggruppare e filtrare: quella della banca se c'e', altrimenti l'invio.</summary>
    public DateTime OccurredAt { get; private set; }

    public DateTime IngestedAt { get; private set; }

    public ProcessingStatus Status { get; private set; }

    public PaymentDirection Direction { get; private set; }

    public TransactionKind Kind { get; private set; }

    /// <summary>
    /// Importo in centesimi: e' questa la colonna vera. Vedi <see cref="Money.Cents"/> per il
    /// perche' non si salvano decimal.
    /// </summary>
    public long? AmountCents { get; private set; }

    /// <summary>Importo del movimento, sempre positivo: il verso sta in <see cref="Direction"/>.</summary>
    public decimal? Amount => AmountCents is { } cents ? cents / 100m : null;

    public string? Currency { get; private set; }

    /// <summary>Esercente o controparte: il negozio, chi ha disposto il bonifico, l'ente che addebita.</summary>
    public string? Merchant { get; private set; }

    /// <summary>Categoria di spesa, assegnata dalle regole o corretta a mano.</summary>
    public string Category { get; private set; }

    /// <summary>Ultime quattro cifre della carta, quando la notifica le riporta.</summary>
    public string? CardLast4 { get; private set; }

    /// <summary>Saldo residuo dopo il movimento, in centesimi.</summary>
    public long? BalanceAfterCents { get; private set; }

    /// <summary>Saldo residuo comunicato dalla banca dopo il movimento, quando c'e'.</summary>
    public decimal? BalanceAfter => BalanceAfterCents is { } cents ? cents / 100m : null;

    /// <summary>Numero d'ordine, CRO del bonifico o riferimento dell'operazione.</summary>
    public string? Reference { get; private set; }

    /// <summary>Quanto la pipeline si fida di quello che ha estratto, da 0 a 1.</summary>
    public double Confidence { get; private set; }

    /// <summary>Regola che ha prodotto il risultato: serve a capire perche' un movimento e' finito dov'e'.</summary>
    public string? MatchedRule { get; private set; }

    /// <summary>Vero quando una persona ha confermato o corretto i dati estratti.</summary>
    public bool ReviewedByHuman { get; private set; }

    /// <summary>
    /// Estratto del corpo, troncato secondo configurazione. Il corpo completo non viene salvato:
    /// riduce la superficie di dati bancari a riposo.
    /// </summary>
    public string? BodySnippet { get; private set; }

    public string? FailureReason { get; private set; }

    public IReadOnlyList<TransactionAttachment> Attachments => _attachments;

    /// <summary>Importo e valuta insieme, quando entrambi sono stati riconosciuti.</summary>
    public Money? Total =>
        Amount is { } amount && Currency is { } currency
            ? new Money(amount, currency)
            : null;

    /// <summary>Importo con segno: negativo in uscita, positivo in entrata. Comodo per i totali.</summary>
    public decimal SignedAmount =>
        Amount is not { } amount
            ? 0m
            : Direction == PaymentDirection.Incoming ? amount : -amount;

    public static Transaction Create(
        string messageId,
        string messageIdHash,
        string folder,
        string sender,
        string? senderDisplayName,
        string subject,
        DateTime sentAt,
        DateTime ingestedAt,
        string? bodySnippet)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageIdHash);

        return new Transaction
        {
            Id = Guid.CreateVersion7(),
            MessageId = messageId,
            MessageIdHash = messageIdHash,
            Folder = folder,
            Sender = sender,
            SenderDisplayName = senderDisplayName,
            Subject = subject,
            SentAt = sentAt,
            OccurredAt = sentAt,
            IngestedAt = ingestedAt,
            BodySnippet = bodySnippet,
            Status = ProcessingStatus.Pending,
            Category = SpendingCategory.Uncategorized,
        };
    }

    public void MarkClassified(
        PaymentDirection direction,
        TransactionKind kind,
        Money? money,
        string? merchant,
        string category,
        string? cardLast4,
        long? balanceAfter,
        DateTime? valueDate,
        string? reference,
        double confidence,
        string? matchedRule,
        bool needsReview)
    {
        Direction = direction;
        Kind = kind;
        AmountCents = money?.Cents;
        Currency = money?.Currency;
        Merchant = merchant;
        Category = string.IsNullOrWhiteSpace(category) ? SpendingCategory.Uncategorized : category;
        CardLast4 = cardLast4;
        BalanceAfterCents = balanceAfter;
        ValueDate = valueDate;
        OccurredAt = valueDate ?? SentAt;
        Reference = reference;
        Confidence = Math.Clamp(confidence, 0d, 1d);
        MatchedRule = matchedRule;
        Status = needsReview ? ProcessingStatus.NeedsReview : ProcessingStatus.Classified;
        FailureReason = null;
    }

    /// <summary>
    /// Correzione fatta da una persona nell'interfaccia. Porta il movimento a
    /// <see cref="ProcessingStatus.Classified"/>: quello che conferma un umano non va piu' rivisto.
    /// </summary>
    public void ApplyManualCorrection(
        PaymentDirection direction,
        TransactionKind kind,
        Money? money,
        string? merchant,
        string category)
    {
        Direction = direction;
        Kind = kind;

        if (money is { } value)
        {
            AmountCents = value.Cents;
            Currency = value.Currency;
        }

        Merchant = merchant;
        Category = string.IsNullOrWhiteSpace(category) ? SpendingCategory.Uncategorized : category;
        Status = ProcessingStatus.Classified;
        Confidence = 1d;
        ReviewedByHuman = true;
        MatchedRule = "manuale";
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

    public void AddAttachment(TransactionAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        _attachments.Add(attachment);
    }
}
