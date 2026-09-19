using EmailPagamenti.Application.Models;
using EmailPagamenti.Application.Options;
using EmailPagamenti.Application.Parsing;
using EmailPagamenti.Domain.Enums;
using Microsoft.Extensions.Options;
using Xunit;

namespace EmailPagamenti.Tests;

public sealed class RuleBasedPaymentExtractorTests
{
    private static RuleBasedPaymentExtractor CreateExtractor(ClassificationOptions? options = null) =>
        new(new StaticOptionsMonitor<ClassificationOptions>(options ?? new ClassificationOptions()));

    private static RawEmail Email(
        string sender = "no-reply@negozio.it",
        string subject = "Oggetto",
        string? body = null,
        string? displayName = null,
        params RawAttachment[] attachments) =>
        new(
            MessageId: $"<{Guid.NewGuid():N}@test>",
            Folder: "INBOX",
            Sender: sender,
            SenderDisplayName: displayName,
            Subject: subject,
            SentAt: new DateTimeOffset(2026, 3, 14, 10, 0, 0, TimeSpan.Zero),
            TextBody: body,
            Attachments: attachments);

    [Fact]
    public void RiconosceUnaRicevutaConImporto()
    {
        var extractor = CreateExtractor();

        var result = extractor.Extract(Email(
            subject: "Ricevuta del tuo pagamento",
            body: "Grazie! Abbiamo addebitato 29,90 € sulla tua carta."));

        Assert.True(result.Confidence >= 0.5d);
        Assert.Equal(DocumentKind.Receipt, result.Kind);
        Assert.Equal(PaymentDirection.Outgoing, result.Direction);
        Assert.NotNull(result.Total);
        Assert.Equal(29.90m, result.Total!.Value.Amount);
    }

    [Fact]
    public void ScartaLeNewsletterAncheSeParlanoDiPrezzi()
    {
        var extractor = CreateExtractor();

        var result = extractor.Extract(Email(
            subject: "Offerta esclusiva per te",
            body: "Sconto del 30%! Tutto a partire da 19,90 €. Per non ricevere piu' queste email, unsubscribe."));

        Assert.Equal(0d, result.Confidence);
        Assert.Equal(DocumentKind.Unknown, result.Kind);
        Assert.StartsWith("exclude:", result.MatchedRule, StringComparison.Ordinal);
    }

    [Fact]
    public void UnaRegolaEsercenteHaLaPrecedenzaSulleParoleChiave()
    {
        var options = new ClassificationOptions
        {
            Merchants =
            [
                new MerchantRule
                {
                    Name = "Netflix",
                    SenderContains = "netflix.com",
                    Kind = DocumentKind.Subscription,
                    Direction = PaymentDirection.Outgoing,
                },
            ],
        };

        var result = CreateExtractor(options).Extract(Email(
            sender: "info@netflix.com",
            subject: "Il tuo abbonamento",
            body: "Importo 12,99 €"));

        Assert.Equal("Netflix", result.Merchant);
        Assert.Equal(DocumentKind.Subscription, result.Kind);
        Assert.Equal("merchant:Netflix", result.MatchedRule);
    }

    [Fact]
    public void UnRimborsoDiventaUnMovimentoInEntrata()
    {
        var result = CreateExtractor().Extract(Email(
            subject: "Rimborso effettuato",
            body: "Ti abbiamo accreditato 45,00 € per il reso."));

        Assert.Equal(PaymentDirection.Incoming, result.Direction);
        Assert.Equal(DocumentKind.Refund, result.Kind);
    }

    [Fact]
    public void EstraeIlRiferimentoDellOrdine()
    {
        var result = CreateExtractor().Extract(Email(
            subject: "Conferma ordine",
            body: "Ordine n. 403-1234567-8901234 - totale 15,00 €"));

        Assert.Equal("403-1234567-8901234", result.Reference);
    }

    [Fact]
    public void SenzaAlcunSegnaleNonClassifica()
    {
        var result = CreateExtractor().Extract(Email(
            subject: "Ci vediamo domani",
            body: "Ricordati la riunione delle 15."));

        Assert.Equal(0d, result.Confidence);
        Assert.Equal("no-signal", result.MatchedRule);
    }

    [Fact]
    public void UnMittenteAttendibileAlzaLaConfidenza()
    {
        var options = new ClassificationOptions { TrustedSenders = ["banca.it"] };

        var conosciuto = CreateExtractor(options).Extract(Email(
            sender: "avvisi@banca.it",
            subject: "Addebito eseguito",
            body: "Importo 80,00 €"));

        var sconosciuto = CreateExtractor(options).Extract(Email(
            sender: "avvisi@sconosciuto.xyz",
            subject: "Addebito eseguito",
            body: "Importo 80,00 €"));

        Assert.True(conosciuto.Confidence > sconosciuto.Confidence);
    }

    /// <summary>IOptionsMonitor minimo: evita di tirarsi dietro una libreria di mock per due test.</summary>
    private sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public StaticOptionsMonitor(T value) => CurrentValue = value;

        public T CurrentValue { get; }

        public T Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
