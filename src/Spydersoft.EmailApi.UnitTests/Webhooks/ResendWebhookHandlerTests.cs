using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Spydersoft.Email.Client;
using Spydersoft.EmailApi.Infrastructure.Data;
using Spydersoft.EmailApi.Infrastructure.Data.Entities;
using Spydersoft.EmailApi.Webhooks;

namespace Spydersoft.EmailApi.UnitTests.Webhooks;

[TestFixture]
public sealed class ResendWebhookHandlerTests
{
    private EmailDbContext _db = null!;
    private ResendWebhookHandler _handler = null!;
    private Guid _messageId;

    [SetUp]
    public async Task SetUp()
    {
        _db = new EmailDbContext(new DbContextOptionsBuilder<EmailDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _handler = CreateHandler(null);

        _messageId = Guid.NewGuid();
        _db.Messages.Add(new EmailMessageEntity
        {
            Id = _messageId,
            ResendId = "re_1",
            FromAddress = "a@mattgerega.net",
            Subject = "Hi",
            To = ["b@example.com"],
            Status = EmailStatus.Sent,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    private ResendWebhookHandler CreateHandler(string? environmentTag) => new(
        _db,
        Options.Create(new EmailOptions { EnvironmentTag = environmentTag }),
        TimeProvider.System,
        NullLogger<ResendWebhookHandler>.Instance);

    private static string TaggedPayload(string type, string environment, string emailId = "re_1") =>
        $"{{\"type\":\"{type}\",\"created_at\":\"2026-06-02T14:00:00Z\",\"data\":{{\"email_id\":\"{emailId}\",\"tags\":{{\"environment\":\"{environment}\"}}}}}}";

    private static string Payload(string type, string emailId = "re_1") =>
        $"{{\"type\":\"{type}\",\"created_at\":\"2026-06-02T14:00:00Z\",\"data\":{{\"email_id\":\"{emailId}\"}}}}";

    [Test]
    public async Task Delivered_AdvancesStatusAndStoresEvent()
    {
        var result = await _handler.HandleAsync("msg_1", Payload("email.delivered"), default);

        var message = await _db.Messages.SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(WebhookResult.Applied));
            Assert.That(message.Status, Is.EqualTo(EmailStatus.Delivered));
            Assert.That(message.LastEventAt, Is.Not.Null);
            Assert.That(_db.Events.Count(), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task DuplicateWebhookId_IsIgnored()
    {
        await _handler.HandleAsync("msg_1", Payload("email.delivered"), default);

        var result = await _handler.HandleAsync("msg_1", Payload("email.delivered"), default);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(WebhookResult.Duplicate));
            Assert.That(_db.Events.Count(), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task StatusNeverMovesBackward()
    {
        await _handler.HandleAsync("msg_1", Payload("email.delivered"), default);
        await _handler.HandleAsync("msg_2", Payload("email.delivery_delayed"), default);

        Assert.That((await _db.Messages.SingleAsync()).Status, Is.EqualTo(EmailStatus.Delivered));
    }

    [Test]
    public async Task BounceAfterDelivery_AdvancesToBounced()
    {
        await _handler.HandleAsync("msg_1", Payload("email.delivered"), default);
        await _handler.HandleAsync("msg_2", Payload("email.bounced"), default);

        Assert.That((await _db.Messages.SingleAsync()).Status, Is.EqualTo(EmailStatus.Bounced));
    }

    [Test]
    public async Task Opened_RecordsEventWithoutChangingStatus()
    {
        await _handler.HandleAsync("msg_1", Payload("email.opened"), default);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((await _db.Messages.SingleAsync()).Status, Is.EqualTo(EmailStatus.Sent));
            Assert.That(_db.Events.Count(), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task UnknownMessage_IsNotStored()
    {
        var result = await _handler.HandleAsync("msg_1", Payload("email.delivered", "re_other"), default);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(WebhookResult.UnknownMessage));
            Assert.That(_db.Events.Count(), Is.Zero);
        }
    }

    [Test]
    public async Task MalformedPayload_IsInvalid()
    {
        Assert.That(await _handler.HandleAsync("msg_1", "not json", default), Is.EqualTo(WebhookResult.Invalid));
    }

    [Test]
    public async Task TestDeployment_AppliesEventWithMatchingTag()
    {
        var result = await CreateHandler("test").HandleAsync("msg_1", TaggedPayload("email.delivered", "test"), default);

        Assert.That(result, Is.EqualTo(WebhookResult.Applied));
        Assert.That((await _db.Messages.SingleAsync()).Status, Is.EqualTo(EmailStatus.Delivered));
    }

    [Test]
    public async Task TestDeployment_IgnoresUntaggedEvent()
    {
        var result = await CreateHandler("test").HandleAsync("msg_1", Payload("email.delivered"), default);

        Assert.That(result, Is.EqualTo(WebhookResult.Ignored));
        Assert.That(_db.Events.Count(), Is.Zero);
    }

    [Test]
    public async Task ProductionDeployment_IgnoresTestTaggedEvent()
    {
        var result = await CreateHandler(null).HandleAsync("msg_1", TaggedPayload("email.delivered", "test"), default);

        Assert.That(result, Is.EqualTo(WebhookResult.Ignored));
        Assert.That((await _db.Messages.SingleAsync()).Status, Is.EqualTo(EmailStatus.Sent));
    }

    [Test]
    public async Task TagsAsNameValueList_AreRecognised()
    {
        const string payload = "{\"type\":\"email.delivered\",\"data\":{\"email_id\":\"re_1\",\"tags\":[{\"name\":\"environment\",\"value\":\"test\"}]}}";

        var result = await CreateHandler("test").HandleAsync("msg_1", payload, default);

        Assert.That(result, Is.EqualTo(WebhookResult.Applied));
    }
}
