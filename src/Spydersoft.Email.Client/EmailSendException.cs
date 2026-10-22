using System.Net;

namespace Spydersoft.Email.Client;

public sealed class EmailSendException(string message, HttpStatusCode? statusCode = null, string? detail = null, Exception? innerException = null)
    : Exception(message, innerException)
{
    public HttpStatusCode? StatusCode { get; } = statusCode;

    public string? Detail { get; } = detail;

    internal static async Task<EmailSendException> FromResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        return new EmailSendException($"Email service returned {(int)response.StatusCode}.", response.StatusCode, detail);
    }
}
