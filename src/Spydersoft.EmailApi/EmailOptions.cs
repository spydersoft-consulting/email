namespace Spydersoft.EmailApi;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string SendingDomain { get; set; } = string.Empty;

    public string DefaultFrom { get; set; } = string.Empty;

    public long MaxMessageBytes { get; set; } = 40L * 1024 * 1024;
}
