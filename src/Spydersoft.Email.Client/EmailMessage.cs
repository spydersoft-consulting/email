namespace Spydersoft.Email.Client;

public sealed record EmailMessage(
    IReadOnlyList<string> To,
    string Subject,
    string? Html = null,
    string? Text = null,
    string? From = null,
    string? ReplyTo = null,
    string? IdempotencyKey = null);
