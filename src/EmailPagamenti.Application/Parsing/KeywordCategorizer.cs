using EmailPagamenti.Application.Abstractions;
using EmailPagamenti.Application.Options;
using EmailPagamenti.Domain.Enums;
using EmailPagamenti.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace EmailPagamenti.Application.Parsing;

/// <summary>
/// Assegna la categoria per parole chiave sull'esercente. Deterministico e correggibile:
/// quello che sbaglia si sistema dall'interfaccia, e la correzione resta.
/// </summary>
public sealed class KeywordCategorizer : ICategorizer
{
    /// <summary>
    /// Catene e servizi diffusi in Italia. Non pretende di essere completo: copre il grosso
    /// dello scontrino medio e lascia il resto a "Da categorizzare", che l'interfaccia mostra
    /// in evidenza proprio per farlo sistemare.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string[]> BuiltInRules =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Alimentari"] =
            [
                "esselunga", "coop", "conad", "carrefour", "lidl", "eurospin", "penny", "md discount",
                "pam ", "bennet", "iper", "famila", "despar", "crai", "tigros", "supermercat", "alimentari",
                "panificio", "macelleria", "fruttivendolo", "gastronomia",
            ],
            ["Ristoranti"] =
            [
                "ristorante", "trattoria", "osteria", "pizzeria", "bar ", "caffe", "caffè", "pub",
                "mcdonald", "burger king", "kfc", "roadhouse", "old wild west", "just eat", "deliveroo",
                "glovo", "starbucks", "pasticceria", "gelateria",
            ],
            ["Carburante"] =
            [
                "eni ", "agip", "q8", "tamoil", "ip ", "esso", "shell", "api ", "erg ", "distributore",
                "carburant", "benzina", "enel x way", "be charge", "ionity",
            ],
            ["Trasporti"] =
            [
                "trenitalia", "italo", "atm ", "gtt", "amt", "autostrade", "telepass", "uber", "freenow",
                "bolt", "parcheggio", "easypark", "ryanair", "easyjet", "ita airways", "aeroport",
            ],
            ["Casa e bollette"] =
            [
                "enel", "eni gas", "hera", "a2a", "iren", "acea", "sorgenia", "illumia", "edison",
                "tim ", "vodafone", "windtre", "fastweb", "iliad", "sky italia", "affitto", "condominio",
                "leroy merlin", "brico", "obi italia", "ikea",
            ],
            ["Salute"] =
            [
                "farmacia", "parafarmacia", "medic", "dentist", "ottica", "laboratorio analisi",
                "poliambulator", "ospedale", "fisioterap", "veterinar",
            ],
            ["Shopping"] =
            [
                "amazon", "zalando", "zara", "h&m", "hm.com", "decathlon", "mediaworld", "unieuro",
                "euronics", "apple store", "nike", "adidas", "ovs", "kiabi", "action", "tigota", "acqua e sapone",
            ],
            ["Abbonamenti"] =
            [
                "netflix", "spotify", "disney", "amazon prime", "dazn", "now tv", "youtube premium",
                "google one", "icloud", "microsoft 365", "adobe", "openai", "anthropic", "github",
            ],
            ["Svago"] =
            [
                "cinema", "teatro", "palestra", "piscina", "libreria", "feltrinelli", "mondadori",
                "steam", "playstation", "nintendo", "ticketone", "museo",
            ],
        };

    private readonly IOptionsMonitor<CategoryOptions> _options;

    public KeywordCategorizer(IOptionsMonitor<CategoryOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public string Categorize(string? merchant, string text, TransactionKind kind)
    {
        // Alcuni movimenti hanno una categoria per natura, qualunque sia la controparte.
        switch (kind)
        {
            case TransactionKind.CashWithdrawal:
                return "Prelievi";
            case TransactionKind.Income:
                return "Entrate";
            case TransactionKind.Fee:
                return "Casa e bollette";
        }

        var options = _options.CurrentValue;

        // L'esercente e' il segnale buono; il testo intero e' l'ultima risorsa, perche'
        // una notifica puo' nominare parole che non c'entrano con la spesa.
        var haystack = string.IsNullOrWhiteSpace(merchant) ? text : merchant;

        foreach (var (category, keywords) in options.Rules)
        {
            if (Matches(haystack, keywords))
            {
                return category;
            }
        }

        if (options.UseBuiltInRules)
        {
            foreach (var (category, keywords) in BuiltInRules)
            {
                if (Matches(haystack, keywords))
                {
                    return category;
                }
            }
        }

        return SpendingCategory.Uncategorized;
    }

    private static bool Matches(string haystack, IEnumerable<string> keywords) =>
        keywords.Any(k => !string.IsNullOrEmpty(k) && haystack.Contains(k, StringComparison.OrdinalIgnoreCase));
}
