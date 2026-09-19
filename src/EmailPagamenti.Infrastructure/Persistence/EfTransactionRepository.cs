using EmailPagamenti.Application.Abstractions;
using EmailPagamenti.Application.Models;
using EmailPagamenti.Domain.Entities;
using EmailPagamenti.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EmailPagamenti.Infrastructure.Persistence;

public sealed class EfTransactionRepository : ITransactionRepository
{
    private readonly TransactionsDbContext _db;

    public EfTransactionRepository(TransactionsDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<DateTime?> GetLastSentAtAsync(CancellationToken cancellationToken) =>
        await _db.Transactions
            .AsNoTracking()
            .OrderByDescending(e => e.SentAt)
            .Select(e => (DateTime?)e.SentAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlySet<string>> GetExistingHashesAsync(
        IReadOnlyCollection<string> messageIdHashes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messageIdHashes);

        if (messageIdHashes.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var found = await _db.Transactions
            .AsNoTracking()
            .Where(e => messageIdHashes.Contains(e.MessageIdHash))
            .Select(e => e.MessageIdHash)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return found.ToHashSet(StringComparer.Ordinal);
    }

    public async Task AddAsync(Transaction transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        await _db.Transactions.AddAsync(transaction, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Transaction?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.Transactions.FirstOrDefaultAsync(e => e.Id == id, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<Transaction>> SearchAsync(
        TransactionQuery query,
        CancellationToken cancellationToken) =>
        await Filter(query)
            .AsNoTracking()
            .OrderByDescending(e => e.OccurredAt)
            .Skip(query.Skip)
            .Take(query.NormalizedPageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<int> CountAsync(TransactionQuery query, CancellationToken cancellationToken) =>
        Filter(query).AsNoTracking().CountAsync(cancellationToken);

    public async Task<PeriodSummary> GetSummaryAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken)
    {
        // Una sola andata al database: le quattro cifre in testa alla dashboard si calcolano
        // tutte con la stessa scansione dell'indice su OccurredAt.
        var result = await Countable(from, to)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Spent = g.Sum(e => e.Direction == PaymentDirection.Outgoing ? e.AmountCents!.Value : 0L),
                Received = g.Sum(e => e.Direction == PaymentDirection.Incoming ? e.AmountCents!.Value : 0L),
                Count = g.Count(),
                NeedsReview = g.Count(e => e.Status == ProcessingStatus.NeedsReview),
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return result is null
            ? new PeriodSummary(0m, 0m, 0, 0)
            : new PeriodSummary(Euro(result.Spent), Euro(result.Received), result.Count, result.NeedsReview);
    }

    public async Task<IReadOnlyList<CategoryTotal>> GetCategoryTotalsAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken)
    {
        // Si proietta su un tipo anonimo e non direttamente sul record: EF non traduce la
        // chiamata a un costruttore dentro un raggruppamento. La somma resta comunque in SQL.
        var rows = await Countable(from, to)
            .Where(e => e.Direction == PaymentDirection.Outgoing)
            .GroupBy(e => e.Category)
            .Select(g => new { Category = g.Key, Total = g.Sum(e => e.AmountCents!.Value), Count = g.Count() })
            .OrderByDescending(r => r.Total)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(r => new CategoryTotal(r.Category, Euro(r.Total), r.Count)).ToList();
    }

    public async Task<IReadOnlyList<MerchantTotal>> GetTopMerchantsAsync(
        DateTime from,
        DateTime to,
        int limit,
        CancellationToken cancellationToken)
    {
        var rows = await Countable(from, to)
            .Where(e => e.Direction == PaymentDirection.Outgoing && e.Merchant != null)
            .GroupBy(e => e.Merchant!)
            .Select(g => new { Merchant = g.Key, Total = g.Sum(e => e.AmountCents!.Value), Count = g.Count() })
            .OrderByDescending(r => r.Total)
            .Take(Math.Clamp(limit, 1, 100))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(r => new MerchantTotal(r.Merchant, Euro(r.Total), r.Count)).ToList();
    }

    public async Task<IReadOnlyList<PeriodPoint>> GetDailyTotalsAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken)
    {
        // Si raggruppa su anno/mese/giorno e non su una data intera: e' l'unica forma che
        // entrambi i provider traducono in SQL senza ricadere sul client.
        var rows = await Countable(from, to)
            .GroupBy(e => new { e.OccurredAt.Year, e.OccurredAt.Month, e.OccurredAt.Day })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                g.Key.Day,
                Spent = g.Sum(e => e.Direction == PaymentDirection.Outgoing ? e.AmountCents!.Value : 0L),
                Received = g.Sum(e => e.Direction == PaymentDirection.Incoming ? e.AmountCents!.Value : 0L),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(r => new PeriodPoint(new DateOnly(r.Year, r.Month, r.Day), Euro(r.Spent), Euro(r.Received)))
            .OrderBy(p => p.Date)
            .ToList();
    }

    public async Task<IReadOnlyList<PeriodPoint>> GetMonthlyTotalsAsync(
        int months,
        CancellationToken cancellationToken)
    {
        var window = Math.Clamp(months, 1, 60);
        var to = DateTime.UtcNow;
        var from = new DateTime(to.Year, to.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-(window - 1));

        var rows = await Countable(from, to)
            .GroupBy(e => new { e.OccurredAt.Year, e.OccurredAt.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Spent = g.Sum(e => e.Direction == PaymentDirection.Outgoing ? e.AmountCents!.Value : 0L),
                Received = g.Sum(e => e.Direction == PaymentDirection.Incoming ? e.AmountCents!.Value : 0L),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(r => new PeriodPoint(new DateOnly(r.Year, r.Month, 1), Euro(r.Spent), Euro(r.Received)))
            .OrderBy(p => p.Date)
            .ToList();
    }

    public async Task<IReadOnlyList<string>> GetUsedCategoriesAsync(CancellationToken cancellationToken) =>
        await _db.Transactions
            .AsNoTracking()
            .Select(e => e.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        _db.SaveChangesAsync(cancellationToken);

    /// <summary>Riporta in euro una somma che il database ha calcolato in centesimi.</summary>
    private static decimal Euro(long cents) => cents / 100m;

    private static long Cents(decimal amount) => (long)decimal.Round(amount * 100m, 0, MidpointRounding.ToEven);

    /// <summary>Sfugge i caratteri jolly di LIKE (%, _ e lo stesso escape) prima di usarli in un pattern.</summary>
    private static string EscapeLikePattern(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    /// <summary>
    /// I movimenti che contano nei totali: classificati o da rivedere, con un importo, e non
    /// rifiutati. Un pagamento negato non e' una spesa e falserebbe ogni cifra della dashboard.
    /// </summary>
    private IQueryable<Transaction> Countable(DateTime from, DateTime to) =>
        _db.Transactions
            .AsNoTracking()
            .Where(e =>
                e.OccurredAt >= from
                && e.OccurredAt <= to
                && e.AmountCents != null
                && e.Kind != TransactionKind.Declined
                && (e.Status == ProcessingStatus.Classified || e.Status == ProcessingStatus.NeedsReview));

    private IQueryable<Transaction> Filter(TransactionQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Tutti i filtri restano lato database: niente materializzazione prima del filtro.
        var q = _db.Transactions.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            // Senza escape, un utente che cerca "100%" o "così_com'è" otterrebbe wildcard
            // impreviste invece di un confronto letterale.
            var text = EscapeLikePattern(query.Text.Trim());
            q = q.Where(e =>
                (e.Merchant != null && EF.Functions.Like(e.Merchant, $"%{text}%", "\\"))
                || EF.Functions.Like(e.Subject, $"%{text}%", "\\")
                || (e.Reference != null && EF.Functions.Like(e.Reference, $"%{text}%", "\\")));
        }

        if (!string.IsNullOrWhiteSpace(query.Merchant))
        {
            q = q.Where(e => e.Merchant == query.Merchant);
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            q = q.Where(e => e.Category == query.Category);
        }

        if (query.From is { } from)
        {
            q = q.Where(e => e.OccurredAt >= from);
        }

        if (query.To is { } to)
        {
            q = q.Where(e => e.OccurredAt <= to);
        }

        if (query.MinAmount is { } min)
        {
            var cents = Cents(min);
            q = q.Where(e => e.AmountCents >= cents);
        }

        if (query.MaxAmount is { } max)
        {
            var cents = Cents(max);
            q = q.Where(e => e.AmountCents <= cents);
        }

        if (query.Direction is { } direction)
        {
            q = q.Where(e => e.Direction == direction);
        }

        if (query.Kind is { } kind)
        {
            q = q.Where(e => e.Kind == kind);
        }

        if (query.Status is { } status)
        {
            q = q.Where(e => e.Status == status);
        }

        if (query.NeedsReview is true)
        {
            q = q.Where(e => e.Status == ProcessingStatus.NeedsReview);
        }

        return q;
    }
}
