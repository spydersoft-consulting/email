namespace Spydersoft.EmailApi.UnitTests;

[TestFixture]
public sealed class AuthorizationPoliciesTests
{
    [Test]
    public void ScopeNames_MatchIdentityServerScopes()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(AuthorizationPolicies.Send, Is.EqualTo("email:send"));
            Assert.That(AuthorizationPolicies.Read, Is.EqualTo("email:read"));
        }
    }
}
