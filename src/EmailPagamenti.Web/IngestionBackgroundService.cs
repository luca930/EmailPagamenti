using EmailPagamenti.Application.Options;
using EmailPagamenti.Application.Pipeline;
using Microsoft.Extensions.Options;

namespace EmailPagamenti.Web;

/// <summary>
/// Acquisizione periodica dentro la stessa applicazione web: su un server di casa un
/// contenitore solo e' piu' semplice da gestire di due. Un errore su una passata viene
/// registrato e non abbatte il servizio.
/// </summary>
public sealed class IngestionBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<IngestionOptions> _options;
    private readonly ILogger<IngestionBackgroundService> _logger;

    public IngestionBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<IngestionOptions> options,
        ILogger<IngestionBackgroundService> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.CurrentValue.Enabled)
        {
            _logger.LogInformation("Acquisizione automatica disattivata: si legge solo su richiesta.");
            return;
        }

        using var timer = new PeriodicTimer(_options.CurrentValue.PollInterval);

        do
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var ingestion = scope.ServiceProvider.GetRequiredService<EmailIngestionService>();
                await ingestion.RunAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Rete assente, credenziali scadute, server lento: si riprova al giro dopo.
                _logger.LogError(
                    ex,
                    "Passata di acquisizione fallita, si riprova tra {Interval}.",
                    _options.CurrentValue.PollInterval);
            }
        }
        while (await SafeWaitAsync(timer, stoppingToken).ConfigureAwait(false));
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
