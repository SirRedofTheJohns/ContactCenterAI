using ContactCenterAI.Application;
namespace ContactCenterAI.Api;

internal sealed class DemoAssistantWorker(AssistantProcessor assistant, IHandoffStore handoff, IContactCenterAdapter adapter, TimeProvider clock) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                for (var count=0;count<20 && await assistant.RunOnceAsync(stoppingToken);count++) { }
                var request=await handoff.ClaimHandoffAsync(clock.GetUtcNow(),stoppingToken);
                if(request is not null)
                {
                    using var deadline=CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);deadline.CancelAfter(TimeSpan.FromSeconds(8));
                    var acknowledgment=await adapter.RequestHandoffAsync(request,deadline.Token);
                    await handoff.ApplyAcknowledgmentAsync(acknowledgment,clock.GetUtcNow(),stoppingToken);
                }
            }
            catch(OperationalUnavailable) { }
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) { break; }
            catch(Exception error) when(error is RequestRejected or HttpRequestException or OperationCanceledException) { }
            await Task.Delay(TimeSpan.FromSeconds(1),stoppingToken);
        }
    }
}
