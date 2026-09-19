using EmailPagamenti.Application.Models;
using EmailPagamenti.Domain.Entities;
using EmailPagamenti.Domain.Enums;
using EmailPagamenti.Domain.ValueObjects;
using EmailPagamenti.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EmailPagamenti.Tests;

/// <summary>
/// Gira su uno SQLite vero in memoria, non su un finto database: e' l'unico modo per
/// accorgersi che un raggruppamento non e' traducibile in SQL prima di scoprirlo in esercizio.
/// </summary>
public sealed class RepositoryAggregationTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private TransactionsDbContext _db = null!;
    private EfTransactionRepository _repository = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<TransactionsDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new TransactionsDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        _repository = new EfTransactionRepository(_db);

        await SeedAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private async Task SeedAsync()
    {
        Add("ESSELUNGA", "Alimentari", 50m, PaymentDirection.Outgoing, TransactionKind.CardPayment, 5);
        Add("ESSELUNGA", "Alimentari", 30m, PaymentDirection.Outgoing, TransactionKind.CardPayment, 6);
        Add("Q8", "Carburante", 70m, PaymentDirection.Outgoing, TransactionKind.CardPayment, 7);
        Add("Azienda Srl", "Entrate", 2000m, PaymentDirection.Incoming, TransactionKind.Income, 1);

        // Rifiutato: non deve entrare in nessun totale.
        Add("NEGOZIO", "Shopping", 999m, PaymentDirection.Outgoing, TransactionKind.Declined, 8);

        await _db.SaveChangesAsync();
    }

    private void Add(
        string merchant,
        string category,
        decimal amount,
        PaymentDirection direction,
        TransactionKind kind,
        int day)
    {
        var occurred = new DateTime(2026, 3, day, 12, 0, 0, DateTimeKind.Utc);

        var entity = Transaction.Create(
            $"<{Guid.NewGuid():N}@test>",
            Guid.NewGuid().ToString("N"),
            "INBOX",
            "avvisi@ing.it",
            "ING",
            $"Movimento {merchant}",
            occurred,
            occurred,
            null);

        entity.MarkClassified(
            direction,
            kind,
            Money.Create(amount, "EUR"),
            merchant,
            category,
            null,
            null,
            occurred,
            null,
            1d,
            "test",
            needsReview: false);

        _db.Transactions.Add(entity);
    }

    private static readonly DateTime MarzoInizio = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime MarzoFine = new(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc);

    [Fact]
    public async Task IlTotaleDelMeseEscludeIMovimentiRifiutati()
    {
        var summary = await _repository.GetSummaryAsync(MarzoInizio, MarzoFine, CancellationToken.None);

        Assert.Equal(150m, summary.SpentTotal);
        Assert.Equal(2000m, summary.ReceivedTotal);
        Assert.Equal(1850m, summary.Net);
        Assert.Equal(4, summary.Count);
    }

    [Fact]
    public async Task IlDettaglioPerCategoriaEOrdinatoDalPiuSpeso()
    {
        var totals = await _repository.GetCategoryTotalsAsync(
            MarzoInizio,
            MarzoFine,
            CancellationToken.None);

        Assert.Equal(2, totals.Count);
        Assert.Equal("Alimentari", totals[0].Category);
        Assert.Equal(80m, totals[0].Total);
        Assert.Equal("Carburante", totals[1].Category);
    }

    [Fact]
    public async Task GliEsercentiPiuCostosiVengonoPrima()
    {
        var merchants = await _repository.GetTopMerchantsAsync(
            MarzoInizio,
            MarzoFine,
            10,
            CancellationToken.None);

        Assert.Equal("ESSELUNGA", merchants[0].Merchant);
        Assert.Equal(80m, merchants[0].Total);
        Assert.Equal(2, merchants[0].Count);
    }

    [Fact]
    public async Task LaSerieGiornalieraSiTraduceInSql()
    {
        var points = await _repository.GetDailyTotalsAsync(
            MarzoInizio,
            MarzoFine,
            CancellationToken.None);

        Assert.Equal(4, points.Count);
        Assert.Equal(new DateOnly(2026, 3, 1), points[0].Date);
        Assert.Equal(2000m, points[0].Received);
        Assert.Equal(50m, points[1].Spent);
    }

    [Fact]
    public async Task LaRicercaPerTestoFiltraSullEsercente()
    {
        var found = await _repository.SearchAsync(
            new TransactionQuery { Text = "ESSELUNGA" },
            CancellationToken.None);

        Assert.Equal(2, found.Count);
        Assert.All(found, t => Assert.Equal("ESSELUNGA", t.Merchant));
    }

    [Fact]
    public async Task LaRicercaPerTestoNonTrattaICaratteriJollyDiChiCerca()
    {
        // "%" e "_" sono jolly per LIKE: senza escape, cercare "100%" tornerebbe quasi tutto.
        var found = await _repository.SearchAsync(
            new TransactionQuery { Text = "100%" },
            CancellationToken.None);

        Assert.Empty(found);
    }

    [Fact]
    public async Task LaCorrezioneManualeChiudeLaRevisione()
    {
        // La ricerca torna entita' non tracciate, apposta: per modificarne una la si ricarica.
        var id = (await _repository.SearchAsync(
            new TransactionQuery { Merchant = "Q8" },
            CancellationToken.None))[0].Id;

        var first = await _repository.GetAsync(id, CancellationToken.None);
        Assert.NotNull(first);

        first!.ApplyManualCorrection(
            PaymentDirection.Outgoing,
            TransactionKind.CardPayment,
            Money.Create(75m, "EUR"),
            "Q8 Autostrada",
            "Carburante");

        await _repository.SaveChangesAsync(CancellationToken.None);

        var reloaded = await _repository.GetAsync(id, CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.Equal(ProcessingStatus.Classified, reloaded!.Status);
        Assert.True(reloaded.ReviewedByHuman);
        Assert.Equal(75m, reloaded.Amount);
    }
}
