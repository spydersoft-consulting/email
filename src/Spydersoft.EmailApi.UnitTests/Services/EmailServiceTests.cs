using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Spydersoft.Email.Client;
using Spydersoft.EmailApi.Infrastructure.Data;
using Spydersoft.EmailApi.Resend;
using Spydersoft.EmailApi.Services;

namespace Spydersoft.EmailApi.UnitTests.Services;

[TestFixture]
public sealed class EmailServiceTests
{
    private static readonly SenderIdentity Sender = new("identity-server", null);

    private EmailDbContext _db = null!;
    private FakeResendGateway _gateway = null!;
    private EmailService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _db = new EmailDbContext(new DbContextOptionsBuilder<EmailDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _gateway = new FakeResendGateway();
        _service = Create(_gateway);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    private EmailService Create(IResendGateway gateway) => new(
        _db,
        gateway,
        Options.Create(new EmailOptions { SendingDomain = "mattgerega.net", DefaultFrom = "Spydersoft <test-noreply@mattgerega.net>" }),
        TimeProvider.System,
        NullLogger<EmailService>.Instance);

    private static EmailMessage Message(Action<EmailMessageBuilder>? configure = null)
    {
        var b = new EmailMessageBuilder();
        configure?.Invoke(b);
        return new EmailMessage { To = b.To, Subject = b.Subject, Text = b.Text, Html = b.Html, From = b.From, IdempotencyKey = b.IdempotencyKey };
    }

    private sealed class EmailMessageBuilder
    {
        public IReadOnlyList<string> To { get; set; } = ["a@example.com"];
        public string Subject { get; set; } = "Hi";
        public string? Text { get; set; } = "Hello";
        public string? Html { get; set; }
        public string? From { get; set; }
        public string? IdempotencyKey { get; set; }
    }

    [Test]
    public async Task SendAsync_PersistsSentMessage()
    {
        var outcome = await _service.SendAsync(Message(), Sender);

        var row = await _db.Messages.SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(outcome.Result.Status, Is.EqualTo(EmailStatus.Sent));
            Assert.That(row.ResendId, Is.EqualTo(outcome.Result.ResendId));
            Assert.That(row.SenderClientId, Is.EqualTo("identity-server"));
            Assert.That(_gateway.Sent, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public async Task SendAsync_NoFrom_UsesDefaultFrom()
    {
        await _service.SendAsync(Message(), Sender);

        Assert.That(_gateway.Sent.Single().From, Is.EqualTo("Spydersoft <test-noreply@mattgerega.net>"));
    }

    [Test]
    public async Task SendAsync_ForcesSendingDomain()
    {
        await _service.SendAsync(Message(b => b.From = "Billing <billing@evil.example>"), Sender);

        Assert.That(_gateway.Sent.Single().From, Is.EqualTo("Billing <billing@mattgerega.net>"));
    }

    [Test]
    public void SendAsync_NoRecipients_Throws()
    {
        Assert.ThrowsAsync<EmailValidationException>(() => _service.SendAsync(Message(b => b.To = []), Sender));
    }

    [Test]
    public void SendAsync_NoBody_Throws()
    {
        Assert.ThrowsAsync<EmailValidationException>(() => _service.SendAsync(Message(b => b.Text = null), Sender));
    }

    [Test]
    public void SendAsync_InvalidFrom_Throws()
    {
        Assert.ThrowsAsync<EmailValidationException>(() => _service.SendAsync(Message(b => b.From = "not an address"), Sender));
    }

    [Test]
    public void SendAsync_OversizeMessage_ThrowsTooLarge()
    {
        var service = new EmailService(
            _db, _gateway, Options.Create(new EmailOptions { SendingDomain = "mattgerega.net", MaxMessageBytes = 5 }),
            TimeProvider.System, NullLogger<EmailService>.Instance);

        var ex = Assert.ThrowsAsync<EmailValidationException>(() => service.SendAsync(Message(b => b.Text = "123456789"), Sender));

        Assert.That(ex!.TooLarge, Is.True);
    }

    [Test]
    public async Task SendAsync_SameIdempotencyKey_DoesNotSendTwice()
    {
        var first = await _service.SendAsync(Message(b => b.IdempotencyKey = "k1"), Sender);
        var second = await _service.SendAsync(Message(b => b.IdempotencyKey = "k1"), Sender);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(second.Existing, Is.True);
            Assert.That(second.Result.Id, Is.EqualTo(first.Result.Id));
            Assert.That(_gateway.Sent, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public async Task SendAsync_SameKeyDifferentClient_SendsAgain()
    {
        await _service.SendAsync(Message(b => b.IdempotencyKey = "k1"), Sender);
        await _service.SendAsync(Message(b => b.IdempotencyKey = "k1"), new SenderIdentity("other", null));

        Assert.That(_gateway.Sent, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task SendAsync_ResendRejects_PersistsFailedRow()
    {
        var gateway = Substitute.For<IResendGateway>();
        gateway.SendAsync(Arg.Any<ResendSendRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ResendException(422, "invalid"));

        var outcome = await Create(gateway).SendAsync(Message(), Sender);

        var row = await _db.Messages.SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(outcome.FailureStatusCode, Is.EqualTo(422));
            Assert.That(row.Status, Is.EqualTo(EmailStatus.Failed));
            Assert.That(row.Error, Is.EqualTo("invalid"));
        }
    }

    [Test]
    public async Task SendAsync_NoEnvironmentTag_LeavesTagsUntouched()
    {
        await _service.SendAsync(Message(), Sender);

        Assert.That(_gateway.Sent.Single().Tags, Is.Null);
    }

    [Test]
    public async Task SendAsync_WithEnvironmentTag_TagsRequestAndRow()
    {
        var service = new EmailService(
            _db, _gateway,
            Options.Create(new EmailOptions { SendingDomain = "mattgerega.net", DefaultFrom = "a@mattgerega.net", EnvironmentTag = "test" }),
            TimeProvider.System, NullLogger<EmailService>.Instance);

        await service.SendAsync(Message(), Sender);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_gateway.Sent.Single().Tags!["environment"], Is.EqualTo("test"));
            Assert.That((await _db.Messages.SingleAsync()).Tags!["environment"], Is.EqualTo("test"));
        }
    }
}
