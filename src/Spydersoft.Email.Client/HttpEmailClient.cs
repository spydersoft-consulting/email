using System.Net.Http.Json;

namespace Spydersoft.Email.Client;

internal sealed class HttpEmailClient(HttpClient http) : IEmailClient
{
    public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync("api/emails", message, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new EmailSendException("Email service is unreachable.", ex.StatusCode, ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new EmailSendException($"Email service returned {(int)response.StatusCode}.", response.StatusCode);
            }

            return await response.Content.ReadFromJsonAsync<EmailSendResult>(cancellationToken)
                ?? throw new EmailSendException("Email service returned an empty response.", response.StatusCode);
        }
    }
}
