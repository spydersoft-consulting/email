namespace Spydersoft.Email.Client;

public sealed record EmailQuery
{
    public string? SenderClientId { get; init; }
    public EmailStatus? Status { get; init; }
    public string? Recipient { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public int Skip { get; init; }
    public int Limit { get; init; } = 50;
}
