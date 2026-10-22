using Spydersoft.Email.Client;

namespace Spydersoft.EmailApi.Resend;

public sealed record ResendSendRequest(
    string From,
    IReadOnlyList<string> To,
    IReadOnlyList<string> Cc,
    IReadOnlyList<string> Bcc,
    string? ReplyTo,
    string Subject,
    string? Html,
    string? Text,
    IReadOnlyDictionary<string, string>? Tags,
    IReadOnlyList<EmailAttachment> Attachments,
    string? IdempotencyKey);

public sealed record ResendSendResult(string Id);

public sealed record ResendMessageState(string Id, string LastEvent);

public sealed class ResendException(int statusCode, string detail)
    : Exception($"Resend returned {statusCode}.")
{
    public int StatusCode { get; } = statusCode;

    public string Detail { get; } = detail;
}

public interface IResendGateway
{
    /// <summary>Sends one message. Throws <see cref="ResendException"/> when Resend rejects it.</summary>
    Task<ResendSendResult> SendAsync(ResendSendRequest request, CancellationToken cancellationToken = default);

    /// <summary>Current state of a message by Resend id; null if unknown.</summary>
    Task<ResendMessageState?> GetAsync(string resendId, CancellationToken cancellationToken = default);
}
