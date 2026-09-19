using EmailPagamenti.Application.Models;
using EmailPagamenti.Domain.Entities;

namespace EmailPagamenti.Web.Contracts;

/// <summary>Un movimento come lo vede l'interfaccia.</summary>
public sealed record TransactionDto(
    Guid Id,
    DateTime OccurredAt,
    string? Merchant,
    string Category,
    string Direction,
    string Kind,
    decimal? Amount,
    string? Currency,
    decimal Signed,
    string? CardLast4,
    string? Reference,
    string Subject,
    string Status,
    bool NeedsReview,
    bool ReviewedByHuman,
    double Confidence,
    string? MatchedRule,
    string? BodySnippet)
{
    public static TransactionDto From(Transaction t) =>
        new(
            t.Id,
            t.OccurredAt,
            t.Merchant,
            t.Category,
            t.Direction.ToString(),
            t.Kind.ToString(),
            t.Amount,
            t.Currency,
            t.SignedAmount,
            t.CardLast4,
            t.Reference,
            t.Subject,
            t.Status.ToString(),
            t.Status == Domain.Enums.ProcessingStatus.NeedsReview,
            t.ReviewedByHuman,
            t.Confidence,
            t.MatchedRule,
            t.BodySnippet);
}

/// <summary>Una pagina di risultati con il totale, per impaginare senza contare a mano.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

/// <summary>Le cifre in testa alla dashboard.</summary>
public sealed record SummaryDto(decimal Spent, decimal Received, decimal Net, int Count, int NeedsReview)
{
    public static SummaryDto From(PeriodSummary s) =>
        new(s.SpentTotal, s.ReceivedTotal, s.Net, s.Count, s.NeedsReviewCount);
}

/// <summary>Un punto della serie temporale.</summary>
public sealed record PointDto(string Date, decimal Spent, decimal Received);

/// <summary>Correzione inviata dall'interfaccia.</summary>
public sealed record CorrectionRequest(
    string Direction,
    string Kind,
    decimal? Amount,
    string? Currency,
    string? Merchant,
    string Category);

/// <summary>Valori che servono a popolare i filtri.</summary>
public sealed record MetaDto(
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> Kinds,
    IReadOnlyList<string> Directions);
