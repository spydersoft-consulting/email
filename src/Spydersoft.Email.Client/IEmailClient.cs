namespace Spydersoft.Email.Client;

public interface IEmailClient
{
    /// <summary>Sends one email. Throws <see cref="EmailSendException"/> on a non-success response.</summary>
    Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);

    /// <summary>Status of a message by service id; null if unknown.</summary>
    Task<EmailStatusView?> GetStatusAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Delivery-event history for a message, oldest first.</summary>
    Task<IReadOnlyList<EmailEventView>> GetEventsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>One page of the email log matching the filters.</summary>
    Task<EmailPage> SearchAsync(EmailQuery query, CancellationToken cancellationToken = default);
}
