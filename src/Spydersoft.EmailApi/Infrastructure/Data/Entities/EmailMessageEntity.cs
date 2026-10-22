using Spydersoft.Email.Client;

namespace Spydersoft.EmailApi.Infrastructure.Data.Entities;

public sealed class EmailMessageEntity
{
    public Guid Id { get; init; }
    public string? ResendId { get; set; }
    public string? IdempotencyKey { get; init; }

    public string? SenderClientId { get; init; }
    public string? SenderUserId { get; init; }

    public string FromAddress { get; init; } = default!;
    public List<string> To { get; init; } = [];
    public List<string> Cc { get; init; } = [];
    public List<string> Bcc { get; init; } = [];
    public string Subject { get; init; } = default!;
    public Dictionary<string, string>? Tags { get; init; }
    public List<AttachmentMeta> Attachments { get; init; } = [];

    public EmailStatus Status { get; set; }
    public string? Error { get; set; }

    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? LastEventAt { get; set; }
    public string? CorrelationId { get; init; }

    public List<EmailEventEntity> Events { get; init; } = [];
}
