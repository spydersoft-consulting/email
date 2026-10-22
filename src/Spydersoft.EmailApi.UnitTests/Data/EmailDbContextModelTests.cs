using Microsoft.EntityFrameworkCore;
using Spydersoft.EmailApi.Infrastructure.Data;
using Spydersoft.EmailApi.Infrastructure.Data.Entities;

namespace Spydersoft.EmailApi.UnitTests.Data;

[TestFixture]
public sealed class EmailDbContextModelTests
{
    private static EmailDbContext Create() =>
        new(new DbContextOptionsBuilder<EmailDbContext>().UseNpgsql("Host=localhost").Options);

    [Test]
    public void IdempotencyKeyPerSender_IsUnique()
    {
        using var db = Create();
        var entity = db.Model.FindEntityType(typeof(EmailMessageEntity))!;

        var index = entity.GetIndexes().Single(i =>
            i.Properties.Select(p => p.Name).SequenceEqual([nameof(EmailMessageEntity.IdempotencyKey), nameof(EmailMessageEntity.SenderClientId)]));

        Assert.That(index.IsUnique, Is.True);
    }

    [Test]
    public void WebhookId_IsUnique()
    {
        using var db = Create();
        var entity = db.Model.FindEntityType(typeof(EmailEventEntity))!;

        var index = entity.GetIndexes().Single(i => i.Properties.Single().Name == nameof(EmailEventEntity.WebhookId));

        Assert.That(index.IsUnique, Is.True);
    }
}
