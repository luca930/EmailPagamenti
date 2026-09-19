using EmailPagamenti.Infrastructure;
using EmailPagamenti.Infrastructure.Persistence;
using EmailPagamenti.Worker;
using Microsoft.EntityFrameworkCore;

// I segreti (password della casella, stringa di connessione) arrivano da user-secrets in
// sviluppo e da variabili d'ambiente in esecuzione: appsettings.json resta pulito e versionabile.
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddEmailPagamenti(builder.Configuration);
builder.Services.AddHostedService<IngestionWorker>();

var host = builder.Build();

await PrepareDatabaseAsync(host).ConfigureAwait(false);
await host.RunAsync().ConfigureAwait(false);

static async Task PrepareDatabaseAsync(IHost host)
{
    using var scope = host.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();

    // Finche' non esiste una migrazione (dotnet ef migrations add Iniziale) si crea lo schema
    // direttamente, cosi' il progetto parte appena clonato. In produzione servono le migrazioni.
    if (db.Database.GetMigrations().Any())
    {
        await db.Database.MigrateAsync().ConfigureAwait(false);
    }
    else
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning("Nessuna migrazione trovata: lo schema viene creato con EnsureCreated.");
        await db.Database.EnsureCreatedAsync().ConfigureAwait(false);
    }
}
