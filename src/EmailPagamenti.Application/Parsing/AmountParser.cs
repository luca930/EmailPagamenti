using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using EmailPagamenti.Domain.ValueObjects;

namespace EmailPagamenti.Application.Parsing;

/// <summary>
/// Estrae importi da testo libero, gestendo insieme le convenzioni italiane (1.234,56 EUR)
/// e anglosassoni ($1,234.56). La regex e' generata a compile time: zero costo di
/// interpretazione a runtime, e questo metodo gira su ogni email.
/// </summary>
public static partial class AmountParser
{
    private const string NumberPattern = @"\d{1,3}(?:[.,  ]\d{3})+(?:[.,]\d{1,2})?|\d+(?:[.,]\d{1,2})?";

    private const string CurrencyPattern = @"€|\$|£|\bEUR\b|\bUSD\b|\bGBP\b|\bCHF\b";

    [GeneratedRegex(
        $"(?:(?<curBefore>{CurrencyPattern})\\s*(?<numAfter>{NumberPattern}))|(?:(?<numBefore>{NumberPattern})\\s*(?<curAfter>{CurrencyPattern}))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 250)]
    private static partial Regex AmountRegex();

    /// <summary>Tutti gli importi trovati nel testo, nell'ordine in cui compaiono.</summary>
    public static IReadOnlyList<Money> FindAll(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        List<Money> results = [];

        foreach (var match in AmountRegex().EnumerateMatches(text))
        {
            // EnumerateMatches non espone i gruppi: si rilegge la porzione trovata.
            var slice = text.AsSpan(match.Index, match.Length);
            if (TryParseSingle(slice, out var money))
            {
                results.Add(money);
            }
        }

        return results;
    }

    /// <summary>
    /// L'importo piu' alto trovato. Nelle ricevute il totale e' quasi sempre il numero
    /// piu' grande: gli altri sono imponibile, IVA, sconti o spedizione.
    /// </summary>
    public static bool TryFindTotal(string? text, [NotNullWhen(true)] out Money? total)
    {
        var all = FindAll(text);
        if (all.Count == 0)
        {
            total = null;
            return false;
        }

        var best = all[0];
        for (var i = 1; i < all.Count; i++)
        {
            if (all[i].Amount > best.Amount)
            {
                best = all[i];
            }
        }

        total = best;
        return true;
    }

    private static bool TryParseSingle(ReadOnlySpan<char> slice, out Money money)
    {
        money = default;

        var currency = ExtractCurrency(slice);
        if (currency is null)
        {
            return false;
        }

        Span<char> digits = stackalloc char[slice.Length];
        var length = 0;
        foreach (var c in slice)
        {
            if (char.IsAsciiDigit(c) || c is '.' or ',')
            {
                digits[length++] = c;
            }
        }

        if (length == 0)
        {
            return false;
        }

        var normalized = Normalize(digits[..length]);
        if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            return false;
        }

        money = Money.Create(amount, currency);
        return true;
    }

    private static string? ExtractCurrency(ReadOnlySpan<char> slice)
    {
        foreach (var c in slice)
        {
            switch (c)
            {
                case '€':
                    return "EUR";
                case '$':
                    return "USD";
                case '£':
                    return "GBP";
            }
        }

        if (slice.Contains("EUR", StringComparison.OrdinalIgnoreCase))
        {
            return "EUR";
        }

        if (slice.Contains("USD", StringComparison.OrdinalIgnoreCase))
        {
            return "USD";
        }

        if (slice.Contains("GBP", StringComparison.OrdinalIgnoreCase))
        {
            return "GBP";
        }

        return slice.Contains("CHF", StringComparison.OrdinalIgnoreCase) ? "CHF" : null;
    }

    /// <summary>
    /// Porta il numero in formato invariante. Regola: quando compaiono entrambi i separatori
    /// l'ultimo e' il decimale; quando ce n'e' uno solo seguito da tre cifre e' una migliaia.
    /// </summary>
    private static string Normalize(ReadOnlySpan<char> raw)
    {
        var value = raw.ToString();
        var lastDot = value.LastIndexOf('.');
        var lastComma = value.LastIndexOf(',');

        if (lastDot >= 0 && lastComma >= 0)
        {
            return lastComma > lastDot
                ? value.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.')
                : value.Replace(",", string.Empty, StringComparison.Ordinal);
        }

        if (lastComma >= 0)
        {
            var decimals = value.Length - lastComma - 1;
            return decimals == 3
                ? value.Replace(",", string.Empty, StringComparison.Ordinal)
                : value.Replace(',', '.');
        }

        if (lastDot >= 0)
        {
            var decimals = value.Length - lastDot - 1;
            return decimals == 3
                ? value.Replace(".", string.Empty, StringComparison.Ordinal)
                : value;
        }

        return value;
    }
}
