# IdentityServer Configuration — Per-Env Runbook

The email service depends on two OAuth2 scopes (`email:send`, `email:read`) on an `email-api`
API resource, plus client grants. Each environment (test, stage, production) is configured
independently.

| Env | How it is configured today |
| --- | --- |
| test | Terraform: `ops-automation/terraform/identity` (`email-api` resource and both scopes). Client grants are still added by hand or in `clients.tf`. |
| stage, production | IdentityServer admin UI, following this runbook. |

---

## Per-Environment Admin URLs

| Env | Admin UI | Discovery endpoint |
| --- | --- | --- |
| test | `https://auth.mattgerega.net` | `https://auth.mattgerega.net/.well-known/openid-configuration` |
| stage | `https://auth.mattgerega.org` | `https://auth.mattgerega.org/.well-known/openid-configuration` |
| production | `https://auth.mattgerega.com` | `https://auth.mattgerega.com/.well-known/openid-configuration` |

Field names below are the conceptual names; match them to whatever the current UI calls them.

---

## What to Configure

### 1. API resource

| Name | Display Name | Scopes |
| --- | --- | --- |
| `email-api` | `Email API` | `email:send`, `email:read` |

The API validates the audience `email-api` (`Auth:Audience`) against the env's authority.

### 2. Scopes

| Field | `email:send` | `email:read` |
| --- | --- | --- |
| Display Name | `Email Service Send` | `Email Service Read` |
| Grants | `POST /api/emails` | `GET /api/emails`, `GET /api/emails/{id}`, `GET /api/emails/{id}/events` |
| Required | no | no |
| Emphasize | no | no |
| Show in Discovery | yes | yes |

### 3. Client grants

| Client ID | Type | Allowed scopes | Why |
| --- | --- | --- | --- |
| `identity-server` | Machine (client_credentials) | `email:send` | First consumer: confirm-email and reset-password |
| Any client that queries delivery status | Machine or interactive | `email:read` | Status lookups |

If a client already exists, add the scope to its allowed-scopes list instead of creating a new
client. Client secrets go in the consumer's secret store, never in this document.

---

## Verify

```bash
curl -s https://auth.mattgerega.net/.well-known/openid-configuration \
  | jq '.scopes_supported'
```

Both `"email:send"` and `"email:read"` should be present.

Smoke test the machine client:

```bash
curl -s -X POST https://auth.mattgerega.net/connect/token \
  -d grant_type=client_credentials \
  -d client_id=identity-server \
  -d client_secret=<from-vault> \
  -d scope=email:send \
  | jq .
```

Expect a JWT in `access_token` with `aud` including `email-api` and `scope` containing
`email:send`. Then call the API (use the simulator address so no real mail is sent):

```bash
curl -s -X POST https://email.mattgerega.net/api/emails \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"to":["delivered@resend.dev"],"subject":"Smoke test","text":"Hello"}' \
  | jq .
```

Expect `202` and an `EmailSendResult` with `status: "Sent"`.

---

## Rollback

1. Disable the `email-api` resource (disable, don't delete).
2. Disable any clients granted `email:send` / `email:read`.
3. Confirm the discovery document no longer advertises either scope.

The email service stays deployed but returns 401 for every request once tokens stop being issued.

---

## Promotion Checklist

- [ ] Stage admin UI (`auth.mattgerega.org`): sections 1–3, then the verify steps against stage
- [ ] Production admin UI (`auth.mattgerega.com`): sections 1–3, then the verify steps against production

Configuration does not replicate between environments.
