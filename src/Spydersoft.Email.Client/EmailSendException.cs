using System.Net;

namespace Spydersoft.Email.Client;

public sealed class EmailSendException(string message, HttpStatusCode? statusCode = null, Exception? innerException = null)
    : Exception(message, innerException)
{
    public HttpStatusCode? StatusCode { get; } = statusCode;
}
