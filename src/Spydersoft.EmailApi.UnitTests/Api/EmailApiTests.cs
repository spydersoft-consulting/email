using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Spydersoft.Email.Client;
using Spydersoft.EmailApi.Resend;
using Spydersoft.EmailApi.Webhooks;

namespace Spydersoft.EmailApi.UnitTests.Api;

[TestFixture]
public sealed class EmailApiTests
{
    private EmailApiFactory _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new EmailApiFactory();
        _client = _factory.CreateAuthenticatedClient("test-client", "email:send", "email:read");
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private static EmailMessage Message(string to = "a@example.com", string? idempotencyKey = null) => new()
    {
        To = [to],
        Subject = "Hello",
        Text = "Body",
        IdempotencyKey = idempotencyKey,
    };

    private async Task<EmailSendResult> SendAsync(EmailMessage message)
    {
        var response = await _client.PostAsJsonAsync("/api/emails", message);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
        return (await response.Content.ReadFromJsonAsync<EmailSendResult>())!;
    }

    private async Task<HttpResponseMessage> PostWebhookAsync(string webhookId, string type, string resendId, string? secret = null)
    {
        var body = $"{{\"type\":\"{type}\",\"created_at\":\"2026-06-02T14:00:00Z\",\"data\":{{\"email_id\":\"{resendId}\"}}}}";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = SvixSignatureVerifier.Sign(secret ?? EmailApiFactory.WebhookSecret, webhookId, timestamp, body);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/resend")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("svix-id", webhookId);
        request.Headers.Add("svix-timestamp", timestamp);
        request.Headers.Add("svix-signature", signature);
        return await _factory.CreateClient().SendAsync(request);
    }

    [Test]
    public async Task Livez_ReturnsOk()
    {
        var response = await _factory.CreateClient().GetAsync("/livez");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Send_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/emails", Message());

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Send_WithReadScopeOnly_ReturnsForbidden()
    {
        using var client = _factory.CreateAuthenticatedClient("reader", "email:read");

        var response = await client.PostAsJsonAsync("/api/emails", Message());

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Send_InvalidMessage_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/emails", new EmailMessage { To = [], Subject = "x", Text = "y" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Send_ThenGet_ReturnsStatusWithSender()
    {
        var sent = await SendAsync(Message());

        var view = await _client.GetFromJsonAsync<EmailStatusView>($"/api/emails/{sent.Id}");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(view!.Status, Is.EqualTo(EmailStatus.Sent));
            Assert.That(view.SenderClientId, Is.EqualTo("test-client"));
            Assert.That(view.FromAddress, Is.EqualTo("Test <noreply@example.com>"));
        }
    }

    [Test]
    public async Task Send_SameIdempotencyKey_ReturnsOkWithSameId()
    {
        var first = await SendAsync(Message(idempotencyKey: "k1"));

        var again = await _client.PostAsJsonAsync("/api/emails", Message(idempotencyKey: "k1"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(again.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That((await again.Content.ReadFromJsonAsync<EmailSendResult>())!.Id, Is.EqualTo(first.Id));
        }
    }

    [Test]
    public async Task Send_WhenResendRejects_ReturnsResendStatus()
    {
        var gateway = Substitute.For<IResendGateway>();
        gateway.SendAsync(Arg.Any<ResendSendRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(new ResendException(422, "invalid"));
        using var factory = _factory.WithWebHostBuilder(b => b.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton(gateway))));
        using var client = EmailApiFactory.Authenticate(factory.CreateClient(), "test-client", "email:send");

        var response = await client.PostAsJsonAsync("/api/emails", Message());

        Assert.That((int)response.StatusCode, Is.EqualTo(422));
    }

    [Test]
    public async Task Get_UnknownId_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/emails/{Guid.NewGuid()}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetEvents_UnknownId_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/emails/{Guid.NewGuid()}/events");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Webhook_Delivered_AdvancesStatusAndRecordsEvent()
    {
        var sent = await SendAsync(Message());

        var webhook = await PostWebhookAsync("msg_1", "email.delivered", sent.ResendId!);
        var view = await _client.GetFromJsonAsync<EmailStatusView>($"/api/emails/{sent.Id}");
        var events = await _client.GetFromJsonAsync<List<EmailEventView>>($"/api/emails/{sent.Id}/events");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(webhook.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(view!.Status, Is.EqualTo(EmailStatus.Delivered));
            Assert.That(events!.Select(e => e.EventType), Is.EqualTo(new[] { "email.delivered" }));
        }
    }

    [Test]
    public async Task Webhook_BadSignature_ReturnsUnauthorized()
    {
        var sent = await SendAsync(Message());

        var response = await PostWebhookAsync("msg_1", "email.delivered", sent.ResendId!, "whsec_" + Convert.ToBase64String("other"u8.ToArray()));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Webhook_MalformedBody_ReturnsBadRequest()
    {
        const string body = "not json";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/resend")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("svix-id", "msg_x");
        request.Headers.Add("svix-timestamp", timestamp);
        request.Headers.Add("svix-signature", SvixSignatureVerifier.Sign(EmailApiFactory.WebhookSecret, "msg_x", timestamp, body));

        var response = await _factory.CreateClient().SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Webhook_MissingHeaders_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient().PostAsync("/api/webhooks/resend", new StringContent("{}"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Search_FiltersByStatusRecipientAndSender()
    {
        var delivered = await SendAsync(Message("delivered@example.com"));
        await SendAsync(Message("other@example.com"));
        await PostWebhookAsync("msg_1", "email.delivered", delivered.ResendId!);

        var byStatus = await _client.GetFromJsonAsync<EmailPage>("/api/emails?status=Delivered");
        var byRecipient = await _client.GetFromJsonAsync<EmailPage>("/api/emails?recipient=other@example.com");
        var bySender = await _client.GetFromJsonAsync<EmailPage>("/api/emails?senderClientId=test-client&limit=1");
        var none = await _client.GetFromJsonAsync<EmailPage>("/api/emails?senderClientId=nobody");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(byStatus!.Items.Select(i => i.Id), Is.EqualTo(new[] { delivered.Id }));
            Assert.That(byRecipient!.Total, Is.EqualTo(1));
            Assert.That(bySender!.Total, Is.EqualTo(2));
            Assert.That(bySender.Items, Has.Count.EqualTo(1));
            Assert.That(none!.Total, Is.Zero);
        }
    }

    [Test]
    public async Task Search_FiltersByCreatedRange()
    {
        await SendAsync(Message());
        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddHours(-1).ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddHours(1).ToString("O"));
        var past = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddHours(-3).ToString("O"));

        var inRange = await _client.GetFromJsonAsync<EmailPage>($"/api/emails?from={from}&to={to}");
        var outOfRange = await _client.GetFromJsonAsync<EmailPage>($"/api/emails?to={past}");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(inRange!.Total, Is.EqualTo(1));
            Assert.That(outOfRange!.Total, Is.Zero);
        }
    }
}
