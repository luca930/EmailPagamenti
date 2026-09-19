using EmailPagamenti.Application.Models;
using EmailPagamenti.Application.Options;
using EmailPagamenti.Application.Parsing;
using EmailPagamenti.Domain.Enums;
using Xunit;

namespace EmailPagamenti.Tests;

/// <summary>
/// Il profilo generico e' la rete di sicurezza per le notifiche che il profilo della banca
/// non copre ancora: deve riconoscere il movimento, ma con meno confidenza.
/// </summary>
public sealed class GenericBankProfileTests
{
    private static BankNotificationExtractor CreateExtractor()
    {
        var categorizer = new KeywordCategorizer(new StaticOptionsMonitor<CategoryOptions>(new CategoryOptions()));
        return new BankNotificationExtractor(new StaticOptionsMonitor<BankOptions>(new BankOptions()), categorizer);
    }

    private static RawEmail Email(string body, string subject = "Notifica") =>
        new(
            MessageId: "<test@banca.example>",
            Folder: "INBOX",
            Sender: "avvisi@banca.example",
            SenderDisplayName: null,
            Subject: subject,
            SentAt: new DateTime(2026, 3, 14, 10, 0, 0, DateTimeKind.Utc),
            TextBody: body,
            Attachments: []);

    [Fact]
    public void RiconosceUnPagamentoConCarta()
    {
        var result = CreateExtractor().Extract(Email(
            "Pagamento di 42,50 EUR con carta **** 1234\nEsercente: ESSELUNGA SPA\nin data 12/03/2026"));

        Assert.Equal(TransactionKind.CardPayment, result.Kind);
        Assert.Equal(PaymentDirection.Outgoing, result.Direction);
        Assert.Equal(42.50m, result.Total!.Value.Amount);
        Assert.Equal("1234", result.CardLast4);
        Assert.Equal("ESSELUNGA SPA", result.Merchant);
        Assert.Equal("Alimentari", result.Category);
        Assert.Equal(new DateTime(2026, 3, 12, 0, 0, 0, DateTimeKind.Utc), result.ValueDate);
    }

    [Fact]
    public void RiconosceUnPrelievo()
    {
        var result = CreateExtractor().Extract(Email("Prelievo di EUR 100,00 presso ATM Via Roma"));

        Assert.Equal(TransactionKind.CashWithdrawal, result.Kind);
        Assert.Equal(100.00m, result.Total!.Value.Amount);
        Assert.Equal("Prelievi", result.Category);
    }

    [Fact]
    public void UnBonificoInEntrataEUnMovimentoInEntrata()
    {
        var result = CreateExtractor().Extract(Email("Bonifico in entrata di 1.500,00 EUR\nOrdinante: Azienda Srl"));

        Assert.Equal(PaymentDirection.Incoming, result.Direction);
        Assert.Equal(TransactionKind.Transfer, result.Kind);
        Assert.Equal(1500.00m, result.Total!.Value.Amount);
    }

    [Fact]
    public void SenzaProfiloDellaBancaLaConfidenzaRestaPiuBassa()
    {
        var generico = CreateExtractor().Extract(Email("Pagamento di 42,50 EUR presso NEGOZIO"));

        Assert.True(generico.Confidence > 0d);
        Assert.True(generico.Confidence < 1d);
    }

    [Fact]
    public void UnOperazioneRifiutataNonEUnaSpesa()
    {
        var result = CreateExtractor().Extract(Email(
            "La tua operazione è stata rifiutata per fondi insufficienti.",
            subject: "Operazione non riuscita"));

        Assert.Equal(TransactionKind.Declined, result.Kind);
    }
}
