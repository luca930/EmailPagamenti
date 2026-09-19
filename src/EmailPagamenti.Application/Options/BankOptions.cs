using EmailPagamenti.Domain.Enums;

namespace EmailPagamenti.Application.Options;

/// <summary>
/// Come si leggono le notifiche della banca. Le espressioni stanno in configurazione, non nel
/// codice: adattare l'applicazione a una banca diversa non deve richiedere una ricompilazione.
/// </summary>
public sealed class BankOptions
{
    public const string SectionName = "Banks";

    /// <summary>
    /// Se vero, ai profili configurati si aggiungono quelli incorporati per le formule
    /// piu' diffuse in italiano. Spegnerlo quando il profilo della propria banca e' completo
    /// ed i default generici introducono rumore.
    /// </summary>
    public bool UseBuiltInProfiles { get; set; } = true;

    /// <summary>Profili specifici, valutati prima di quelli incorporati.</summary>
    public List<BankProfile> Profiles { get; set; } = [];

    /// <summary>
    /// Espressioni che estraggono i dettagli comuni a tutte le notifiche. Ogni voce lasciata
    /// vuota usa quella incorporata.
    /// </summary>
    public EnrichmentPatterns Enrichment { get; set; } = new();
}

/// <summary>Profilo di una banca: come riconoscerne le email e come leggerne i movimenti.</summary>
public sealed class BankProfile
{
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Sottostringhe cercate nel mittente. Vuoto significa che il profilo vale per qualsiasi
    /// mittente: e' il caso dei profili generici incorporati.
    /// </summary>
    public List<string> SenderContains { get; set; } = [];

    /// <summary>
    /// Sottostringhe cercate nel testo. Utile quando l'indirizzo del mittente cambia ma la
    /// notifica nomina sempre il prodotto, come "Conto Corrente Arancio" per ING.
    /// </summary>
    public List<string> BodyContains { get; set; } = [];

    /// <summary>Espressioni che riconoscono un movimento, valutate in ordine.</summary>
    public List<TransactionPattern> Patterns { get; set; } = [];
}

/// <summary>
/// Una formula che riconosce un tipo di movimento. I gruppi <c>amount</c> e <c>merchant</c>
/// sono entrambi facoltativi: quello che la formula non cattura viene cercato dalle
/// espressioni di arricchimento comuni (vedi <see cref="EnrichmentPatterns"/>).
/// </summary>
public sealed class TransactionPattern
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Espressione regolare, valutata senza distinzione tra maiuscole e minuscole.</summary>
    public string Regex { get; set; } = string.Empty;

    public TransactionKind Kind { get; set; } = TransactionKind.CardPayment;

    public PaymentDirection Direction { get; set; } = PaymentDirection.Outgoing;
}

/// <summary>
/// Espressioni per i dettagli comuni. Girano dopo il riconoscimento del movimento e riempiono
/// solo i campi che la formula principale non ha gia' catturato.
/// </summary>
public sealed class EnrichmentPatterns
{
    public string? Amount { get; set; }

    public string? Card { get; set; }

    public string? Date { get; set; }

    public string? Balance { get; set; }

    public string? Merchant { get; set; }

    public string? Reference { get; set; }
}
