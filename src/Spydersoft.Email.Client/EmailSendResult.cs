namespace Spydersoft.Email.Client;

public sealed record EmailSendResult(
    Guid Id,
    string? ResendId,
    EmailStatus Status,
    DateTimeOffset CreatedAt);
