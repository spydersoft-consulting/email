namespace Spydersoft.Email.Client;

public sealed class EmailClientOptions
{
    public const string SectionName = "EmailClient";

    public string BaseUrl { get; set; } = string.Empty;
}
