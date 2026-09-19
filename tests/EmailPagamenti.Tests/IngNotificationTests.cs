using EmailPagamenti.Application.Models;
using EmailPagamenti.Application.Options;
using EmailPagamenti.Application.Parsing;
using EmailPagamenti.Domain.Enums;
using Xunit;

namespace EmailPagamenti.Tests;

/// <summary>
/// Ricalca una notifica ING reale, con nomi e cifre sostituiti. E' il formato su cui il
/// profilo ING e' stato scritto: se cambia, questi test lo dicono subito.
/// </summary>
public sealed class IngNotificationTests
{
    private const string BonificoUscita = """
        Ciao Luca,
        ti confermiamo che è stato eseguito un bonifico dal tuo Conto Corrente Arancio n. *********.
        Ecco i dettagli dell'operazione:

        * Beneficiario: Mario Rossi
        * IBAN beneficiario: IT60X0542811101000000123456
        * Importo: 1.250,00 euro
        * TRN: AB12345678901

        Puoi visualizzare la contabile nella sezione Operazioni > Operazioni eseguite della tua
        Area Riservata su app o sito ING. Ti basta selezionare l'operazione e cliccare su Mostra PDF.
        Non sei stato tu?
        Vai in app > Altro > Segnalazioni
        """;

    private static BankNotificationExtractor CreateExtractor()
    {
        var categorizer = new KeywordCategorizer(new StaticOptionsMonitor<CategoryOptions>(new CategoryOptions()));
        return new BankNotificationExtractor(new StaticOptionsMonitor<BankOptions>(new BankOptions()), categorizer);
    }

    private static RawEmail Email(string body, string sender = "noreply@ing.it", string subject = "Bonifico eseguito") =>
        new(
            MessageId: "<test@ing.it>",
            Folder: "INBOX",
            Sender: sender,
            SenderDisplayName: "ING",
            Subject: subject,
            SentAt: new DateTime(2026, 3, 14, 10, 0, 0, DateTimeKind.Utc),
            TextBody: body,
            Attachments: []);

    [Fact]
    public void LeggeIlBonificoInUscita()
    {
        var result = CreateExtractor().Extract(Email(BonificoUscita));

        Assert.Equal(TransactionKind.Transfer, result.Kind);
        Assert.Equal(PaymentDirection.Outgoing, result.Direction);
        Assert.NotNull(result.Total);
        Assert.Equal(1250.00m, result.Total!.Value.Amount);
        Assert.Equal("EUR", result.Total.Value.Currency);
        Assert.Equal("AB12345678901", result.Reference);
        Assert.Equal(1d, result.Confidence);
    }

    [Fact]
    public void PrendeIlBeneficiarioEnonLIbanDelBeneficiario()
    {
        // Le due righe si somigliano e stanno una sotto l'altra: e' l'errore piu' facile da fare.
        var result = CreateExtractor().Extract(Email(BonificoUscita));

        Assert.Equal("Mario Rossi", result.Merchant);
    }

    [Fact]
    public void RiconosceLaBancaDalTestoAncheSeIlMittenteCambia()
    {
        var result = CreateExtractor().Extract(Email(BonificoUscita, sender: "servizio@dominio-sconosciuto.example"));

        // Il profilo scatta lo stesso grazie a "Conto Corrente Arancio" nel testo.
        Assert.StartsWith("ING:", result.MatchedRule, StringComparison.Ordinal);
        Assert.Equal(1250.00m, result.Total!.Value.Amount);
    }

    [Fact]
    public void UnaNotificaSenzaMovimentoNonDiventaUnaSpesa()
    {
        const string body = """
            Ciao Luca, abbiamo aggiornato le condizioni del tuo Conto Corrente Arancio.
            Puoi consultarle nella tua Area Riservata su app o sito ING.
            """;

        var result = CreateExtractor().Extract(Email(body, subject: "Aggiornamento condizioni"));

        Assert.Equal(0d, result.Confidence);
        Assert.Null(result.Total);
    }

    [Fact]
    public void UnEmailQualsiasiNonVieneScambiataPerUnMovimento()
    {
        var result = CreateExtractor().Extract(Email(
            "Ci vediamo domani alle 15 per la riunione.",
            sender: "collega@azienda.example",
            subject: "Riunione"));

        Assert.Equal(0d, result.Confidence);
    }
}
