// Local smoke test: needs the API running with Resend__Mode=fake, Auth__TestKey and Resend__WebhookSecret set
// (same values in this shell). Sends, queries, and posts signed webhooks. Run: node scripts/smoke-test.js
const c = require('crypto');
const b64u = b => Buffer.from(b).toString('base64url');
const key = Buffer.from(process.env.Auth__TestKey, 'base64');
const jwt = (claims) => { const h = b64u(JSON.stringify({alg:'HS256',typ:'JWT'})); const p = b64u(JSON.stringify(claims)); return `${h}.${p}.${c.createHmac('sha256', key).update(`${h}.${p}`).digest('base64url')}`; };
const tok = jwt({ client_id: 'e2e-client', scope: ['email:send','email:read'] });
const base = process.env.EMAIL_API_URL ?? 'http://localhost:5400';
const H = { Authorization: `Bearer ${tok}`, 'Content-Type': 'application/json' };
const j = async (r) => { const t = await r.text(); return `${r.status} ${t}`; };
(async () => {
  const body = JSON.stringify({ to: ['delivered@resend.dev'], subject: 'e2e', text: 'hello', idempotencyKey: 'e2e-1', from: 'X <x@evil.example>' });
  const r1 = await fetch(`${base}/api/emails`, { method: 'POST', headers: H, body });
  const t1 = await r1.text(); console.log('send', r1.status, t1);
  const res = JSON.parse(t1);
  console.log('send again', await j(await fetch(`${base}/api/emails`, { method: 'POST', headers: H, body })));
  console.log('no token', (await fetch(`${base}/api/emails`, { method: 'POST', headers: {'Content-Type':'application/json'}, body })).status);
  console.log('bad body', await j(await fetch(`${base}/api/emails`, { method: 'POST', headers: H, body: JSON.stringify({to:[],subject:'x'}) })));
  console.log('get', await j(await fetch(`${base}/api/emails/${res.id}`, { headers: H })));
  const wh = async (id, type, secret) => {
    const payload = JSON.stringify({ type, created_at: new Date().toISOString(), data: { email_id: res.resendId } });
    const ts = Math.floor(Date.now()/1000).toString();
    const k = Buffer.from(secret.replace('whsec_',''), 'base64');
    const sig = 'v1,' + c.createHmac('sha256', k).update(`${id}.${ts}.${payload}`).digest('base64');
    return j(await fetch(`${base}/api/webhooks/resend`, { method: 'POST', headers: {'svix-id': id, 'svix-timestamp': ts, 'svix-signature': sig, 'Content-Type':'application/json'}, body: payload }));
  };
  const s = process.env.Resend__WebhookSecret;
  console.log('webhook delivered', await wh('msg_a','email.delivered', s));
  console.log('webhook dup', await wh('msg_a','email.delivered', s));
  console.log('webhook opened', await wh('msg_b','email.opened', s));
  console.log('webhook bad sig', await wh('msg_c','email.bounced', 'whsec_' + Buffer.from('wrong').toString('base64')));
  console.log('get after', await j(await fetch(`${base}/api/emails/${res.id}`, { headers: H })));
  console.log('events', await j(await fetch(`${base}/api/emails/${res.id}/events`, { headers: H })));
  console.log('search', await j(await fetch(`${base}/api/emails?senderClientId=e2e-client&status=Delivered`, { headers: H })));
  console.log('search recipient', await j(await fetch(`${base}/api/emails?recipient=delivered@resend.dev`, { headers: H })));
})();
