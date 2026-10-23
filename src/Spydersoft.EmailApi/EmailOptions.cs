namespace Spydersoft.EmailApi;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string SendingDomain { get; set; } = string.Empty;

    public string DefaultFrom { get; set; } = string.Empty;

    /// <summary>
    /// Tags outgoing mail with environment=<value> and makes the webhook handler accept only events carrying the same tag.
    /// Leave empty in production: it then handles every event that has no environment tag.
    /// </summary>
    public string? EnvironmentTag { get; set; }

    public long MaxMessageBytes { get; set; } = 40L * 1024 * 1024;
}
