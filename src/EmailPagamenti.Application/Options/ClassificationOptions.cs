using EmailPagamenti.Domain.Enums;

namespace EmailPagamenti.Application.Options;

/// <summary>
/// Regole di riconoscimento. Stanno in configurazione, non nel codice: aggiungere una banca
/// o un negozio non deve richiedere una ricompilazione.
/// </summary>
public sealed class ClassificationOptions
{
    public const string SectionName = "Classification";

    /// <summary>Mittenti attendibili: dominio o indirizzo completo. Alzano molto la confidenza.</summary>
    public List<string> TrustedSenders { get; set; } = [];

    /// <summary>Parole che indicano un pagamento nell'oggetto o nel corpo.</summary>
    public List<string> PaymentKeywords { get; set; } =
    [
        "pagamento", "pagato", "ricevuta", "fattura", "addebito", "transazione",
        "ordine confermato", "conferma ordine", "importo", "scontrino", "bonifico",
        "payment", "receipt", "invoice", "charged", "your order", "transaction",
    ];

    /// <summary>Parole che indicano un rimborso o un accredito.</summary>
    public List<string> IncomingKeywords { get; set; } =
    [
        "rimborso", "rimborsato", "accredito", "accreditato", "storno",
        "refund", "refunded", "credited", "payout",
    ];

    /// <summary>Parole che escludono l'email a prescindere: marketing, newsletter, spam.</summary>
    public List<string> ExcludeKeywords { get; set; } =
    [
        "newsletter", "promozione", "offerta esclusiva", "sconto del",
        "unsubscribe", "disiscriviti", "black friday", "saldi",
    ];

    /// <summary>Regole specifiche per esercente, valutate prima di quelle generiche.</summary>
    public List<MerchantRule> Merchants { get; set; } = [];
}

/// <summary>Regola che riconosce un esercente preciso e ne fissa la classificazione.</summary>
public sealed class MerchantRule
{
    /// <summary>Nome normalizzato salvato a database.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Sottostringa cercata nel mittente, tipicamente il dominio.</summary>
    public string? SenderContains { get; set; }

    /// <summary>Sottostringa cercata nell'oggetto.</summary>
    public string? SubjectContains { get; set; }

    /// <summary>Tipo di documento da assegnare quando la regola scatta.</summary>
    public DocumentKind Kind { get; set; } = DocumentKind.Receipt;

    /// <summary>Verso da assegnare quando la regola scatta.</summary>
    public PaymentDirection Direction { get; set; } = PaymentDirection.Outgoing;
}
