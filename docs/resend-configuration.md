# Resend Configuration — Runbook

One Resend account with one verified domain, `mattgerega.com`, serves every environment. The
existing key and domain are stored at `secrets-k8/resend` (`api-key`, `domain`).

The service forces every outgoing From address onto `Email:SendingDomain`, so only the verified
domain is ever used. Per-env default senders (`Email:DefaultFrom`) keep non-production mail
distinguishable from production:

| Env | `Email:SendingDomain` | `Email:DefaultFrom` |
| --- | --- | --- |
| test | `mattgerega.com` | `Spydersoft <test-noreply@mattgerega.com>` |
| production | `mattgerega.com` | `Spydersoft <noreply@mattgerega.com>` |

Because all environments share one account, they share its send quota and sender reputation —
send only to Resend's simulator addresses from test.

---

## 1. Verify the domain (once)

1. Resend dashboard → **Domains** → **Add Domain** → `mattgerega.com`.
2. Add the DNS records Resend lists to the zone (typically a DKIM `TXT`/`CNAME`, an SPF `TXT` and
   `MX` for the send subdomain, and optionally a DMARC `TXT`).
3. Click **Verify** and wait for the status to go green. (Already done for the current key.)

Record the exact records you added so the zone can be rebuilt:

| Type | Name | Value |
| --- | --- | --- |
| | | |

## 2. API key

The key in `secrets-k8/resend.api-key` is reused by each environment. Copy it to the
environment's path (the ExternalSecret reads `apiKey` from there):

```bash
vault kv put secrets-k8/email/<env>/resend \
  apiKey="$(vault kv get -field=api-key secrets-k8/resend)" \
  webhookSecret="<signing secret from step 3>"
```

If you later create a dedicated sending-only key per environment, store it at the same path and
leave `secrets-k8/resend` for other consumers.

## 3. Webhook endpoint (per env, after its route is live)

The endpoint URL must resolve, so create it only after the chart is deployed and
`https://<host>/livez` responds.

| Env | Endpoint URL | Store signing secret in |
| --- | --- | --- |
| test | `https://email.mattgerega.net/api/webhooks/resend` | `secrets-k8/email/test/resend.webhookSecret` |
| production | `https://email.mattgerega.com/api/webhooks/resend` | `secrets-k8/email/production/resend.webhookSecret` |

1. Resend → **Webhooks** → **Add Endpoint**, enter the URL.
2. Subscribe to: `email.sent`, `email.delivered`, `email.delivery_delayed`, `email.bounced`,
   `email.complained`, `email.opened`, `email.clicked`.
3. Copy the **Signing Secret** (`whsec_...`) into Vault at the path above, keeping the existing
   `apiKey` property (`vault kv patch` updates one property).
4. Wait for the ExternalSecret to refresh (hourly) or restart the pod so the API picks it up.

## Sharing one account across environments

Resend sends every event to every webhook endpoint in the account, so test and production both receive each other's events. To tell them apart, the test deployment sets `Email:EnvironmentTag=test`: it tags outgoing mail with `environment=test` and handles only webhook events carrying that tag. Production leaves the setting empty and handles only events with no `environment` tag. Events for the other environment are acknowledged with 200 and ignored.

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
