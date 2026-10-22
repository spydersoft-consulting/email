using Spydersoft.Email.Client;

namespace Spydersoft.EmailApi.Services;

/// <param name="Result">The persisted send result.</param>
/// <param name="Existing">True when an idempotency key matched an earlier send.</param>
/// <param name="FailureStatusCode">The Resend status code when the send was rejected; null on success.</param>
/// <param name="FailureDetail">The Resend error body when the send was rejected.</param>
public sealed record SendOutcome(EmailSendResult Result, bool Existing, int? FailureStatusCode = null, string? FailureDetail = null);

public interface IEmailService
{
    Task<SendOutcome> SendAsync(EmailMessage message, SenderIdentity sender, CancellationToken cancellationToken = default);
}
