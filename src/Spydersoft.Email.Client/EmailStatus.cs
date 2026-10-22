using System.Text.Json.Serialization;

namespace Spydersoft.Email.Client;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EmailStatus
{
    Queued,
    Sent,
    Delivered,
    DeliveryDelayed,
    Bounced,
    Complained,
    Failed,
}
