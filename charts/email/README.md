# email chart

OCI chart for the `email-api` controller. Published from this repo (`ghcr.io/spydersoft-consulting/charts/email`), versioned alongside the container image.

The chart does not create Secrets, ConfigMaps or PostgreSQL. The composing repo (`platform-helm-config`) supplies `envFrom` and the in-namespace Postgres.

## Values

- `controllers.email-api.containers.main.image.tag` — email-api image tag.
- `controllers.email-api.containers.main.envFrom` — supplied by the caller.
- `route.email-api.hostnames` — per-environment hostname(s). The route is public (Resend webhooks).

## Secrets contract

- `ConnectionStrings__email` — PostgreSQL connection string
- `Resend__ApiKey` — Resend API key
- `Resend__WebhookSecret` — Resend webhook signing secret
