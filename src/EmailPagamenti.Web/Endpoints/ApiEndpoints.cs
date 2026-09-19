using System.Globalization;
using EmailPagamenti.Application.Abstractions;
using EmailPagamenti.Application.Models;
using EmailPagamenti.Application.Pipeline;
using EmailPagamenti.Domain.Enums;
using EmailPagamenti.Domain.ValueObjects;
using EmailPagamenti.Web.Contracts;

namespace EmailPagamenti.Web.Endpoints;

/// <summary>Le rotte che l'interfaccia consuma. Tutte sotto <c>/api</c> e tutte protette.</summary>
public static class ApiEndpoints
{
    public static RouteGroupBuilder MapApi(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/summary", async (
            string? from,
            string? to,
            ITransactionRepository repository,
            CancellationToken cancellationToken) =>
        {
            var (start, end) = Period(from, to);
            var summary = await repository.GetSummaryAsync(start, end, cancellationToken);
            return Results.Ok(SummaryDto.From(summary));
        });

        group.MapGet("/categories", async (
            string? from,
            string? to,
            ITransactionRepository repository,
            CancellationToken cancellationToken) =>
        {
            var (start, end) = Period(from, to);
            return Results.Ok(await repository.GetCategoryTotalsAsync(start, end, cancellationToken));
        });

        group.MapGet("/merchants", async (
            string? from,
            string? to,
            int? limit,
            ITransactionRepository repository,
            CancellationToken cancellationToken) =>
        {
            var (start, end) = Period(from, to);
            return Results.Ok(await repository.GetTopMerchantsAsync(start, end, limit ?? 8, cancellationToken));
        });

        group.MapGet("/timeline/daily", async (
            string? from,
            string? to,
            ITransactionRepository repository,
            CancellationToken cancellationToken) =>
        {
            var (start, end) = Period(from, to);
            var points = await repository.GetDailyTotalsAsync(start, end, cancellationToken);
            return Results.Ok(points.Select(ToDto));
        });

        group.MapGet("/timeline/monthly", async (
            int? months,
            ITransactionRepository repository,
            CancellationToken cancellationToken) =>
        {
            var points = await repository.GetMonthlyTotalsAsync(months ?? 12, cancellationToken);
            return Results.Ok(points.Select(ToDto));
        });

        group.MapGet("/transactions", async (
            HttpRequest request,
            ITransactionRepository repository,
            CancellationToken cancellationToken) =>
        {
            var query = BuildQuery(request);

            var items = await repository.SearchAsync(query, cancellationToken);
            var total = await repository.CountAsync(query, cancellationToken);

            return Results.Ok(new PagedResult<TransactionDto>(
                items.Select(TransactionDto.From).ToList(),
                total,
                query.Page,
                query.NormalizedPageSize));
        });

        group.MapGet("/transactions/{id:guid}", async (
            Guid id,
            ITransactionRepository repository,
            CancellationToken cancellationToken) =>
        {
            var found = await repository.GetAsync(id, cancellationToken);
            return found is null ? Results.NotFound() : Results.Ok(TransactionDto.From(found));
        });

        group.MapPut("/transactions/{id:guid}", async (
            Guid id,
            CorrectionRequest correction,
            ITransactionRepository repository,
            CancellationToken cancellationToken) =>
        {
            var found = await repository.GetAsync(id, cancellationToken);
            if (found is null)
            {
                return Results.NotFound();
            }

            if (!Enum.TryParse<PaymentDirection>(correction.Direction, ignoreCase: true, out var direction)
                || !Enum.TryParse<TransactionKind>(correction.Kind, ignoreCase: true, out var kind))
            {
                return Results.BadRequest(new { errore = "Verso o tipo di movimento non validi." });
            }

            Money? money = null;
            if (correction.Amount is { } amount and > 0m)
            {
                money = Money.Create(amount, string.IsNullOrWhiteSpace(correction.Currency) ? "EUR" : correction.Currency);
            }

            found.ApplyManualCorrection(direction, kind, money, correction.Merchant, correction.Category);
            await repository.SaveChangesAsync(cancellationToken);

            return Results.Ok(TransactionDto.From(found));
        });

        group.MapGet("/meta", async (ITransactionRepository repository, CancellationToken cancellationToken) =>
        {
            var used = await repository.GetUsedCategoriesAsync(cancellationToken);

            // Si uniscono le categorie proposte con quelle gia' presenti: il filtro mostra
            // anche le categorie aggiunte a mano in configurazione.
            var categories = SpendingCategory.Defaults
                .Concat(used)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c, StringComparer.CurrentCulture)
                .ToList();

            return Results.Ok(new MetaDto(
                categories,
                Enum.GetNames<TransactionKind>(),
                Enum.GetNames<PaymentDirection>()));
        });

        group.MapPost("/ingest", async (EmailIngestionService ingestion, CancellationToken cancellationToken) =>
        {
            var result = await ingestion.RunAsync(cancellationToken);
            return Results.Ok(new
            {
                lette = result.Fetched,
                salvate = result.Stored,
                duplicate = result.Duplicates,
                scartate = result.Ignored,
            });
        });

        return group;
    }

    private static PointDto ToDto(PeriodPoint p) =>
        new(p.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), p.Spent, p.Received);

    private static TransactionQuery BuildQuery(HttpRequest request)
    {
        var q = request.Query;

        return new TransactionQuery
        {
            Text = Value(q["text"]),
            Merchant = Value(q["merchant"]),
            Category = Value(q["category"]),
            From = Date(q["from"]),
            To = EndOfDay(q["to"]),
            MinAmount = Decimal(q["minAmount"]),
            MaxAmount = Decimal(q["maxAmount"]),
            Direction = Enum<PaymentDirection>(q["direction"]),
            Kind = Enum<TransactionKind>(q["kind"]),
            NeedsReview = Value(q["needsReview"]) is "true" ? true : null,
            Page = Int(q["page"]) ?? 1,
            PageSize = Int(q["pageSize"]) ?? 50,
        };

        static string? Value(Microsoft.Extensions.Primitives.StringValues v) =>
            string.IsNullOrWhiteSpace(v) ? null : v.ToString().Trim();

        static int? Int(Microsoft.Extensions.Primitives.StringValues v) =>
            int.TryParse(v, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

        static decimal? Decimal(Microsoft.Extensions.Primitives.StringValues v) =>
            decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

        static TEnum? Enum<TEnum>(Microsoft.Extensions.Primitives.StringValues v)
            where TEnum : struct =>
            System.Enum.TryParse<TEnum>(v, ignoreCase: true, out var parsed) ? parsed : null;
    }

    /// <summary>Il periodo di default e' il mese corrente: e' la domanda che si fa piu' spesso.</summary>
    private static (DateTime From, DateTime To) Period(string? from, string? to)
    {
        var now = DateTime.UtcNow;
        var start = Parse(from) ?? new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = Parse(to)?.AddDays(1).AddTicks(-1) ?? start.AddMonths(1).AddTicks(-1);

        return end < start ? (end, start) : (start, end);
    }

    private static DateTime? Date(Microsoft.Extensions.Primitives.StringValues value) => Parse(value.ToString());

    private static DateTime? EndOfDay(Microsoft.Extensions.Primitives.StringValues value) =>
        Parse(value.ToString())?.AddDays(1).AddTicks(-1);

    private static DateTime? Parse(string? value) =>
        DateTime.TryParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
            : null;
}
