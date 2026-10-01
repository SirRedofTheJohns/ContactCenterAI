using ContactCenterAI.Domain;
namespace ContactCenterAI.Application;

public sealed record SourceReservation(string ReservationId, string MemberRef, string PropertyName,
    DateTimeOffset CheckInUtc, ReservationStatus Status, string Version);
public sealed record ReservationView(string ReservationId, string PropertyName, DateTimeOffset CheckInUtc,
    string Status, string Version, CancellationEligibility Cancellation);
public interface IReservationSource
{
    Task<IReadOnlyList<SourceReservation>> ListAsync(string serverMemberRef, CancellationToken ct);
}
