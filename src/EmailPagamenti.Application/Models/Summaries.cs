namespace EmailPagamenti.Application.Models;

/// <summary>Totali di un periodo. Gli importi sono sempre positivi: il verso e' nel nome del campo.</summary>
public sealed record PeriodSummary(decimal SpentTotal, decimal ReceivedTotal, int Count, int NeedsReviewCount)
{
    /// <summary>Differenza tra entrate e uscite: positiva quando si e' messo da parte.</summary>
    public decimal Net => ReceivedTotal - SpentTotal;
}

/// <summary>Speso in una categoria nel periodo.</summary>
public sealed record CategoryTotal(string Category, decimal Total, int Count);

/// <summary>Speso presso un esercente nel periodo.</summary>
public sealed record MerchantTotal(string Merchant, decimal Total, int Count);

/// <summary>Un punto della serie temporale: un giorno o un mese.</summary>
public sealed record PeriodPoint(DateOnly Date, decimal Spent, decimal Received);
