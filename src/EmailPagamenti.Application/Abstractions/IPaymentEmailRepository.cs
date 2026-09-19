using EmailPagamenti.Application.Models;
using EmailPagamenti.Domain.Entities;

namespace EmailPagamenti.Application.Abstractions;

/// <summary>Accesso allo storico dei pagamenti.</summary>
public interface IPaymentEmailRepository
{
    /// <summary>Data dell'email piu' recente gia' acquisita, per riprendere da li'.</summary>
    Task<DateTimeOffset?> GetLastSentAtAsync(CancellationToken cancellationToken);

    /// <summary>Filtra gli hash gia' presenti, in una sola query invece che una per email.</summary>
    Task<IReadOnlySet<string>> GetExistingHashesAsync(
        IReadOnlyCollection<string> messageIdHashes,
        CancellationToken cancellationToken);

    Task AddAsync(PaymentEmail email, CancellationToken cancellationToken);

    Task<IReadOnlyList<PaymentEmail>> SearchAsync(PaymentEmailQuery query, CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
