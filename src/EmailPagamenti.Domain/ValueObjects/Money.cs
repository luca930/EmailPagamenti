using System.Globalization;

namespace EmailPagamenti.Domain.ValueObjects;

/// <summary>
/// Importo con valuta. Struct immutabile: niente allocazioni sull'heap nel percorso di parsing,
/// che e' il piu' caldo dell'intera pipeline.
/// </summary>
/// <param name="Amount">Importo, sempre con segno positivo: il verso sta in <see cref="Enums.PaymentDirection"/>.</param>
/// <param name="Currency">Codice ISO 4217 a tre lettere, maiuscolo.</param>
public readonly record struct Money(decimal Amount, string Currency)
{
    /// <summary>Crea un importo validando la valuta e normalizzandola in maiuscolo.</summary>
    /// <exception cref="ArgumentException">La valuta non e' un codice ISO 4217 di tre lettere.</exception>
    public static Money Create(decimal amount, string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        var normalized = currency.Trim().ToUpperInvariant();
        if (normalized.Length != 3 || !normalized.All(char.IsAsciiLetterUpper))
        {
            throw new ArgumentException($"Valuta non valida: '{currency}'. Atteso un codice ISO 4217 (es. EUR).", nameof(currency));
        }

        return new Money(decimal.Round(amount, 2, MidpointRounding.ToEven), normalized);
    }

    public override string ToString() =>
        string.Create(CultureInfo.GetCultureInfo("it-IT"), $"{Amount:N2} {Currency}");
}
