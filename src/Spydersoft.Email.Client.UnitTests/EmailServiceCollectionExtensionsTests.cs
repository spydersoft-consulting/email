using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Spydersoft.Email.Client.UnitTests;

[TestFixture]
public sealed class EmailServiceCollectionExtensionsTests
{
    [Test]
    public void AddSpydersoftEmailClient_RegistersEmailClient()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["EmailClient:BaseUrl"] = "https://email.example.com" })
            .Build();

        var provider = new ServiceCollection()
            .AddSpydersoftEmailClient(config)
            .BuildServiceProvider();

        Assert.That(provider.GetRequiredService<IEmailClient>(), Is.Not.Null);
    }
}
