using System.Security.Cryptography;
using System.Text.Json;
using ContactCenterAI.Application;
using ContactCenterAI.Channels;
using ContactCenterAI.Infrastructure;

try
{
    string Env(string name) => Environment.GetEnvironmentVariable("CCAI_CHANNEL_" + name) ?? "";
    bool Enabled(string name) => Env(name) == "true";
    EndpointOptions? Configure(string channel)
    {
        var prefix = channel == "telegram" ? "TELEGRAM_" : "META_";
        if (!Enabled(prefix + "ENABLED")) return null;
        var recipients = Env(prefix + "RECIPIENTS").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
        var options = new EndpointOptions(channel, Env(prefix + "ENDPOINT_ID"), recipients, Env(prefix + "ACCESS_TOKEN"),
            Env(prefix + "APP_SECRET"), Env(prefix + "VERIFY_TOKEN"), Env(prefix + "API_VERSION"), Enabled(prefix + "TEST_RESOURCES_CONFIRMED"));
        options.Validate(); return options;
    }
    var directory = Path.Combine(Directory.GetCurrentDirectory(), ".local", "channels");
    Directory.CreateDirectory(directory);
    using var processLock = new FileStream(Path.Combine(directory, "host.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    var keyPath = Path.Combine(directory, "ledger.key");
    if (!File.Exists(keyPath)) File.WriteAllBytes(keyPath, RandomNumberGenerator.GetBytes(32));
    var store = new ChannelStore(Path.Combine(directory, "channels.db"), File.ReadAllBytes(keyPath));
    if (args.SequenceEqual(new[] { "--status" })) { Console.WriteLine(JsonSerializer.Serialize(store.Status())); return; }
    if (args.SequenceEqual(new[] { "--inspect-telegram" }))
    {
        var options = new EndpointOptions("telegram", Env("TELEGRAM_ENDPOINT_ID"), new HashSet<string> { "1" }, Env("TELEGRAM_ACCESS_TOKEN"));
        using var inspector = new ProviderClient(options);
        using var inspection = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        await inspector.CheckTelegramAsync(inspection.Token);
        var batch = await inspector.PollAsync(0, TimeProvider.System, inspection.Token);
        var ids = batch.Where(update => update.Text is not null).Select(update => update.Text!.SenderId).Distinct().ToArray();
        Console.WriteLine("Bot identity matched. No checkpoint advanced and no reply sent.");
        Console.WriteLine(ids.Length == 0 ? "No private text received. Send /start to your bot and run inspection again." : "Private-chat IDs (local only): " + string.Join(", ", ids));
        return;
    }
    if (args.Length != 0) throw new RequestRejected(400, "CHANNEL_ARGUMENTS_REJECTED");
    var telegram = Configure("telegram"); var meta = Configure("whatsapp");
    if ((telegram is not null || meta is not null) && !Enabled("STORAGE_RESTRICTED")) throw new RequestRejected(503, "CHANNEL_STORAGE_RESTRICTION_REQUIRED");
    var mode = Env("AI_MODE"); if (mode == "") mode = "simulated";
    if (mode is not ("simulated" or "local-llm")) throw new RequestRejected(503, "CHANNEL_AI_MODE_REJECTED");
    using var intentClient = OllamaIntentProvider.CreateClient();
    using var embedding = mode == "local-llm" ? new OllamaEmbeddingProvider() : null;
    using var reranker = mode == "local-llm" ? new OllamaKnowledgeReranker() : null;
    var knowledge = new SqliteOperationalStore(Path.Combine(directory, "channels.db"), embedding, reranker);
    if (mode == "local-llm")
    {
        using var preparation = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        _ = await knowledge.BuildKnowledgeIndexAsync(DateTimeOffset.UtcNow, preparation.Token);
    }
    IIntentProvider model = mode == "local-llm" ? new OllamaIntentProvider(intentClient) : new SimulatedIntentProvider();
    using var tgClient = telegram is null ? null : new ProviderClient(telegram);
    using var metaClient = meta is null ? null : new ProviderClient(meta, diagnostic: Console.WriteLine);
    var providers = new Dictionary<string, ProviderClient>();
    if (tgClient is not null) providers.Add("telegram", tgClient);
    if (metaClient is not null) providers.Add("whatsapp", metaClient);
    var clock = TimeProvider.System;
    var runtime = new ChannelRuntime(store, new PublicResponder(knowledge, model, clock), providers, new LocalContactCenterMock(), clock);
    await using var host = ChannelHost.Build(store, meta);
    using var stopping = new CancellationTokenSource();
    host.Lifetime.ApplicationStopping.Register(stopping.Cancel);
    await host.StartAsync(stopping.Token);
    Console.WriteLine("ChannelHost: loopback 7454; AI=" + mode + "; Telegram=" + (telegram is null ? "disabled" : "configured") + "; WhatsApp=" + (meta is null ? "disabled" : "configured"));
    Console.WriteLine("Configured does not mean provider-validated. Ctrl+C stops this host; product data is unchanged.");
    async Task Observe(Task task)
    {
        try { await task; }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
        catch (Exception) { stopping.Cancel(); throw new RequestRejected(503, "CHANNEL_WORKER_STOPPED"); }
    }
    var tasks = new List<Task> { host.WaitForShutdownAsync(stopping.Token), Observe(runtime.WorkAsync(stopping.Token)), Observe(runtime.DispatchAsync(stopping.Token)) };
    if (tgClient is not null) tasks.Add(Observe(runtime.PollAsync(tgClient, stopping.Token)));
    await Task.WhenAll(tasks);
}
catch (OperationCanceledException) { }
catch (RequestRejected error) { Console.Error.WriteLine(error.Code); Environment.ExitCode = 1; }
catch (Exception) { Console.Error.WriteLine("CHANNEL_START_OR_STORAGE_FAILED: no credentials or messages exported."); Environment.ExitCode = 1; }
