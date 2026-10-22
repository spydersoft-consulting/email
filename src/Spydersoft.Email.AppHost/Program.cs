var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres").WithPgAdmin();
var emailDb = postgres.AddDatabase("email");

var dashboardOtlp = builder.Configuration["DOTNET_DASHBOARD_OTLP_ENDPOINT_URL"]
    ?? "http://localhost:18889";

var api = builder.AddProject<Projects.Spydersoft_EmailApi>("email-api")
    .WithReference(emailDb)
    .WithEnvironment("Resend__Mode", "fake")
    .WithEnvironment("Resend__WebhookSecret", "whsec_bG9jYWwtZGV2LXdlYmhvb2stc2VjcmV0")
    .WithEnvironment("Email__SendingDomain", "mattgerega.net")
    .WithEnvironment("Email__DefaultFrom", "Spydersoft <local-noreply@mattgerega.net>")
    .WaitFor(postgres);

foreach (var (typeKey, endpointKey) in new[]
{
    ("Telemetry__Trace__Type",   "Telemetry__Trace__Otlp__Endpoint"),
    ("Telemetry__Metrics__Type", "Telemetry__Metrics__Otlp__Endpoint"),
    ("Telemetry__Log__Type",     "Telemetry__Log__Otlp__Endpoint"),
})
{
    api.WithEnvironment(typeKey, builder.Configuration[typeKey] ?? "otlp");
    api.WithEnvironment(endpointKey, builder.Configuration[endpointKey] ?? dashboardOtlp);
}

if (builder.Environment.EnvironmentName == "Testing")
{
    var testKey = builder.Configuration["Auth:TestKey"]
        ?? "jRv3YFPH/19t9t5CgsEFgAkykfW5bQhHmceMprLgzlQ=";

    api.WithEnvironment("DOTNET_ENVIRONMENT", "Testing")
       .WithEnvironment("Auth__TestKey", testKey)
       .WithEndpoint("http", e => e.Port = 5400);
}
else
{
    api.WithEnvironment("Auth__Authority", builder.Configuration["Auth:Authority"] ?? "https://auth.mattgerega.net")
       .WithEnvironment("Auth__Audience", builder.Configuration["Auth:Audience"] ?? "email-api");
}

await builder.Build().RunAsync();
