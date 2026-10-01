namespace ContactCenterAI.Application;

// Routing metadata never grants a principal, role or member binding.
public sealed record ChannelText(string Channel, string EndpointId, string EventId, string SenderId,
    DateTimeOffset ReceivedAtUtc, DateTimeOffset OccurredAtUtc, string Text);
public sealed record ChannelReply(string Text, string ReasonCode, IReadOnlyList<CitationRef> Citations);
