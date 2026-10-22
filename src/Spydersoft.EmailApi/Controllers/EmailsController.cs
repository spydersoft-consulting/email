using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Spydersoft.Email.Client;
using Spydersoft.EmailApi.Infrastructure.Data;
using Spydersoft.EmailApi.Infrastructure.Data.Entities;
using Spydersoft.EmailApi.Services;

namespace Spydersoft.EmailApi.Controllers;

[ApiController]
[Route("api/emails")]
public sealed class EmailsController(IEmailService emails, EmailDbContext db) : ControllerBase
{
    private const int MaxLimit = 500;

    [HttpPost]
    [Authorize(AuthorizationPolicies.Send)]
    public async Task<IActionResult> Send([FromBody] EmailMessage message, CancellationToken cancellationToken)
    {
        var sender = new SenderIdentity(
            User.FindFirstValue("client_id"),
            User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier));

        SendOutcome outcome;
        try
        {
            outcome = await emails.SendAsync(message, sender, cancellationToken);
        }
        catch (EmailValidationException ex)
        {
            return Problem(ex.Message, statusCode: ex.TooLarge ? StatusCodes.Status413PayloadTooLarge : StatusCodes.Status400BadRequest);
        }

        if (outcome.FailureStatusCode is { } failure)
        {
            return Problem(outcome.FailureDetail, statusCode: failure);
        }

        return outcome.Existing ? Ok(outcome.Result) : Accepted(outcome.Result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(AuthorizationPolicies.Read)]
    public async Task<ActionResult<EmailStatusView>> Get(Guid id, CancellationToken cancellationToken)
    {
        var entity = await db.Messages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        return entity is null ? NotFound() : ToView(entity);
    }

    [HttpGet("{id:guid}/events")]
    [Authorize(AuthorizationPolicies.Read)]
    public async Task<ActionResult<IReadOnlyList<EmailEventView>>> GetEvents(Guid id, CancellationToken cancellationToken)
    {
        if (!await db.Messages.AnyAsync(m => m.Id == id, cancellationToken))
        {
            return NotFound();
        }

        var events = await db.Events.AsNoTracking()
            .Where(e => e.MessageId == id)
            .OrderBy(e => e.OccurredAt)
            .Select(e => new EmailEventView(e.EventType, e.OccurredAt))
            .ToListAsync(cancellationToken);
        return events;
    }

    [HttpGet]
    [Authorize(AuthorizationPolicies.Read)]
    public async Task<ActionResult<EmailPage>> Search(
        [FromQuery] string? senderClientId,
        [FromQuery] EmailStatus? status,
        [FromQuery] string? recipient,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int skip = 0,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        skip = Math.Max(skip, 0);
        limit = Math.Clamp(limit, 1, MaxLimit);

        var query = db.Messages.AsNoTracking().AsQueryable();
        if (senderClientId is not null) query = query.Where(m => m.SenderClientId == senderClientId);
        if (status is not null) query = query.Where(m => m.Status == status);
        if (from is not null) query = query.Where(m => m.CreatedAt >= from);
        if (to is not null) query = query.Where(m => m.CreatedAt <= to);
        if (recipient is not null)
        {
            query = query.Where(m => m.To.Contains(recipient) || m.Cc.Contains(recipient) || m.Bcc.Contains(recipient));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(m => m.CreatedAt).Skip(skip).Take(limit).ToListAsync(cancellationToken);
        return new EmailPage([.. items.Select(ToView)], total, skip, limit);
    }

    private static EmailStatusView ToView(EmailMessageEntity e) => new(
        e.Id, e.ResendId, e.Status, e.Error, e.FromAddress, e.To, e.Subject,
        e.SenderClientId, e.SenderUserId, e.CreatedAt, e.SentAt, e.LastEventAt);
}
