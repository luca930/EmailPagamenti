using System.Globalization;
using EmailPagamenti.Application.Parsing;
using Xunit;

namespace EmailPagamenti.Tests;

public sealed class AmountParserTests
{
    // Gli importi arrivano come stringa: decimal non e' un tipo ammesso negli attributi.
    [Theory]
    [InlineData("Totale: \u20AC 1.234,56", "1234.56", "EUR")]
    [InlineData("Totale 1.234,56 \u20AC", "1234.56", "EUR")]
    [InlineData("Importo addebitato: EUR 49,90", "49.90", "EUR")]
    [InlineData("Charged $1,234.56 today", "1234.56", "USD")]
    [InlineData("Amount: 99.99 USD", "99.99", "USD")]
    [InlineData("Pagati 12,50\u20AC per il caffe", "12.50", "EUR")]
    [InlineData("Canone di 9 \u20AC al mese", "9", "EUR")]
    public void RiconosceIFormatiPiuComuni(string text, string expected, string currency)
    {
        var atteso = decimal.Parse(expected, CultureInfo.InvariantCulture);

        Assert.True(AmountParser.TryFindTotal(text, out var total));
        Assert.Equal(atteso, total!.Value.Amount);
        Assert.Equal(currency, total.Value.Currency);
    }

    [Fact]
    public void SceglieIlTotaleEnonLeVociIntermedie()
    {
        const string body = "Imponibile 100,00 € - IVA 22,00 € - Totale 122,00 €";

        Assert.True(AmountParser.TryFindTotal(body, out var total));
        Assert.Equal(122.00m, total!.Value.Amount);
    }

    [Fact]
    public void TrattaIlSeparatoreDelleMigliaiaSenzaDecimali()
    {
        Assert.True(AmountParser.TryFindTotal("Bonifico di 1.500 EUR", out var total));
        Assert.Equal(1500m, total!.Value.Amount);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Nessun importo qui dentro")]
    [InlineData("Il tuo ordine 123456 e' stato spedito")]
    public void NonInventaImportiQuandoNonCeNeSono(string? text)
    {
        Assert.False(AmountParser.TryFindTotal(text, out var total));
        Assert.Null(total);
    }

    [Fact]
    public void RestituisceTuttiGliImportiTrovati()
    {
        var all = AmountParser.FindAll("Spesa 10,00 € e poi 25,50 €");

        Assert.Equal(2, all.Count);
        Assert.Equal(10.00m, all[0].Amount);
        Assert.Equal(25.50m, all[1].Amount);
    }
}
