using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Spydersoft.Email.Client.UnitTests;

[TestFixture]
public sealed class EmailClientAuthHandlerTests
{
    private sealed class TestTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static (EmailTokenProvider Provider, StubHttpMessageHandler Token) CreateProvider(EmailClientOptions options, TimeProvider time)
    {
        var tokenHandler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"access_token\":\"tok-" + Guid.NewGuid() + "\",\"expires_in\":3600}"),
        });
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(EmailTokenProvider.HttpClientName).Returns(_ => new HttpClient(tokenHandler, disposeHandler: false));
        return (new EmailTokenProvider(factory, Options.Create(options), time), tokenHandler);
    }

    private static HttpClient CreateClient(EmailClientOptions options, EmailTokenProvider provider, IHttpContextAccessor accessor, StubHttpMessageHandler inner)
    {
        var handler = new EmailClientAuthHandler(provider, Options.Create(options), accessor) { InnerHandler = inner };
        return new HttpClient(handler) { BaseAddress = new Uri("https://email.test/") };
    }

    private static readonly EmailClientOptions ServerToServer = new()
    {
        BaseUrl = "https://email.test",
        TokenEndpoint = "https://auth.test/connect/token",
        ClientId = "identity-server",
        ClientSecret = "secret",
    };

    [Test]
    public async Task TokenProvider_CachesTokenUntilExpiry()
    {
        var time = new TestTime(DateTimeOffset.UtcNow);
        var (provider, tokenHandler) = CreateProvider(ServerToServer, time);

        var first = await provider.GetTokenAsync(default);
        var second = await provider.GetTokenAsync(default);
        time.Now = time.Now.AddHours(2);
        var third = await provider.GetTokenAsync(default);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(second, Is.EqualTo(first));
            Assert.That(third, Is.Not.EqualTo(first));
            Assert.That(tokenHandler.Requests, Has.Count.EqualTo(2));
        }
    }

    [Test]
    public async Task Handler_ServerToServer_AttachesClientCredentialsToken()
    {
        var (provider, _) = CreateProvider(ServerToServer, TimeProvider.System);
        var inner = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(ServerToServer, provider, Substitute.For<IHttpContextAccessor>(), inner);

        await client.GetAsync("api/emails");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(inner.Requests[0].Headers.Authorization!.Scheme, Is.EqualTo("Bearer"));
            Assert.That(inner.Requests[0].Headers.Authorization!.Parameter, Does.StartWith("tok-"));
        }
    }

    [Test]
    public async Task Handler_UserContext_ForwardsInboundBearerToken()
    {
        var options = new EmailClientOptions { BaseUrl = "https://email.test" };
        var (provider, _) = CreateProvider(options, TimeProvider.System);
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer user-jwt";
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);
        var inner = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(options, provider, accessor, inner);

        await client.GetAsync("api/emails");

        Assert.That(inner.Requests[0].Headers.Authorization, Is.EqualTo(new AuthenticationHeaderValue("Bearer", "user-jwt")));
    }

    [Test]
    public async Task Handler_UserContext_NoInboundRequest_SendsUnauthenticated()
    {
        var options = new EmailClientOptions { BaseUrl = "https://email.test" };
        var (provider, _) = CreateProvider(options, TimeProvider.System);
        var inner = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(options, provider, Substitute.For<IHttpContextAccessor>(), inner);

        await client.GetAsync("api/emails");

        Assert.That(inner.Requests[0].Headers.Authorization, Is.Null);
    }
}
