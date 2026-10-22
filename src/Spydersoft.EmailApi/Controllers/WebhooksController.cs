using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Spydersoft.EmailApi.Resend;
using Spydersoft.EmailApi.Webhooks;

namespace Spydersoft.EmailApi.Controllers;

[ApiController]
[Route("api/webhooks")]
[AllowAnonymous]
public sealed class WebhooksController(
    ResendWebhookHandler handler,
    IOptions<ResendOptions> options,
    TimeProvider time) : ControllerBase
{
    [HttpPost("resend")]
    public async Task<IActionResult> Resend(CancellationToken cancellationToken)
    {
        string body;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
        {
            body = await reader.ReadToEndAsync(cancellationToken);
        }

        var id = Request.Headers["svix-id"].ToString();
        var timestamp = Request.Headers["svix-timestamp"].ToString();
        var signature = Request.Headers["svix-signature"].ToString();

        if (!SvixSignatureVerifier.Verify(options.Value.WebhookSecret, id, timestamp, signature, body, time.GetUtcNow()))
        {
            return Unauthorized();
        }

        var result = await handler.HandleAsync(id, body, cancellationToken);
        return result == WebhookResult.Invalid ? BadRequest() : Ok();
    }
}
