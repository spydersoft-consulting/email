namespace Spydersoft.Email.Client;

public sealed record EmailMessage
{
    public string? From { get; init; }
    public required IReadOnlyList<string> To { get; init; }
    public IReadOnlyList<string> Cc { get; init; } = [];
    public IReadOnlyList<string> Bcc { get; init; } = [];
    public string? ReplyTo { get; init; }
    public required string Subject { get; init; }
    public string? Html { get; init; }
    public string? Text { get; init; }
    public IReadOnlyDictionary<string, string>? Tags { get; init; }
    public IReadOnlyList<EmailAttachment> Attachments { get; init; } = [];
    public string? IdempotencyKey { get; init; }
    public string? CorrelationId { get; init; }
}
