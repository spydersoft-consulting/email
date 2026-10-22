namespace Spydersoft.Email.Client;

public sealed record EmailStatusView(
    Guid Id,
    string? ResendId,
    EmailStatus Status,
    string? Error,
    string FromAddress,
    IReadOnlyList<string> To,
    string Subject,
    string? SenderClientId,
    string? SenderUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SentAt,
    DateTimeOffset? LastEventAt);
