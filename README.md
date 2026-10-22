# Spydersoft Email

Platform email service for Spydersoft applications. Wraps [Resend](https://resend.com) so apps send transactional email through one place, with a shared sending identity, a queryable delivery log, and webhook-driven delivery status.

See [../plans/email/overview.md](../plans/email/overview.md) for the design and [../plans/email/implementation-plan.md](../plans/email/implementation-plan.md) for the roadmap.

**Status:** scaffold only — project structure, auth, health checks, client contract, CI and chart are in place; send/query/webhook endpoints, persistence and the Resend gateway are not yet implemented.

## Projects

- `Spydersoft.Email.Client` — `IEmailClient`, DTOs and DI helpers (NuGet)
- `Spydersoft.EmailApi` — ASP.NET Core 10 API: send, query, Resend webhook receiver
- `Spydersoft.Email.AppHost` — .NET Aspire local development host

## Local Development

```powershell
dotnet run --project src/Spydersoft.Email.AppHost
```

Requires Docker. Starts PostgreSQL and the API with `Resend__Mode=fake`.

## Consuming the Client

```csharp
builder.Services.AddSpydersoftEmailClient(builder.Configuration);
```

```json
{
  "EmailClient": {
    "BaseUrl": "https://email.example.com"
  }
}
```

## Codebase Conventions

- All C# classes sealed unless explicitly designed for inheritance
- Nullable reference types and implicit usings enabled globally
- NUnit + NSubstitute for testing
- Spydersoft.Platform.Hosting for telemetry and health checks
