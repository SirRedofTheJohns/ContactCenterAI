using ContactCenterAI.Application;
namespace ContactCenterAI.Api;

internal sealed class DemoCommandWorker(CommandDispatcher dispatcher) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { for (var count = 0; count < 20 && await dispatcher.RunOnceAsync(stoppingToken); count++) { } }
            catch (OperationalUnavailable) { /* Durable state remains authoritative; next bounded tick retries storage. */ }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
