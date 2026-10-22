using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Spydersoft.Email.Client.UnitTests;

[TestFixture]
public sealed class HttpEmailClientTests
{
    private static (HttpEmailClient Client, StubHttpMessageHandler Handler) Create(
        Func<HttpRequestMessage, HttpResponseMessage> respond, EmailClientOptions? options = null)
    {
        var handler = new StubHttpMessageHandler(respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://email.test/") };
        return (new HttpEmailClient(http, Options.Create(options ?? new EmailClientOptions())), handler);
    }

    [Test]
    public async Task SendAsync_AppliesDefaultFromWhenMissing()
    {
        var result = new EmailSendResult(Guid.NewGuid(), null, EmailStatus.Sent, DateTimeOffset.UtcNow);
        string? body = null;
        var (client, _) = Create(
            r =>
            {
                body = r.Content!.ReadAsStringAsync().Result;
                return new HttpResponseMessage(HttpStatusCode.Accepted) { Content = JsonContent.Create(result) };
            },
            new EmailClientOptions { DefaultFrom = "Spydersoft <noreply@example.com>" });

        await client.SendAsync(Message);

        Assert.That(System.Text.Json.JsonDocument.Parse(body!).RootElement.GetProperty("from").GetString(),
            Is.EqualTo("Spydersoft <noreply@example.com>"));
    }

    private static readonly EmailMessage Message = new() { To = ["a@example.com"], Subject = "Hi", Text = "Hello" };

    [Test]
    public async Task SendAsync_PostsMessageAndReturnsResult()
    {
        var expected = new EmailSendResult(Guid.NewGuid(), "re_1", EmailStatus.Sent, DateTimeOffset.UtcNow);
        var (client, handler) = Create(_ => new HttpResponseMessage(HttpStatusCode.Accepted) { Content = JsonContent.Create(expected) });

        var result = await client.SendAsync(Message);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(expected));
            Assert.That(handler.Requests[0].Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(handler.Requests[0].RequestUri!.AbsolutePath, Is.EqualTo("/api/emails"));
        });
    }

    [Test]
    public void SendAsync_NonSuccess_ThrowsWithStatusCode()
    {
        var (client, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("bad") });

        var ex = Assert.ThrowsAsync<EmailSendException>(() => client.SendAsync(Message));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(ex.Detail, Is.EqualTo("bad"));
        });
    }

    [Test]
    public async Task GetStatusAsync_NotFound_ReturnsNull()
    {
        var (client, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        Assert.That(await client.GetStatusAsync(Guid.NewGuid()), Is.Null);
    }

    [Test]
    public async Task GetEventsAsync_ReturnsEvents()
    {
        var events = new[] { new EmailEventView("email.delivered", DateTimeOffset.UtcNow) };
        var (client, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(events) });

        var result = await client.GetEventsAsync(Guid.NewGuid());

        Assert.That(result.Select(e => e.EventType), Is.EqualTo(new[] { "email.delivered" }));
    }

    [Test]
    public async Task SearchAsync_SendsFiltersAsQueryString()
    {
        var page = new EmailPage([], 0, 5, 10);
        var (client, handler) = Create(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(page) });

        var result = await client.SearchAsync(new EmailQuery { SenderClientId = "identity-server", Status = EmailStatus.Delivered, Skip = 5, Limit = 10 });

        Assert.Multiple(() =>
        {
            Assert.That(result.Skip, Is.EqualTo(5));
            Assert.That(handler.Requests[0].RequestUri!.Query,
                Is.EqualTo("?senderClientId=identity-server&status=Delivered&skip=5&limit=10"));
        });
    }
}
