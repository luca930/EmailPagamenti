using EmailPagamenti.Application.Options;
using EmailPagamenti.Domain.Enums;

namespace EmailPagamenti.Application.Parsing;

/// <summary>
/// Profili di partenza. ING e' modellato su una notifica reale; gli altri seguono le formule
/// piu' diffuse nelle comunicazioni bancarie italiane e vanno confermati su un esempio vero
/// prima di fidarsene. Tutto e' sovrascrivibile da configurazione.
/// </summary>
public static class BuiltInBankProfiles
{
    /// <summary>
    /// Un importo con la sua valuta, nelle forme che si incontrano davvero: "1.234,56 EUR",
    /// "EUR 1.234,56", "1.234,56 euro", "€ 1.234,56".
    /// </summary>
    private const string Amount =
        @"(?<amount>(?:EUR|EURO|USD|CHF|GBP|€|\$|£)\s*\d[\d.,  ]*\d|\d[\d.,  ]*\d\s*(?:EUR|EURO|USD|CHF|GBP|€|\$|£)|\d\s*(?:EUR|EURO|€))";

    /// <summary>Valore di un campo etichettato, fino a fine riga.</summary>
    private const string FieldValue = @"(?<value>[^\r\n]{1,80})";

    public static IReadOnlyList<BankProfile> All { get; } = [Ing(), GenericItalian()];

    public static EnrichmentPatterns DefaultEnrichment { get; } = new()
    {
        // "Importo: 1.234,56 euro" oppure "per un importo di EUR 1.234,56".
        Amount = $@"\bimporto\b[^\r\n:]{{0,20}}:?\s*{Amount}",

        // "carta **** 1234", "carta n. 1234", "bancomat ...1234".
        Card = @"\b(?:carta|bancomat|pan)\b[^\r\n]{0,24}?[*x•.\s](?<card>\d{4})\b",

        // "in data 14/03/2026", "del 14-03-26".
        Date = @"\b(?:in\s+data|data\s+(?:operazione|contabile|valuta)|del)\b\s*:?\s*(?<date>\d{1,2}[/\-.]\d{1,2}[/\-.]\d{2,4})",

        Balance = $@"\bsaldo\b[^\r\n:]{{0,24}}:?\s*(?<balance>{Amount})",

        // L'ancoraggio a inizio riga e' quello che distingue "Beneficiario:" da
        // "IBAN beneficiario:", che nelle email ING stanno una sotto l'altra.
        Merchant =
            @"(?:^|\r|\n)\W{0,4}(?:beneficiario|esercente|ordinante|mittente|destinatario|a\s+favore\s+di|presso)\b\s*:?\s*(?<merchant>[^\r\n]{2,80})",

        Reference = @"\b(?:TRN|CRO|IUR|rif(?:erimento)?|numero\s+operazione|codice\s+operazione)\b\s*:?\s*(?<reference>[A-Z0-9][A-Z0-9\-/]{4,34})",
    };

    /// <summary>
    /// ING, prodotto "Conto Corrente Arancio". Le notifiche sono a campi etichettati, quindi
    /// il riconoscimento e' sulla frase di apertura e i valori arrivano dalle etichette.
    /// </summary>
    private static BankProfile Ing() => new()
    {
        Name = "ING",
        SenderContains = ["ing.it", "ing.com"],
        BodyContains = ["Conto Corrente Arancio", "Area Riservata su app o sito ING"],
        Patterns =
        [
            new TransactionPattern
            {
                Name = "ING bonifico in uscita",
                Regex = @"bonifico\b[^\r\n]{0,60}?\bdal\s+tuo\s+conto",
                Kind = TransactionKind.Transfer,
                Direction = PaymentDirection.Outgoing,
            },
            new TransactionPattern
            {
                Name = "ING bonifico in entrata",
                Regex = @"bonifico\b[^\r\n]{0,60}?\b(?:sul\s+tuo\s+conto|in\s+entrata|a\s+tuo\s+favore|ricevuto)",
                Kind = TransactionKind.Transfer,
                Direction = PaymentDirection.Incoming,
            },
            new TransactionPattern
            {
                Name = "ING pagamento con carta",
                Regex = @"\b(?:pagamento|acquisto)\b[^\r\n]{0,60}?\b(?:carta|bancomat)\b",
                Kind = TransactionKind.CardPayment,
                Direction = PaymentDirection.Outgoing,
            },
            new TransactionPattern
            {
                Name = "ING prelievo",
                Regex = @"\bprelievo\b",
                Kind = TransactionKind.CashWithdrawal,
                Direction = PaymentDirection.Outgoing,
            },
            new TransactionPattern
            {
                Name = "ING addebito",
                Regex = @"\baddebito\b[^\r\n]{0,60}?\b(?:SDD|diretto|utenz|domiciliazion)",
                Kind = TransactionKind.DirectDebit,
                Direction = PaymentDirection.Outgoing,
            },
            new TransactionPattern
            {
                Name = "ING accredito",
                Regex = @"\b(?:accredito|accreditato|stipendio)\b",
                Kind = TransactionKind.Income,
                Direction = PaymentDirection.Incoming,
            },
        ],
    };

    /// <summary>
    /// Formule generiche italiane, per qualsiasi mittente. Servono da rete di sicurezza quando
    /// arriva una notifica che il profilo della banca non copre ancora.
    /// </summary>
    private static BankProfile GenericItalian() => new()
    {
        Name = "Generico italiano",
        Patterns =
        [
            new TransactionPattern
            {
                Name = "Pagamento con carta",
                Regex = $@"\b(?:pagamento|acquisto|transazione)\b[^\r\n]{{0,60}}?{Amount}",
                Kind = TransactionKind.CardPayment,
                Direction = PaymentDirection.Outgoing,
            },
            new TransactionPattern
            {
                Name = "Prelievo",
                Regex = $@"\b(?:prelievo|prelevamento)\b[^\r\n]{{0,60}}?{Amount}",
                Kind = TransactionKind.CashWithdrawal,
                Direction = PaymentDirection.Outgoing,
            },
            new TransactionPattern
            {
                Name = "Bonifico in entrata",
                Regex = $@"\bbonifico\b[^\r\n]{{0,60}}?\b(?:in\s+entrata|ricevuto|a\s+tuo\s+favore)\b[^\r\n]{{0,60}}?{Amount}",
                Kind = TransactionKind.Transfer,
                Direction = PaymentDirection.Incoming,
            },
            new TransactionPattern
            {
                Name = "Bonifico in uscita",
                Regex = $@"\bbonifico\b[^\r\n]{{0,60}}?{Amount}",
                Kind = TransactionKind.Transfer,
                Direction = PaymentDirection.Outgoing,
            },
            new TransactionPattern
            {
                Name = "Addebito diretto",
                Regex = $@"\baddebito\b[^\r\n]{{0,60}}?{Amount}",
                Kind = TransactionKind.DirectDebit,
                Direction = PaymentDirection.Outgoing,
            },
            new TransactionPattern
            {
                Name = "Accredito",
                Regex = $@"\b(?:accredito|accreditato|stipendio|versamento)\b[^\r\n]{{0,60}}?{Amount}",
                Kind = TransactionKind.Income,
                Direction = PaymentDirection.Incoming,
            },
            new TransactionPattern
            {
                Name = "Commissione",
                Regex = $@"\b(?:commissione|canone|imposta\s+di\s+bollo)\b[^\r\n]{{0,60}}?{Amount}",
                Kind = TransactionKind.Fee,
                Direction = PaymentDirection.Outgoing,
            },
            new TransactionPattern
            {
                Name = "Operazione rifiutata",
                Regex = @"\b(?:non\s+(?:e'\s+)?(?:andata|andato)\s+a\s+buon\s+fine|rifiutat[ao]|negat[ao]|insufficient)\b",
                Kind = TransactionKind.Declined,
                Direction = PaymentDirection.Outgoing,
            },
        ],
    };

    /// <summary>Espone il frammento degli importi, cosi' un profilo scritto a mano puo' riusarlo.</summary>
    public static string AmountFragment => Amount;

    /// <summary>Espone il frammento del valore di un campo etichettato.</summary>
    public static string FieldValueFragment => FieldValue;
}
