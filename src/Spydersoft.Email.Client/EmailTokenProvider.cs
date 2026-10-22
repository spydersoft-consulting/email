using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Spydersoft.Email.Client;

/// <summary>Acquires and caches a client-credentials access token.</summary>
internal sealed class EmailTokenProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<EmailClientOptions> options,
    TimeProvider time)
{
    internal const string HttpClientName = "email-token";

    private static readonly TimeSpan RefreshSkew = TimeSpan.FromSeconds(60);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _token;
    private DateTimeOffset _expiresAt;

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (_token is not null && time.GetUtcNow() < _expiresAt)
        {
            return _token;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_token is not null && time.GetUtcNow() < _expiresAt)
            {
                return _token;
            }

            var opts = options.Value;
            using var request = new HttpRequestMessage(HttpMethod.Post, opts.TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = opts.ClientId ?? string.Empty,
                    ["client_secret"] = opts.ClientSecret ?? string.Empty,
                    ["scope"] = opts.Scope,
                }),
            };

            using var response = await httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
                ?? throw new InvalidOperationException("Token endpoint returned an empty response.");

            _token = token.AccessToken;
            _expiresAt = time.GetUtcNow().AddSeconds(token.ExpiresIn) - RefreshSkew;
            return _token;
        }
        finally
        {
            _gate.Release();
        }
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
