using Spydersoft.EmailApi.Webhooks;

namespace Spydersoft.EmailApi.UnitTests.Webhooks;

[TestFixture]
public sealed class SvixSignatureVerifierTests
{
    private static readonly string Secret = "whsec_" + Convert.ToBase64String("test-signing-key-bytes"u8.ToArray());
    private static readonly DateTimeOffset Now = new(2026, 6, 2, 14, 0, 0, TimeSpan.Zero);

    private static string Timestamp(DateTimeOffset at) => at.ToUnixTimeSeconds().ToString();

    [Test]
    public void Verify_ValidSignature_ReturnsTrue()
    {
        var ts = Timestamp(Now);
        var sig = SvixSignatureVerifier.Sign(Secret, "msg_1", ts, "{}");

        Assert.That(SvixSignatureVerifier.Verify(Secret, "msg_1", ts, sig, "{}", Now), Is.True);
    }

    [Test]
    public void Verify_AnyOfMultipleSignatures_ReturnsTrue()
    {
        var ts = Timestamp(Now);
        var sig = SvixSignatureVerifier.Sign(Secret, "msg_1", ts, "{}");

        Assert.That(SvixSignatureVerifier.Verify(Secret, "msg_1", ts, "v1,AAAA " + sig, "{}", Now), Is.True);
    }

    [Test]
    public void Verify_TamperedBody_ReturnsFalse()
    {
        var ts = Timestamp(Now);
        var sig = SvixSignatureVerifier.Sign(Secret, "msg_1", ts, "{}");

        Assert.That(SvixSignatureVerifier.Verify(Secret, "msg_1", ts, sig, "{\"x\":1}", Now), Is.False);
    }

    [Test]
    public void Verify_WrongSecret_ReturnsFalse()
    {
        var ts = Timestamp(Now);
        var sig = SvixSignatureVerifier.Sign(Secret, "msg_1", ts, "{}");
        var other = "whsec_" + Convert.ToBase64String("another-key"u8.ToArray());

        Assert.That(SvixSignatureVerifier.Verify(other, "msg_1", ts, sig, "{}", Now), Is.False);
    }

    [Test]
    public void Verify_ExpiredTimestamp_ReturnsFalse()
    {
        var ts = Timestamp(Now.AddMinutes(-10));
        var sig = SvixSignatureVerifier.Sign(Secret, "msg_1", ts, "{}");

        Assert.That(SvixSignatureVerifier.Verify(Secret, "msg_1", ts, sig, "{}", Now), Is.False);
    }

    [Test]
    public void Verify_EmptySecret_ReturnsFalse()
    {
        Assert.That(SvixSignatureVerifier.Verify(string.Empty, "msg_1", Timestamp(Now), "v1,AAAA", "{}", Now), Is.False);
    }
}
