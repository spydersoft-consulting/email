using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace Spydersoft.EmailApi.Resend;

public sealed class ResendGateway(HttpClient http) : IResendGateway
{
    public async Task<ResendSendResult> SendAsync(ResendSendRequest request, CancellationToken cancellationToken = default)
    {
        var body = new JsonObject
        {
            ["from"] = request.From,
            ["to"] = ToArray(request.To),
            ["subject"] = request.Subject,
        };

        if (request.Cc.Count > 0) body["cc"] = ToArray(request.Cc);
        if (request.Bcc.Count > 0) body["bcc"] = ToArray(request.Bcc);
        if (request.ReplyTo is not null) body["reply_to"] = request.ReplyTo;
        if (request.Html is not null) body["html"] = request.Html;
        if (request.Text is not null) body["text"] = request.Text;

        if (request.Tags is { Count: > 0 })
        {
            body["tags"] = new JsonArray(request.Tags
                .Select(t => (JsonNode)new JsonObject { ["name"] = t.Key, ["value"] = t.Value }).ToArray());
        }

        if (request.Attachments.Count > 0)
        {
            body["attachments"] = new JsonArray(request.Attachments
                .Select(a => (JsonNode)new JsonObject
                {
                    ["filename"] = a.Filename,
                    ["content"] = a.ContentBase64,
                    ["content_type"] = a.ContentType,
                }).ToArray());
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, "emails")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        if (request.IdempotencyKey is not null)
        {
            message.Headers.Add("Idempotency-Key", request.IdempotencyKey);
        }

        using var response = await http.SendAsync(message, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new ResendException((int)response.StatusCode, text);
        }

        var id = JsonNode.Parse(text)?["id"]?.GetValue<string>()
            ?? throw new ResendException((int)response.StatusCode, "Resend response did not include an id.");
        return new ResendSendResult(id);
    }

    public async Task<ResendMessageState?> GetAsync(string resendId, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync($"emails/{Uri.EscapeDataString(resendId)}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new ResendException((int)response.StatusCode, text);
        }

        var node = JsonNode.Parse(text);
        return new ResendMessageState(resendId, node?["last_event"]?.GetValue<string>() ?? string.Empty);
    }

    private static JsonArray ToArray(IEnumerable<string> values) =>
        new(values.Select(v => (JsonNode?)JsonValue.Create(v)).ToArray());
}
