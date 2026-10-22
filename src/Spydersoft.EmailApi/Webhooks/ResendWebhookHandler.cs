using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Spydersoft.Email.Client;
using Spydersoft.EmailApi.Infrastructure.Data;
using Spydersoft.EmailApi.Infrastructure.Data.Entities;

namespace Spydersoft.EmailApi.Webhooks;

public enum WebhookResult
{
    Applied,
    Duplicate,
    UnknownMessage,
    Invalid,
}

public sealed class ResendWebhookHandler(EmailDbContext db, TimeProvider time, ILogger<ResendWebhookHandler> logger)
{
    public async Task<WebhookResult> HandleAsync(string webhookId, string body, CancellationToken cancellationToken)
    {
        JsonNode? payload;
        try
        {
            payload = JsonNode.Parse(body);
        }
        catch (System.Text.Json.JsonException)
        {
            return WebhookResult.Invalid;
        }

        var type = payload?["type"]?.GetValue<string>();
        var resendId = payload?["data"]?["email_id"]?.GetValue<string>();
        if (type is null || resendId is null)
        {
            return WebhookResult.Invalid;
        }

        if (await db.Events.AnyAsync(e => e.WebhookId == webhookId, cancellationToken))
        {
            return WebhookResult.Duplicate;
        }

        var message = await db.Messages.FirstOrDefaultAsync(m => m.ResendId == resendId, cancellationToken);
        if (message is null)
        {
            logger.LogWarning("Webhook {EventType} for unknown Resend id {ResendId}", type, resendId);
            return WebhookResult.UnknownMessage;
        }

        var occurredAt = DateTimeOffset.TryParse(payload?["created_at"]?.ToString(), out var parsed) ? parsed : time.GetUtcNow();
        db.Events.Add(new EmailEventEntity
        {
            MessageId = message.Id,
            ResendId = resendId,
            WebhookId = webhookId,
            EventType = type,
            OccurredAt = occurredAt,
            RawPayload = body,
        });

        message.LastEventAt = occurredAt;
        if (MapStatus(type) is { } next && Rank(next) > Rank(message.Status))
        {
            message.Status = next;
            if (next is EmailStatus.Bounced or EmailStatus.Complained)
            {
                message.Error = payload?["data"]?["bounce"]?["message"]?.ToString();
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Webhook {EventType} applied to Resend id {ResendId}", type, resendId);
        return WebhookResult.Applied;
    }

    internal static EmailStatus? MapStatus(string eventType) => eventType switch
    {
        "email.sent" => EmailStatus.Sent,
        "email.delivered" => EmailStatus.Delivered,
        "email.delivery_delayed" => EmailStatus.DeliveryDelayed,
        "email.bounced" => EmailStatus.Bounced,
        "email.complained" => EmailStatus.Complained,
        _ => null,
    };

    private static int Rank(EmailStatus status) => status switch
    {
        EmailStatus.Queued => 0,
        EmailStatus.Failed => 0,
        EmailStatus.Sent => 1,
        EmailStatus.DeliveryDelayed => 2,
        EmailStatus.Delivered => 3,
        _ => 4,
    };
}
