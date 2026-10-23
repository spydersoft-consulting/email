using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Options;

namespace Spydersoft.Email.Client;

internal sealed class HttpEmailClient(HttpClient http, IOptions<EmailClientOptions> options) : IEmailClient
{
    public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (message.From is null && options.Value.DefaultFrom is { } defaultFrom)
        {
            message = message with { From = defaultFrom };
        }

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync("/api/emails", message, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new EmailSendException("Email service is unreachable.", ex.StatusCode, innerException: ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw await EmailSendException.FromResponseAsync(response, cancellationToken);
            }

            return await response.Content.ReadFromJsonAsync<EmailSendResult>(cancellationToken)
                ?? throw new EmailSendException("Email service returned an empty response.", response.StatusCode);
        }
    }

    public async Task<EmailStatusView?> GetStatusAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync($"/api/emails/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EmailStatusView>(cancellationToken);
    }

    public async Task<IReadOnlyList<EmailEventView>> GetEventsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var events = await http.GetFromJsonAsync<List<EmailEventView>>($"/api/emails/{id}/events", cancellationToken);
        return events ?? [];
    }

    public async Task<EmailPage> SearchAsync(EmailQuery query, CancellationToken cancellationToken = default)
    {
        var page = await http.GetFromJsonAsync<EmailPage>($"/api/emails{BuildQueryString(query)}", cancellationToken);
        return page ?? new EmailPage([], 0, query.Skip, query.Limit);
    }

    internal static string BuildQueryString(EmailQuery query)
    {
        var sb = new StringBuilder();

        void Add(string key, string? value)
        {
            if (value is null)
            {
                return;
            }

            sb.Append(sb.Length == 0 ? '?' : '&').Append(key).Append('=').Append(Uri.EscapeDataString(value));
        }

        Add("senderClientId", query.SenderClientId);
        Add("status", query.Status?.ToString());
        Add("recipient", query.Recipient);
        Add("from", query.From?.ToString("O"));
        Add("to", query.To?.ToString("O"));
        Add("skip", query.Skip.ToString());
        Add("limit", query.Limit.ToString());
        return sb.ToString();
    }
}
