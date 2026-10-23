using System.Diagnostics;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Spydersoft.Email.Client;
using Spydersoft.EmailApi.Infrastructure.Data;
using Spydersoft.EmailApi.Infrastructure.Data.Entities;
using Spydersoft.EmailApi.Resend;

namespace Spydersoft.EmailApi.Services;

public sealed partial class EmailService(
    EmailDbContext db,
    IResendGateway gateway,
    IOptions<EmailOptions> options,
    TimeProvider time,
    ILogger<EmailService> logger) : IEmailService
{
    private readonly EmailOptions _options = options.Value;

    public async Task<SendOutcome> SendAsync(EmailMessage message, SenderIdentity sender, CancellationToken cancellationToken = default)
    {
        Validate(message);

        if (message.IdempotencyKey is not null)
        {
            var existing = await db.Messages.AsNoTracking().FirstOrDefaultAsync(
                m => m.IdempotencyKey == message.IdempotencyKey && m.SenderClientId == sender.ClientId, cancellationToken);
            if (existing is not null)
            {
                return new SendOutcome(ToResult(existing), Existing: true);
            }
        }

        var from = ResolveFrom(message.From);
        var entity = new EmailMessageEntity
        {
            Id = Guid.NewGuid(),
            IdempotencyKey = message.IdempotencyKey,
            SenderClientId = sender.ClientId,
            SenderUserId = sender.UserId,
            FromAddress = from,
            To = [.. message.To],
            Cc = [.. message.Cc],
            Bcc = [.. message.Bcc],
            Subject = message.Subject,
            Tags = message.Tags?.ToDictionary(t => t.Key, t => t.Value),
            Attachments = [.. message.Attachments.Select(a => new AttachmentMeta(a.Filename, a.ContentType, Base64Size(a.ContentBase64)))],
            CreatedAt = time.GetUtcNow(),
            CorrelationId = message.CorrelationId,
            Status = EmailStatus.Queued,
        };

        var request = new ResendSendRequest(
            from, message.To, message.Cc, message.Bcc, message.ReplyTo, message.Subject,
            message.Html, message.Text, message.Tags, message.Attachments, message.IdempotencyKey);

        int? failureStatus = null;
        string? failureDetail = null;
        var started = Stopwatch.GetTimestamp();
        try
        {
            var sent = await gateway.SendAsync(request, cancellationToken);
            entity.ResendId = sent.Id;
            entity.Status = EmailStatus.Sent;
            entity.SentAt = time.GetUtcNow();
        }
        catch (ResendException ex)
        {
            entity.Status = EmailStatus.Failed;
            entity.Error = ex.Detail;
            failureStatus = ex.StatusCode;
            failureDetail = ex.Detail;
        }

        db.Messages.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        LogSent(
            entity.Id, entity.ResendId, sender.ClientId, message.To.Count + message.Cc.Count + message.Bcc.Count,
            entity.Status, Stopwatch.GetElapsedTime(started).TotalMilliseconds);

        return new SendOutcome(ToResult(entity), Existing: false, failureStatus, failureDetail);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Email {EmailId} resend {ResendId} from client {ClientId} recipients {RecipientCount} status {Status} in {ElapsedMs}ms")]
    private partial void LogSent(Guid emailId, string? resendId, string? clientId, int recipientCount, EmailStatus status, double elapsedMs);

    private void Validate(EmailMessage message)
    {
        if (message.To.Count == 0)
        {
            throw new EmailValidationException("At least one recipient is required.");
        }

        if (string.IsNullOrWhiteSpace(message.Subject))
        {
            throw new EmailValidationException("Subject is required.");
        }

        if (string.IsNullOrWhiteSpace(message.Html) && string.IsNullOrWhiteSpace(message.Text))
        {
            throw new EmailValidationException("Either html or text is required.");
        }

        var size = (message.Html?.Length ?? 0) + (message.Text?.Length ?? 0)
            + message.Attachments.Sum(a => Base64Size(a.ContentBase64));
        if (size > _options.MaxMessageBytes)
        {
            throw new EmailValidationException("Message exceeds the maximum size.", tooLarge: true);
        }
    }

    private string ResolveFrom(string? requested)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            return _options.DefaultFrom;
        }

        MailAddress parsed;
        try
        {
            parsed = new MailAddress(requested);
        }
        catch (FormatException)
        {
            throw new EmailValidationException("From must be a valid email address.");
        }

        var address = $"{parsed.User}@{_options.SendingDomain}";
        return string.IsNullOrWhiteSpace(parsed.DisplayName) ? address : $"{parsed.DisplayName} <{address}>";
    }

    private static long Base64Size(string base64) => (long)base64.Length * 3 / 4;

    private static EmailSendResult ToResult(EmailMessageEntity e) => new(e.Id, e.ResendId, e.Status, e.CreatedAt);
}
