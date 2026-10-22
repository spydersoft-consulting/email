# Resend Configuration — Per-Env Runbook

Two free Resend accounts, each with one verified domain:

| Account | Domain | Backs |
| --- | --- | --- |
| nonproduction | `mattgerega.net` | test and stage |
| production | `mattgerega.com` | production |

The service forces every outgoing From address onto the environment's domain
(`Email:SendingDomain`), so only the verified domain is ever used. Per-env default senders
(`Email:DefaultFrom`) keep test and stage mail distinguishable:

| Env | `Email:SendingDomain` | `Email:DefaultFrom` |
| --- | --- | --- |
| test | `mattgerega.net` | `Spydersoft <test-noreply@mattgerega.net>` |
| stage | `mattgerega.net` | `Spydersoft <stage-noreply@mattgerega.net>` |
| production | `mattgerega.com` | `Spydersoft <noreply@mattgerega.com>` |

---

## 1. Verify the domain (once per account)

1. Resend dashboard → **Domains** → **Add Domain** → enter the domain for that account.
2. Add the DNS records Resend lists to the domain's zone (typically a DKIM `TXT`/`CNAME`, an SPF
   `TXT` and `MX` for the send subdomain, and optionally a DMARC `TXT`).
3. Click **Verify** and wait for the status to go green.

Record the exact records you added here, per domain, so the zone can be rebuilt:

| Domain | Type | Name | Value |
| --- | --- | --- | --- |
| `mattgerega.net` | | | |
| `mattgerega.com` | | | |

## 2. Create an API key

Resend → **API Keys** → create a key with **Sending access** limited to the verified domain.

| Account | Store in Vault |
| --- | --- |
| nonproduction | `secrets-k8/email/test/resend.apiKey` and `secrets-k8/email/stage/resend.apiKey` (same key) |
| production | `secrets-k8/email/production/resend.apiKey` |

The key is shown once; copy it straight into Vault.

## 3. Create the webhook endpoint (per env, after its route is live)

The endpoint URL must resolve, so create it only after the chart is deployed and
`https://<host>/livez` responds.

| Env | Account | Endpoint URL | Store signing secret in |
| --- | --- | --- | --- |
| test | nonproduction | `https://email.mattgerega.net/api/webhooks/resend` | `secrets-k8/email/test/resend.webhookSecret` |
| stage | nonproduction | `https://email.mattgerega.org/api/webhooks/resend` | `secrets-k8/email/stage/resend.webhookSecret` |
| production | production | `https://email.mattgerega.com/api/webhooks/resend` | `secrets-k8/email/production/resend.webhookSecret` |

1. Resend → **Webhooks** → **Add Endpoint**, enter the URL.
2. Subscribe to: `email.sent`, `email.delivered`, `email.delivery_delayed`, `email.bounced`,
   `email.complained`, `email.opened`, `email.clicked`.
3. Copy the **Signing Secret** (`whsec_...`) into Vault at the path above.
4. Wait for the ExternalSecret to refresh (hourly) or restart the pod so the API picks it up.

The endpoint is authenticated by the Svix signature only; requests with a bad signature or a
timestamp more than five minutes old get `401`.

## 4. Verify

Send to Resend's simulator addresses (no real mail is delivered):

| Recipient | Expected final status |
| --- | --- |
| `delivered@resend.dev` | `Delivered` |
| `bounced@resend.dev` | `Bounced` |
| `complained@resend.dev` | `Complained` |

```bash
curl -s -X POST https://email.mattgerega.net/api/emails \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"to":["delivered@resend.dev"],"subject":"Webhook check","text":"Hello"}'

curl -s https://email.mattgerega.net/api/emails/<id> -H "Authorization: Bearer $READ_TOKEN"
curl -s https://email.mattgerega.net/api/emails/<id>/events -H "Authorization: Bearer $READ_TOKEN"
```

`GET /api/emails/{id}` should reach the expected status within a few seconds and the events list
should show the delivery events. If it stays at `Sent`, check the webhook's delivery log in
Resend and confirm `Resend:WebhookSecret` matches the endpoint's signing secret.

## Local development

The Aspire AppHost runs with `Resend__Mode=fake` and needs no Resend account. To hit Resend from a
local run, set `Resend__Mode=live` and `Resend__ApiKey` via user-secrets and send only to the
simulator addresses above.
