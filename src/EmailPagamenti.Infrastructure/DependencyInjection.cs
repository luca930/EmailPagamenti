using EmailPagamenti.Application.Abstractions;
using EmailPagamenti.Application.Options;
using EmailPagamenti.Application.Parsing;
using EmailPagamenti.Application.Pipeline;
using EmailPagamenti.Infrastructure.Email;
using EmailPagamenti.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EmailPagamenti.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registra pipeline, sorgente email e persistenza. Le opzioni sono validate all'avvio:
    /// meglio un errore immediato che un worker che gira a vuoto per ore.
    /// </summary>
    public static IServiceCollection AddEmailPagamenti(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<ImapOptions>()
            .Bind(configuration.GetSection(ImapOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<IngestionOptions>()
            .Bind(configuration.GetSection(IngestionOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<BankOptions>()
            .Bind(configuration.GetSection(BankOptions.SectionName));

        services.AddOptions<CategoryOptions>()
            .Bind(configuration.GetSection(CategoryOptions.SectionName));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IEmailSource, ImapEmailSource>();
        services.AddSingleton<ICategorizer, KeywordCategorizer>();
        services.AddSingleton<ITransactionExtractor, BankNotificationExtractor>();

        AddPersistence(services, configuration);

        services.AddScoped<ITransactionRepository, EfTransactionRepository>();
        services.AddScoped<EmailIngestionService>();

        return services;
    }

    /// <summary>
    /// Crea la cartella del file SQLite se manca. Senza, la prima esecuzione muore con un
    /// "unable to open database file" che non dice quale sia il vero problema.
    /// </summary>
    private static void EnsureSqliteDirectory(string connectionString)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.DataSource) || builder.DataSource == ":memory:")
        {
            return;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(builder.DataSource));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration.GetValue<string>("Database:Provider") ?? "Sqlite";
        var connectionString = configuration.GetConnectionString("Transactions")
            ?? throw new InvalidOperationException(
                "Manca la stringa di connessione 'Transactions'. Impostala via variabile d'ambiente "
                + "ConnectionStrings__Transactions oppure con dotnet user-secrets.");

        services.AddDbContext<TransactionsDbContext>(options =>
        {
            switch (provider.ToUpperInvariant())
            {
                case "POSTGRES":
                case "POSTGRESQL":
                    options.UseNpgsql(connectionString);
                    break;
                case "SQLITE":
                    EnsureSqliteDirectory(connectionString);
                    options.UseSqlite(connectionString);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Provider database non supportato: '{provider}'. Valori ammessi: Sqlite, Postgres.");
            }
        });
    }
}
