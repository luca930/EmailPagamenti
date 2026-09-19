using EmailPagamenti.Application.Abstractions;
using EmailPagamenti.Application.Options;
using EmailPagamenti.Application.Parsing;
using EmailPagamenti.Application.Pipeline;
using EmailPagamenti.Infrastructure.Email;
using EmailPagamenti.Infrastructure.Persistence;
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

        services.AddOptions<ClassificationOptions>()
            .Bind(configuration.GetSection(ClassificationOptions.SectionName));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IEmailSource, ImapEmailSource>();
        services.AddSingleton<IPaymentExtractor, RuleBasedPaymentExtractor>();

        AddPersistence(services, configuration);

        services.AddScoped<IPaymentEmailRepository, EfPaymentEmailRepository>();
        services.AddScoped<EmailIngestionService>();

        return services;
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration.GetValue<string>("Database:Provider") ?? "Sqlite";
        var connectionString = configuration.GetConnectionString("Payments")
            ?? throw new InvalidOperationException(
                "Manca la stringa di connessione 'Payments'. Impostala via variabile d'ambiente "
                + "ConnectionStrings__Payments oppure con dotnet user-secrets.");

        services.AddDbContext<PaymentsDbContext>(options =>
        {
            switch (provider.ToUpperInvariant())
            {
                case "POSTGRES":
                case "POSTGRESQL":
                    options.UseNpgsql(connectionString);
                    break;
                case "SQLITE":
                    options.UseSqlite(connectionString);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Provider database non supportato: '{provider}'. Valori ammessi: Sqlite, Postgres.");
            }
        });
    }
}
