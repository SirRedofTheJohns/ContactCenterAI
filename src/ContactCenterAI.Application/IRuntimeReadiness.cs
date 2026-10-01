namespace ContactCenterAI.Application;

public sealed record RuntimeReadiness(bool IsReady, string Code);

public interface IRuntimeReadiness
{
    ValueTask<RuntimeReadiness> CheckAsync(CancellationToken cancellationToken);
}
