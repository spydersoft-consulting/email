using System.Collections.Concurrent;

namespace Spydersoft.EmailApi.Resend;

public sealed class FakeResendGateway : IResendGateway
{
    private readonly ConcurrentQueue<ResendSendRequest> _sent = new();

    public IReadOnlyCollection<ResendSendRequest> Sent => _sent;

    public Task<ResendSendResult> SendAsync(ResendSendRequest request, CancellationToken cancellationToken = default)
    {
        _sent.Enqueue(request);
        return Task.FromResult(new ResendSendResult($"re_fake_{Guid.NewGuid():N}"));
    }

    public Task<ResendMessageState?> GetAsync(string resendId, CancellationToken cancellationToken = default) =>
        Task.FromResult<ResendMessageState?>(new ResendMessageState(resendId, "email.sent"));
}
