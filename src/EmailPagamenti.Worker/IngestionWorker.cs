using EmailPagamenti.Application.Options;
using EmailPagamenti.Application.Pipeline;
using Microsoft.Extensions.Options;

namespace EmailPagamenti.Worker;

/// <summary>
/// Esegue una passata di acquisizione a intervalli regolari. Un errore su una passata
/// viene registrato e non abbatte il servizio: alla successiva si riprova.
/// </summary>
public sealed class IngestionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<IngestionOptions> _options;
    private readonly ILogger<IngestionWorker> _logger;

    public IngestionWorker(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<IngestionOptions> options,
        ILogger<IngestionWorker> logger)
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
                _logger.LogError(ex, "Passata di acquisizione fallita, si riprova tra {Interval}.", _options.CurrentValue.PollInterval);
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
