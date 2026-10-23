using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Spydersoft.Email.Client;
using Spydersoft.EmailApi.Infrastructure.Data;
using Spydersoft.EmailApi.Infrastructure.Data.Entities;

namespace Spydersoft.EmailApi.Controllers;

[ApiController]
[Route("api/emails")]
[Authorize(AuthorizationPolicies.Read)]
public sealed class EmailQueryController(EmailDbContext db) : ControllerBase
{
    private const int MaxLimit = 500;

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EmailStatusView>> Get(Guid id, CancellationToken cancellationToken)
    {
        var entity = await db.Messages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        return entity is null ? NotFound() : ToView(entity);
    }

    [HttpGet("{id:guid}/events")]
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
    public async Task<ActionResult<EmailPage>> Search([FromQuery] EmailQuery filter, CancellationToken cancellationToken)
    {
        var skip = Math.Max(filter.Skip, 0);
        var limit = Math.Clamp(filter.Limit, 1, MaxLimit);

        var query = db.Messages.AsNoTracking().AsQueryable();
        if (filter.SenderClientId is not null) query = query.Where(m => m.SenderClientId == filter.SenderClientId);
        if (filter.Status is not null) query = query.Where(m => m.Status == filter.Status);
        if (filter.From is not null) query = query.Where(m => m.CreatedAt >= filter.From);
        if (filter.To is not null) query = query.Where(m => m.CreatedAt <= filter.To);
        if (filter.Recipient is not null)
        {
            query = query.Where(m => m.To.Contains(filter.Recipient) || m.Cc.Contains(filter.Recipient) || m.Bcc.Contains(filter.Recipient));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(m => m.CreatedAt).Skip(skip).Take(limit).ToListAsync(cancellationToken);
        return new EmailPage([.. items.Select(ToView)], total, skip, limit);
    }

    private static EmailStatusView ToView(EmailMessageEntity e) => new(
        e.Id, e.ResendId, e.Status, e.Error, e.FromAddress, e.To, e.Subject,
        e.SenderClientId, e.SenderUserId, e.CreatedAt, e.SentAt, e.LastEventAt);
}
