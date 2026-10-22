namespace Spydersoft.EmailApi.Infrastructure.Data.Entities;

public sealed class EmailEventEntity
{
    public long Id { get; init; }
    public Guid MessageId { get; init; }
    public string ResendId { get; init; } = default!;
    public string WebhookId { get; init; } = default!;
    public string EventType { get; init; } = default!;
    public DateTimeOffset OccurredAt { get; init; }
    public string RawPayload { get; init; } = default!;
}
