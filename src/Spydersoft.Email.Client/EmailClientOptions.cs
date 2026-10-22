namespace Spydersoft.Email.Client;

public sealed class EmailClientOptions
{
    public const string SectionName = "EmailClient";

    /// <summary>Base URL of the email API. Required.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// OAuth2 token endpoint for client-credentials. When null the ambient request JWT is forwarded instead.
    /// </summary>
    public string? TokenEndpoint { get; set; }

    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    public string Scope { get; set; } = "email:send";

    /// <summary>Applied client-side when a message omits From.</summary>
    public string? DefaultFrom { get; set; }
}
