using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Spydersoft.Email.Client;

public static class EmailServiceCollectionExtensions
{
    public static IServiceCollection AddSpydersoftEmailClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<EmailClientOptions>()
            .Bind(configuration.GetSection(EmailClientOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "EmailClientOptions.BaseUrl is required.")
            .ValidateOnStart();

        services.AddHttpContextAccessor();
        services.AddSingleton(TimeProvider.System);
        services.AddHttpClient(EmailTokenProvider.HttpClientName);
        services.AddSingleton<EmailTokenProvider>();
        services.AddTransient<EmailClientAuthHandler>();

        services.AddHttpClient<IEmailClient, HttpEmailClient>("email", (sp, http) =>
            {
                var opts = sp.GetRequiredService<IOptions<EmailClientOptions>>().Value;
                http.BaseAddress = new Uri(opts.BaseUrl);
            })
            .AddHttpMessageHandler<EmailClientAuthHandler>();

        return services;
    }
}
