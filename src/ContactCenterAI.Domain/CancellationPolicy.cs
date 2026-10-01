namespace ContactCenterAI.Domain;

public enum ReservationStatus { Confirmed, Cancelled }
public sealed record CancellationEligibility(bool Eligible, string ReasonCode, string PolicyVersion, long PenaltyMinorUnits, string Currency);
public static class CancellationPolicy
{
    public const string Version = "CP-001:v1";
    public static CancellationEligibility Evaluate(ReservationStatus status, DateTimeOffset checkInUtc, DateTimeOffset nowUtc)
    {
        var code = status != ReservationStatus.Confirmed ? "RESERVATION_NOT_CONFIRMED" :
            checkInUtc - nowUtc < TimeSpan.FromHours(72) ? "OUTSIDE_FREE_CANCELLATION_WINDOW" : "FREE_CANCELLATION_WINDOW";
        return new(code == "FREE_CANCELLATION_WINDOW", code, Version, 0, "USD");
    }
}
