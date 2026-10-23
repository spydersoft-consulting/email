using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Spydersoft.EmailApi.Infrastructure.Data;

namespace Spydersoft.EmailApi.UnitTests.Api;

internal sealed class EmailApiFactory : WebApplicationFactory<Program>
{
    public const string WebhookSecret = "whsec_dGVzdC13ZWJob29rLXNlY3JldA==";

    private static readonly string TestKey = Convert.ToBase64String(new byte[32].Select((_, i) => (byte)(i + 1)).ToArray());

    private readonly string _databaseName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Auth:TestKey", TestKey);
        builder.UseSetting("Resend:Mode", "fake");
        builder.UseSetting("Resend:WebhookSecret", WebhookSecret);
        builder.UseSetting("Email:SendingDomain", "example.com");
        builder.UseSetting("Email:DefaultFrom", "Test <noreply@example.com>");
        builder.UseSetting("Telemetry:Trace:Type", "none");
        builder.UseSetting("Telemetry:Metrics:Type", "none");
        builder.UseSetting("Telemetry:Log:Type", "none");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<EmailDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<EmailDbContext>>();
            services.AddDbContext<EmailDbContext>(o => o.UseInMemoryDatabase(_databaseName));
        });
    }

    public HttpClient CreateAuthenticatedClient(string clientId, params string[] scopes) =>
        Authenticate(CreateClient(), clientId, scopes);

    public static HttpClient Authenticate(HttpClient client, string clientId, params string[] scopes)
    {
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(clientId, scopes));
        return client;
    }

    public static string CreateToken(string clientId, params string[] scopes)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim("client_id", clientId)]),
            Claims = new Dictionary<string, object> { ["scope"] = scopes },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Convert.FromBase64String(TestKey)), SecurityAlgorithms.HmacSha256),
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
