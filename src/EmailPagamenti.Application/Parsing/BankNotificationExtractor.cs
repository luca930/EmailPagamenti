using System.Globalization;
using System.Text.RegularExpressions;
using EmailPagamenti.Application.Abstractions;
using EmailPagamenti.Application.Models;
using EmailPagamenti.Application.Options;
using EmailPagamenti.Domain.Enums;
using EmailPagamenti.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace EmailPagamenti.Application.Parsing;

/// <summary>
/// Legge le notifiche di movimento della banca. Prima sceglie il profilo (dal mittente o da una
/// frase riconoscibile nel testo), poi cerca la formula che dice di che movimento si tratta,
/// infine riempie i dettagli dai campi etichettati.
/// </summary>
public sealed class BankNotificationExtractor : ITransactionExtractor
{
    private static readonly string[] DateFormats =
        ["dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "dd.MM.yyyy", "dd/MM/yy", "d/M/yy", "dd-MM-yy"];

    private readonly IOptionsMonitor<BankOptions> _options;
    private readonly ICategorizer _categorizer;

    public BankNotificationExtractor(IOptionsMonitor<BankOptions> options, ICategorizer categorizer)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(categorizer);

        _options = options;
        _categorizer = categorizer;
    }

    public TransactionExtraction Extract(RawEmail email)
    {
        ArgumentNullException.ThrowIfNull(email);

        var options = _options.CurrentValue;
        var text = email.SearchableText;

        var (profile, matchedExplicitly) = SelectProfile(email, text, options);
        if (profile is null)
        {
            return TransactionExtraction.NotATransaction("nessun profilo bancario");
        }

        var (pattern, match) = MatchPattern(profile, text);
        if (pattern is null || match is null)
        {
            return TransactionExtraction.NotATransaction($"profilo {profile.Name}: nessuna formula corrispondente");
        }

        // Ogni voce non configurata ricade su quella incorporata: senza questa fusione un
        // profilo che personalizza un solo campo perderebbe tutti gli altri.
        var enrichment = Merge(options.Enrichment);

        var total = ReadAmount(match, text, enrichment);
        var merchant = CleanMerchant(ReadGroup(match, "merchant") ?? ReadEnriched(text, enrichment.Merchant, "merchant"));
        var card = ReadGroup(match, "card") ?? ReadEnriched(text, enrichment.Card, "card");
        var reference = ReadGroup(match, "reference") ?? ReadEnriched(text, enrichment.Reference, "reference");
        var valueDate = ReadDate(match, text, enrichment);
        var balance = ReadBalance(match, text, enrichment);

        // La formula da sola dice poco: e' l'insieme di profilo riconosciuto, movimento
        // identificato e importo letto a rendere affidabile il risultato.
        var confidence = 0.45d;
        if (matchedExplicitly)
        {
            confidence += 0.35d;
        }

        if (total is not null)
        {
            confidence += 0.20d;
        }
        else
        {
            // Sembra un movimento ma l'importo non si legge: va rivisto a mano, non scartato.
            confidence -= 0.15d;
        }

        if (!string.IsNullOrEmpty(merchant))
        {
            confidence += 0.05d;
        }

        var direction = pattern.Direction;
        var category = _categorizer.Categorize(merchant, text, pattern.Kind);

        return new TransactionExtraction(
            Math.Clamp(confidence, 0d, 1d),
            direction,
            pattern.Kind,
            total,
            merchant,
            category,
            card,
            balance,
            valueDate,
            reference,
            $"{profile.Name}: {pattern.Name}");
    }

    /// <summary>
    /// Sceglie il profilo. Un profilo che nomina il mittente o una frase del testo vince su
    /// quelli generici, che restano come rete di sicurezza.
    /// </summary>
    private static (BankProfile? Profile, bool MatchedExplicitly) SelectProfile(
        RawEmail email,
        string text,
        BankOptions options)
    {
        IEnumerable<BankProfile> candidates = options.Profiles;
        if (options.UseBuiltInProfiles)
        {
            candidates = candidates.Concat(BuiltInBankProfiles.All);
        }

        var all = candidates.ToList();

        foreach (var profile in all)
        {
            var bySender = profile.SenderContains.Any(
                s => !string.IsNullOrEmpty(s) && email.Sender.Contains(s, StringComparison.OrdinalIgnoreCase));

            var byBody = profile.BodyContains.Any(
                s => !string.IsNullOrEmpty(s) && text.Contains(s, StringComparison.OrdinalIgnoreCase));

            if (bySender || byBody)
            {
                return (profile, true);
            }
        }

        var fallback = all.Find(p => p.SenderContains.Count == 0 && p.BodyContains.Count == 0);
        return (fallback, false);
    }

    private static (TransactionPattern? Pattern, Match? Match) MatchPattern(BankProfile profile, string text)
    {
        foreach (var pattern in profile.Patterns)
        {
            if (string.IsNullOrWhiteSpace(pattern.Regex))
            {
                continue;
            }

            try
            {
                var match = RegexCache.Get(pattern.Regex).Match(text);
                if (match.Success)
                {
                    return (pattern, match);
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // Formula troppo costosa su questo testo: si passa alla successiva invece di
                // far cadere l'intera acquisizione.
            }
            catch (ArgumentException)
            {
                // Espressione scritta male in configurazione: la si salta, il movimento
                // finira' fra quelli da rivedere.
            }
        }

        return (null, null);
    }

    /// <summary>Fonde le espressioni configurate con quelle incorporate, voce per voce.</summary>
    private static EnrichmentPatterns Merge(EnrichmentPatterns configured)
    {
        var defaults = BuiltInBankProfiles.DefaultEnrichment;

        return new EnrichmentPatterns
        {
            Amount = Pick(configured.Amount, defaults.Amount),
            Card = Pick(configured.Card, defaults.Card),
            Date = Pick(configured.Date, defaults.Date),
            Balance = Pick(configured.Balance, defaults.Balance),
            Merchant = Pick(configured.Merchant, defaults.Merchant),
            Reference = Pick(configured.Reference, defaults.Reference),
        };

        static string? Pick(string? configured, string? fallback) =>
            string.IsNullOrWhiteSpace(configured) ? fallback : configured;
    }

    private static Money? ReadAmount(Match match, string text, EnrichmentPatterns enrichment)
    {
        var raw = ReadGroup(match, "amount") ?? ReadEnriched(text, enrichment.Amount, "amount");

        if (raw is not null && AmountParser.TryFindTotal(raw, out var fromGroup))
        {
            return fromGroup;
        }

        return null;
    }

    private static long? ReadBalance(Match match, string text, EnrichmentPatterns enrichment)
    {
        var raw = ReadGroup(match, "balance") ?? ReadEnriched(text, enrichment.Balance, "balance");

        return raw is not null && AmountParser.TryFindTotal(raw, out var money) ? money.Value.Cents : null;
    }

    private static DateTime? ReadDate(Match match, string text, EnrichmentPatterns enrichment)
    {
        var raw = ReadGroup(match, "date") ?? ReadEnriched(text, enrichment.Date, "date");

        if (raw is null)
        {
            return null;
        }

        return DateTime.TryParseExact(
            raw.Trim(),
            DateFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed)
            ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
            : null;
    }

    private static string? ReadGroup(Match match, string groupName)
    {
        var group = match.Groups[groupName];
        return group.Success && !string.IsNullOrWhiteSpace(group.Value) ? group.Value.Trim() : null;
    }

    private static string? ReadEnriched(string text, string? pattern, string groupName)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return null;
        }

        try
        {
            var match = RegexCache.Get(pattern).Match(text);
            return match.Success ? ReadGroup(match, groupName) : null;
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Ripulisce il nome della controparte da punteggiatura e codici di coda, che le banche
    /// aggiungono spesso dopo il nome dell'esercente.
    /// </summary>
    private static string? CleanMerchant(string? merchant)
    {
        if (string.IsNullOrWhiteSpace(merchant))
        {
            return null;
        }

        var cleaned = merchant.Trim().Trim('*', '-', ':', ';', ',', '.', ' ');

        var separator = cleaned.IndexOf(" - ", StringComparison.Ordinal);
        if (separator > 2)
        {
            cleaned = cleaned[..separator];
        }

        cleaned = Regex.Replace(cleaned, @"\s{2,}", " ", RegexOptions.None, TimeSpan.FromMilliseconds(100));

        return cleaned.Length is 0 or > 120 ? null : cleaned;
    }
}
