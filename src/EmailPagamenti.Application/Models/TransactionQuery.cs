using EmailPagamenti.Domain.Enums;

namespace EmailPagamenti.Application.Models;

/// <summary>Filtri per ritrovare un movimento. Tutti opzionali: quelli valorizzati si sommano in AND.</summary>
public sealed record TransactionQuery
{
    /// <summary>Ricerca libera su esercente, oggetto e riferimento.</summary>
    public string? Text { get; init; }

    public string? Merchant { get; init; }

    public string? Category { get; init; }

    public DateTime? From { get; init; }

    public DateTime? To { get; init; }

    public decimal? MinAmount { get; init; }

    public decimal? MaxAmount { get; init; }

    public PaymentDirection? Direction { get; init; }

    public TransactionKind? Kind { get; init; }

    public ProcessingStatus? Status { get; init; }

    /// <summary>Solo i movimenti che aspettano una conferma umana.</summary>
    public bool? NeedsReview { get; init; }

    /// <summary>Pagina richiesta, a partire da 1.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Dimensione pagina, limitata a 200 per non caricare mezzo database in memoria.</summary>
    public int PageSize { get; init; } = 50;

    public int Skip => (Math.Max(Page, 1) - 1) * NormalizedPageSize;

    public int NormalizedPageSize => Math.Clamp(PageSize, 1, 200);
}
