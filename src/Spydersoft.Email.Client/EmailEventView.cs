namespace Spydersoft.Email.Client;

public sealed record EmailEventView(string EventType, DateTimeOffset OccurredAt);
