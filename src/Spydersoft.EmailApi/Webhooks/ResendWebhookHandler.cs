using System.Globalization;
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

public sealed partial class ResendWebhookHandler(EmailDbContext db, TimeProvider time, ILogger<ResendWebhookHandler> logger)
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

        if (payload is null)
        {
            return WebhookResult.Invalid;
        }

        var type = payload["type"]?.GetValue<string>();
        var resendId = payload["data"]?["email_id"]?.GetValue<string>();
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
            LogUnknownMessage(type, resendId);
            return WebhookResult.UnknownMessage;
        }

        var occurredAt = DateTimeOffset.TryParse(payload["created_at"]?.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed) ? parsed : time.GetUtcNow();
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
                message.Error = payload["data"]?["bounce"]?["message"]?.ToString();
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        LogApplied(type, resendId);
        return WebhookResult.Applied;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Webhook {EventType} for unknown Resend id {ResendId}")]
    private partial void LogUnknownMessage(string eventType, string resendId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Webhook {EventType} applied to Resend id {ResendId}")]
    private partial void LogApplied(string eventType, string resendId);

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
