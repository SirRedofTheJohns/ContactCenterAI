using ContactCenterAI.Application;
using ContactCenterAI.Infrastructure;

namespace ContactCenterAI.Channels;

public sealed class ChannelRuntime(ChannelStore store, PublicResponder responder,
    IReadOnlyDictionary<string, ProviderClient> providers, IContactCenterAdapter handoff, TimeProvider clock)
{
    public async Task<bool> ProcessOneAsync(CancellationToken stopping)
    {
        var job = store.Claim(clock.GetUtcNow()); if (job is null) return false;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stopping);
        deadline.CancelAfter(TimeSpan.FromSeconds(8));
        PreparedOutput output;
        try
        {
            output = job.Text == "/human" ? HandoffNotice(job.Route.Language) : await responder.AnswerAsync(job, deadline.Token);
            if (output.Reply.ReasonCode == "HANDOFF_REQUESTED")
            {
                var request = store.RequestHandoff(job.Route.Key);
                store.Acknowledge(await handoff.RequestHandoffAsync(request, deadline.Token));
            }
        }
        catch (Exception error) when (error is RequestRejected or OperationalUnavailable or OperationCanceledException or HttpRequestException)
        {
            if (stopping.IsCancellationRequested) return false;
            output = new(new(job.Route.Language == "en" ? "The demo could not answer now. Please try later." : "La demo no pudo responder ahora. Intenta más tarde.", "CHANNEL_UNAVAILABLE", []));
        }
        store.Complete(job, output, clock.GetUtcNow()); return true;
    }
    private static PreparedOutput HandoffNotice(string language) => new(new(language == "en"
        ? "I requested the simulated human queue. The bot pauses; no real provider agent is connected."
        : "Solicité la cola humana simulada. El bot queda en pausa; no hay un agente real conectado.", "HANDOFF_REQUESTED", []));

    public async Task<bool> SendOneAsync(CancellationToken ct)
    {
        var output = store.NextOutput(clock.GetUtcNow()); if (output is null) return false;
        bool valid;
        try { valid = await responder.StillValidAsync(output.Output, ct); }
        catch (Exception error) when (error is RequestRejected or OperationalUnavailable or OperationCanceledException) { valid = false; }
        if (!valid || !providers.TryGetValue(output.Route.Channel, out var provider))
        { store.FinishSend(output.Id, "Suppressed", null, clock.GetUtcNow()); return true; }
        if (!store.BeginSend(output, clock.GetUtcNow())) return true;
        var result = await provider.SendAsync(output, clock.GetUtcNow(), ct);
        store.FinishSend(output.Id, result.Status, result.ProviderId, clock.GetUtcNow().AddSeconds(result.RetryAfterSeconds));
        return true;
    }
    public async Task WorkAsync(CancellationToken stopping)
    {
        var cleanupAt = DateTimeOffset.MinValue;
        while (!stopping.IsCancellationRequested)
        {
            if (clock.GetUtcNow() >= cleanupAt) { store.Cleanup(clock.GetUtcNow()); cleanupAt = clock.GetUtcNow().AddHours(1); }
            if (!await ProcessOneAsync(stopping)) await Task.Delay(250, stopping);
        }
    }
    public async Task DispatchAsync(CancellationToken stopping)
    {
        while (!stopping.IsCancellationRequested)
        { var sent = await SendOneAsync(stopping); await Task.Delay(sent ? 1000 : 250, stopping); }
    }
    public async Task PollAsync(ProviderClient telegram, CancellationToken stopping)
    {
        try { await telegram.CheckTelegramAsync(stopping); }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested) { return; }
        catch (Exception) { Console.WriteLine("TELEGRAM_READINESS_FAILED: transport stopped; review local configuration."); return; }
        var failures = 0;
        while (!stopping.IsCancellationRequested)
        {
            try
            {
                var batch = await telegram.PollAsync(store.ReadOffset(telegram.Options.EndpointId), clock, stopping);
                store.AcceptTelegram(batch, telegram.Options, clock.GetUtcNow()); failures = 0;
                if (batch.Count == 0) await Task.Delay(250, stopping);
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested) { return; }
            catch (Exception)
            {
                if (++failures >= 3) { Console.WriteLine("TELEGRAM_POLL_FAILED: transport stopped after three consecutive failures."); return; }
                await Task.Delay(TimeSpan.FromSeconds(failures * 2), stopping);
            }
        }
    }
}
