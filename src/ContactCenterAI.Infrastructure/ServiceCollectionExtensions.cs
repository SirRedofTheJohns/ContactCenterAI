using ContactCenterAI.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ContactCenterAI.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddContactCenterInfrastructure(this IServiceCollection services)
    {
        var password = Environment.GetEnvironmentVariable("CCAI_OPERATIONAL_DB_PASSWORD");
        if (string.IsNullOrWhiteSpace(password))
        {
            services.TryAddSingleton<IRuntimeReadiness, UnconfiguredOperationalReadiness>();
            services.TryAddSingleton<IOperationalStore, UnconfiguredOperationalStore>();
        }
        else
        {
            services.AddSingleton(new SqlOperationalStore(password));
            services.TryAddSingleton<IRuntimeReadiness>(provider => provider.GetRequiredService<SqlOperationalStore>());
            services.TryAddSingleton<IOperationalStore>(provider => provider.GetRequiredService<SqlOperationalStore>());
        }
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ConversationIngress>();
        return services;
    }
}

internal sealed class UnconfiguredOperationalStore : IOperationalStore
{
    private static Task<T> Missing<T>() => Task.FromException<T>(new OperationalUnavailable());
    public Task<ContactCenterAI.Domain.Actor> CreateGuestAsync(DateTimeOffset now, CancellationToken ct) => Missing<ContactCenterAI.Domain.Actor>();
    public Task<ContactCenterAI.Domain.Actor> LoginAsync(string issuer, string subject, ContactCenterAI.Domain.ActorRoles roles, Guid? old, DateTimeOffset now, CancellationToken ct) => Missing<ContactCenterAI.Domain.Actor>();
    public Task<ContactCenterAI.Domain.Actor?> FindSessionAsync(Guid id, DateTimeOffset now, CancellationToken ct) => Missing<ContactCenterAI.Domain.Actor?>();
    public Task RevokeSessionAsync(Guid id, DateTimeOffset now, CancellationToken ct) => Task.FromException(new OperationalUnavailable());
    public Task<ConversationReceipt> CreateConversationAsync(ContactCenterAI.Domain.Actor actor, string language, string key, string hash, DateTimeOffset now, CancellationToken ct) => Missing<ConversationReceipt>();
    public Task<ConversationSnapshot?> ReadConversationAsync(ContactCenterAI.Domain.Actor actor, Guid id, DateTimeOffset now, CancellationToken ct) => Missing<ConversationSnapshot?>();
    public Task<MessageReceipt> SubmitMessageAsync(ContactCenterAI.Domain.Actor actor, Guid id, Guid client, string text, string hash, long version, DateTimeOffset now, CancellationToken ct) => Missing<MessageReceipt>();
}

internal sealed class UnconfiguredOperationalReadiness : IRuntimeReadiness
{
    public ValueTask<RuntimeReadiness> CheckAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new RuntimeReadiness(false, "OPERATIONAL_STORE_NOT_CONFIGURED"));
    }
}
