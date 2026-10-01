namespace ContactCenterAI.Domain;

[Flags]
public enum ActorRoles { None = 0, Customer = 1, Agent = 2, Supervisor = 4, KnowledgeEditor = 8, KnowledgeReviewer = 16, OperationsAdmin = 32 }

public sealed record Actor(Guid SessionId, string TenantId, Guid? PrincipalId, string? MemberRef, ActorRoles Roles, DateTimeOffset ExpiresAt);
public sealed record ConversationResource(Guid Id, string TenantId, Guid? PrincipalId, Guid? GuestSessionId, long Version, long Epoch);
public sealed record ActiveAssignment(Guid PrincipalId, Guid ConversationId, string TenantId, DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool Revoked);

public static class ResourceAccess
{
    public static bool CanAccess(Actor actor, ConversationResource resource, ActiveAssignment? assignment, DateTimeOffset now)
    {
        if (actor.ExpiresAt <= now || actor.TenantId != resource.TenantId) return false;
        if (actor.PrincipalId is null)
            return resource.PrincipalId is null && resource.GuestSessionId == actor.SessionId;
        if (actor.Roles.HasFlag(ActorRoles.Customer) && actor.MemberRef is not null && resource.PrincipalId == actor.PrincipalId) return true;
        return (actor.Roles & (ActorRoles.Agent | ActorRoles.Supervisor)) != 0 && assignment is not null &&
            !assignment.Revoked && assignment.PrincipalId == actor.PrincipalId && assignment.ConversationId == resource.Id &&
            assignment.TenantId == actor.TenantId && assignment.StartsAt <= now && now < assignment.EndsAt;
    }
}
