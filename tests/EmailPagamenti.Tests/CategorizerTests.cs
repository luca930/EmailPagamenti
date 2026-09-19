using EmailPagamenti.Application.Options;
using EmailPagamenti.Application.Parsing;
using EmailPagamenti.Domain.Enums;
using EmailPagamenti.Domain.ValueObjects;
using Xunit;

namespace EmailPagamenti.Tests;

public sealed class CategorizerTests
{
    private static KeywordCategorizer Create(CategoryOptions? options = null) =>
        new(new StaticOptionsMonitor<CategoryOptions>(options ?? new CategoryOptions()));

    [Theory]
    [InlineData("ESSELUNGA SPA MILANO", "Alimentari")]
    [InlineData("Q8 STAZIONE SERVIZIO", "Carburante")]
    [InlineData("NETFLIX.COM", "Abbonamenti")]
    [InlineData("FARMACIA CENTRALE", "Salute")]
    [InlineData("AMAZON EU SARL", "Shopping")]
    public void RiconosceLeCateneDiffuse(string merchant, string expected) =>
        Assert.Equal(expected, Create().Categorize(merchant, merchant, TransactionKind.CardPayment));

    [Fact]
    public void IlPrelievoHaLaSuaCategoriaQualunqueSiaLaControparte() =>
        Assert.Equal("Prelievi", Create().Categorize("ESSELUNGA", "testo", TransactionKind.CashWithdrawal));

    [Fact]
    public void LoStipendioFinisceTraLeEntrate() =>
        Assert.Equal("Entrate", Create().Categorize("Azienda Srl", "testo", TransactionKind.Income));

    [Fact]
    public void UnaRegolaConfigurataVinceSuQuelleIncorporate()
    {
        var options = new CategoryOptions
        {
            Rules = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Spesa ufficio"] = ["amazon"],
            },
        };

        Assert.Equal("Spesa ufficio", Create(options).Categorize("AMAZON EU SARL", "", TransactionKind.CardPayment));
    }

    [Fact]
    public void QuelloCheNonRiconosceRestaDaCategorizzare() =>
        Assert.Equal(
            SpendingCategory.Uncategorized,
            Create().Categorize("NEGOZIO SCONOSCIUTO 123", "", TransactionKind.CardPayment));
}
