using System.Net;
using System.Text.Json.Nodes;
using Spydersoft.EmailApi.Resend;

namespace Spydersoft.EmailApi.UnitTests.Resend;

[TestFixture]
public sealed class ResendGatewayTests
{
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        }
    }

    private static ResendSendRequest Request(string? idempotencyKey = null) => new(
        "Spydersoft <noreply@mattgerega.net>", ["a@example.com"], [], [], null, "Hi", null, "Hello",
        new Dictionary<string, string> { ["app"] = "identity" }, [], idempotencyKey);

    private static ResendGateway Create(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.test/") });

    [Test]
    public async Task SendAsync_PostsPayloadAndReturnsId()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{\"id\":\"re_123\"}");

        var result = await Create(handler).SendAsync(Request("key-1"));

        var json = JsonNode.Parse(handler.RequestBody!)!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Id, Is.EqualTo("re_123"));
            Assert.That(handler.Request!.RequestUri!.AbsolutePath, Is.EqualTo("/emails"));
            Assert.That(handler.Request.Headers.GetValues("Idempotency-Key").Single(), Is.EqualTo("key-1"));
            Assert.That(json["to"]![0]!.GetValue<string>(), Is.EqualTo("a@example.com"));
            Assert.That(json["tags"]![0]!["name"]!.GetValue<string>(), Is.EqualTo("app"));
        }
    }

    [Test]
    public void SendAsync_ErrorResponse_ThrowsResendException()
    {
        var handler = new StubHandler(HttpStatusCode.UnprocessableEntity, "{\"message\":\"bad\"}");

        var ex = Assert.ThrowsAsync<ResendException>(() => Create(handler).SendAsync(Request()));

        Assert.That(ex!.StatusCode, Is.EqualTo(422));
    }

    [Test]
    public async Task GetAsync_NotFound_ReturnsNull()
    {
        var result = await Create(new StubHandler(HttpStatusCode.NotFound, "{}")).GetAsync("re_x");

        Assert.That(result, Is.Null);
    }
}
