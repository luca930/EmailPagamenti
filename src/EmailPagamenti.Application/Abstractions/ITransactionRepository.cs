using EmailPagamenti.Application.Models;
using EmailPagamenti.Domain.Entities;

namespace EmailPagamenti.Application.Abstractions;

/// <summary>Accesso allo storico dei movimenti.</summary>
public interface ITransactionRepository
{
    /// <summary>Data del movimento piu' recente gia' acquisito, per riprendere da li'.</summary>
    Task<DateTime?> GetLastSentAtAsync(CancellationToken cancellationToken);

    /// <summary>Filtra gli hash gia' presenti, in una sola query invece che una per email.</summary>
    Task<IReadOnlySet<string>> GetExistingHashesAsync(
        IReadOnlyCollection<string> messageIdHashes,
        CancellationToken cancellationToken);

    Task AddAsync(Transaction transaction, CancellationToken cancellationToken);

    Task<Transaction?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Transaction>> SearchAsync(TransactionQuery query, CancellationToken cancellationToken);

    /// <summary>Quanti movimenti soddisfano i filtri, per impaginare senza caricarli tutti.</summary>
    Task<int> CountAsync(TransactionQuery query, CancellationToken cancellationToken);

    Task<PeriodSummary> GetSummaryAsync(DateTime from, DateTime to, CancellationToken cancellationToken);

    Task<IReadOnlyList<CategoryTotal>> GetCategoryTotalsAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MerchantTotal>> GetTopMerchantsAsync(
        DateTime from,
        DateTime to,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>Serie giornaliera del periodo, per il grafico dell'andamento.</summary>
    Task<IReadOnlyList<PeriodPoint>> GetDailyTotalsAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken);

    /// <summary>Serie mensile degli ultimi mesi, per il confronto tra periodi.</summary>
    Task<IReadOnlyList<PeriodPoint>> GetMonthlyTotalsAsync(int months, CancellationToken cancellationToken);

    /// <summary>Categorie effettivamente presenti a database, per popolare i filtri.</summary>
    Task<IReadOnlyList<string>> GetUsedCategoriesAsync(CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
