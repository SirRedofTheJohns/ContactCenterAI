using ContactCenterAI.Application;
using ContactCenterAI.Infrastructure;
namespace ContactCenterAI.Api;
public sealed class DemoKnowledgeWorker(SqliteOperationalStore store,TimeProvider clock):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while(!ct.IsCancellationRequested)
        {
            try{await store.BuildKnowledgeIndexAsync(clock.GetUtcNow(),ct);}
            catch(Exception error)when(error is RequestRejected or HttpRequestException or OperationCanceledException or OperationalUnavailable){if(ct.IsCancellationRequested)return;}
            await Task.Delay(TimeSpan.FromSeconds(60),ct);
        }
    }
}
