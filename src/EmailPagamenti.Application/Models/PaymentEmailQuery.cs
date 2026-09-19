using EmailPagamenti.Domain.Enums;

namespace EmailPagamenti.Application.Models;

/// <summary>
/// Filtri per ritrovare un pagamento. Tutti opzionali: quelli valorizzati si sommano in AND.
/// </summary>
public sealed record PaymentEmailQuery
{
    /// <summary>Ricerca libera su esercente, oggetto e riferimento.</summary>
    public string? Text { get; init; }

    public string? Merchant { get; init; }

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    public decimal? MinAmount { get; init; }

    public decimal? MaxAmount { get; init; }

    public string? Currency { get; init; }

    public PaymentDirection? Direction { get; init; }

    public DocumentKind? Kind { get; init; }

    public ProcessingStatus? Status { get; init; }

    /// <summary>Pagina richiesta, a partire da 1.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Dimensione pagina, limitata a 200 per non caricare mezzo database in memoria.</summary>
    public int PageSize { get; init; } = 50;

    public int Skip => (Math.Max(Page, 1) - 1) * NormalizedPageSize;

    public int NormalizedPageSize => Math.Clamp(PageSize, 1, 200);
}
