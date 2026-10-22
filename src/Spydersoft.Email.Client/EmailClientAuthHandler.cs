using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Spydersoft.Email.Client;

/// <summary>
/// Attaches a bearer token: a cached client-credentials token when a token endpoint is configured,
/// otherwise the JWT of the inbound request.
/// </summary>
internal sealed class EmailClientAuthHandler(
    EmailTokenProvider tokens,
    IOptions<EmailClientOptions> options,
    IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(options.Value.TokenEndpoint))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokens.GetTokenAsync(cancellationToken));
        }
        else if (httpContextAccessor.HttpContext?.Request.Headers.Authorization is { Count: > 0 } inbound
                 && AuthenticationHeaderValue.TryParse(inbound.ToString(), out var forwarded))
        {
            request.Headers.Authorization = forwarded;
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
