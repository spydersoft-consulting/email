namespace Spydersoft.Email.Client;

public interface IEmailClient
{
    Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
