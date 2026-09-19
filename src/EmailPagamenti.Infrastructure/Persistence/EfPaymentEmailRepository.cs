using EmailPagamenti.Application.Abstractions;
using EmailPagamenti.Application.Models;
using EmailPagamenti.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmailPagamenti.Infrastructure.Persistence;

public sealed class EfPaymentEmailRepository : IPaymentEmailRepository
{
    private readonly PaymentsDbContext _db;

    public EfPaymentEmailRepository(PaymentsDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<DateTimeOffset?> GetLastSentAtAsync(CancellationToken cancellationToken) =>
        await _db.PaymentEmails
            .AsNoTracking()
            .OrderByDescending(e => e.SentAt)
            .Select(e => (DateTimeOffset?)e.SentAt)
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

        var found = await _db.PaymentEmails
            .AsNoTracking()
            .Where(e => messageIdHashes.Contains(e.MessageIdHash))
            .Select(e => e.MessageIdHash)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return found.ToHashSet(StringComparer.Ordinal);
    }

    public async Task AddAsync(PaymentEmail email, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);
        await _db.PaymentEmails.AddAsync(email, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PaymentEmail>> SearchAsync(
        PaymentEmailQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Tutti i filtri restano lato database: niente materializzazione prima del filtro.
        var q = _db.PaymentEmails.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            var text = query.Text.Trim();
            q = q.Where(e =>
                (e.Merchant != null && EF.Functions.Like(e.Merchant, $"%{text}%"))
                || EF.Functions.Like(e.Subject, $"%{text}%")
                || (e.Reference != null && EF.Functions.Like(e.Reference, $"%{text}%")));
        }

        if (!string.IsNullOrWhiteSpace(query.Merchant))
        {
            q = q.Where(e => e.Merchant == query.Merchant);
        }

        if (query.From is { } from)
        {
            q = q.Where(e => e.SentAt >= from);
        }

        if (query.To is { } to)
        {
            q = q.Where(e => e.SentAt <= to);
        }

        if (query.MinAmount is { } min)
        {
            q = q.Where(e => e.Amount >= min);
        }

        if (query.MaxAmount is { } max)
        {
            q = q.Where(e => e.Amount <= max);
        }

        if (!string.IsNullOrWhiteSpace(query.Currency))
        {
            q = q.Where(e => e.Currency == query.Currency);
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

        return await q
            .OrderByDescending(e => e.SentAt)
            .Skip(query.Skip)
            .Take(query.NormalizedPageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        _db.SaveChangesAsync(cancellationToken);
}
