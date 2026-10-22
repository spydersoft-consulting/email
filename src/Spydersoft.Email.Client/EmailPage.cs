namespace Spydersoft.Email.Client;

public sealed record EmailPage(IReadOnlyList<EmailStatusView> Items, int Total, int Skip, int Limit);
