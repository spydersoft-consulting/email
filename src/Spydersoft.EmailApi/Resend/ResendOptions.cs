namespace Spydersoft.EmailApi.Resend;

public sealed class ResendOptions
{
    public const string SectionName = "Resend";

    public string Mode { get; set; } = "live";

    public string ApiKey { get; set; } = string.Empty;

    public string WebhookSecret { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.resend.com/";
}
