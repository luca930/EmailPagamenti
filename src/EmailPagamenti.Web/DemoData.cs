using System.Security.Cryptography;
using System.Text;
using EmailPagamenti.Domain.Entities;
using EmailPagamenti.Domain.Enums;
using EmailPagamenti.Domain.ValueObjects;
using EmailPagamenti.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EmailPagamenti.Web;

/// <summary>
/// Movimenti d'esempio per provare la dashboard senza collegare una casella di posta vera.
/// Si attiva con <c>Demo__Seed=true</c> e non tocca nulla se il database ha gia' dei dati.
/// </summary>
public static class DemoData
{
    public static async Task SeedIfEmptyAsync(TransactionsDbContext db, TimeProvider time, CancellationToken cancellationToken)
    {
        if (await db.Transactions.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var oggi = time.GetUtcNow().UtcDateTime.Date;
        var movimenti = new List<Transaction>();

        void Aggiungi(
            int giorniFa,
            string mittente,
            string oggetto,
            PaymentDirection direzione,
            TransactionKind tipo,
            decimal? importo,
            string? esercente,
            string categoria,
            bool daRivedere = false)
        {
            var quando = oggi.AddDays(-giorniFa);
            var messageId = $"<demo-{movimenti.Count}@esempio.local>";
            var transazione = Transaction.Create(
                messageId,
                MessageIdHash(messageId),
                "INBOX",
                mittente,
                "Demo",
                oggetto,
                quando,
                time.GetUtcNow().UtcDateTime,
                bodySnippet: null);

            transazione.MarkClassified(
                direzione,
                tipo,
                importo is { } valore ? Money.Create(valore, "EUR") : null,
                esercente,
                categoria,
                cardLast4: null,
                balanceAfter: null,
                valueDate: quando,
                reference: null,
                confidence: daRivedere ? 0.4d : 0.95d,
                matchedRule: "demo",
                needsReview: daRivedere);

            movimenti.Add(transazione);
        }

        // Tre stipendi, uno al mese.
        Aggiungi(2, "azienda@esempio.local", "Accredito stipendio", PaymentDirection.Incoming, TransactionKind.Income, 1850m, "Datore di lavoro Srl", "Entrate");
        Aggiungi(32, "azienda@esempio.local", "Accredito stipendio", PaymentDirection.Incoming, TransactionKind.Income, 1850m, "Datore di lavoro Srl", "Entrate");
        Aggiungi(62, "azienda@esempio.local", "Accredito stipendio", PaymentDirection.Incoming, TransactionKind.Income, 1850m, "Datore di lavoro Srl", "Entrate");

        // Spesa ricorrente.
        Aggiungi(1, "banca@esempio.local", "Pagamento con carta", PaymentDirection.Outgoing, TransactionKind.CardPayment, 64.30m, "ESSELUNGA SPA", "Alimentari");
        Aggiungi(8, "banca@esempio.local", "Pagamento con carta", PaymentDirection.Outgoing, TransactionKind.CardPayment, 41.10m, "ESSELUNGA SPA", "Alimentari");
        Aggiungi(15, "banca@esempio.local", "Pagamento con carta", PaymentDirection.Outgoing, TransactionKind.CardPayment, 52.90m, "COOP ITALIA", "Alimentari");
        Aggiungi(22, "banca@esempio.local", "Pagamento con carta", PaymentDirection.Outgoing, TransactionKind.CardPayment, 38.45m, "ESSELUNGA SPA", "Alimentari");

        Aggiungi(3, "banca@esempio.local", "Pagamento con carta", PaymentDirection.Outgoing, TransactionKind.CardPayment, 45m, "Q8 STAZIONE SERVIZIO", "Carburante");
        Aggiungi(18, "banca@esempio.local", "Pagamento con carta", PaymentDirection.Outgoing, TransactionKind.CardPayment, 50m, "ENI STAZIONE SERVIZIO", "Carburante");

        Aggiungi(5, "banca@esempio.local", "Addebito diretto", PaymentDirection.Outgoing, TransactionKind.DirectDebit, 12.99m, "NETFLIX.COM", "Abbonamenti");
        Aggiungi(10, "banca@esempio.local", "Addebito diretto", PaymentDirection.Outgoing, TransactionKind.DirectDebit, 9.99m, "SPOTIFY", "Abbonamenti");

        Aggiungi(7, "banca@esempio.local", "Bonifico in uscita", PaymentDirection.Outgoing, TransactionKind.Transfer, 650m, "Proprietario di casa", "Casa e bollette");
        Aggiungi(20, "banca@esempio.local", "Addebito diretto", PaymentDirection.Outgoing, TransactionKind.DirectDebit, 78.40m, "ENEL ENERGIA", "Casa e bollette");
        Aggiungi(28, "banca@esempio.local", "Addebito diretto", PaymentDirection.Outgoing, TransactionKind.DirectDebit, 24.90m, "WINDTRE", "Casa e bollette");

        Aggiungi(12, "banca@esempio.local", "Pagamento con carta", PaymentDirection.Outgoing, TransactionKind.CardPayment, 18.50m, "FARMACIA CENTRALE", "Salute");
        Aggiungi(25, "banca@esempio.local", "Pagamento con carta", PaymentDirection.Outgoing, TransactionKind.CardPayment, 89.90m, "DECATHLON ITALIA", "Shopping");
        Aggiungi(33, "banca@esempio.local", "Pagamento con carta", PaymentDirection.Outgoing, TransactionKind.CardPayment, 34.20m, "AMAZON EU SARL", "Shopping");

        Aggiungi(9, "banca@esempio.local", "Prelievo bancomat", PaymentDirection.Outgoing, TransactionKind.CashWithdrawal, 100m, null, "Prelievi");

        // Due movimenti volutamente incompleti, per provare la correzione manuale.
        Aggiungi(4, "notifiche@esempio.local", "Movimento sul conto", PaymentDirection.Outgoing, TransactionKind.CardPayment, 27.50m, "ESERCENTE SCONOSCIUTO 4471", "Da categorizzare", daRivedere: true);
        Aggiungi(14, "notifiche@esempio.local", "Movimento sul conto", PaymentDirection.Outgoing, TransactionKind.Unknown, null, null, "Da categorizzare", daRivedere: true);

        db.Transactions.AddRange(movimenti);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string MessageIdHash(string messageId)
    {
        Span<byte> destination = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(messageId), destination);
        return Convert.ToHexStringLower(destination);
    }
}
