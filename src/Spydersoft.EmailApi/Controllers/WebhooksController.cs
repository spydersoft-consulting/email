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
    public async Task<IActionResult> Resend(
        [FromHeader(Name = "svix-id")] string? id,
        [FromHeader(Name = "svix-timestamp")] string? timestamp,
        [FromHeader(Name = "svix-signature")] string? signature,
        CancellationToken cancellationToken)
    {
        // The signature covers the exact bytes received, so the body is read raw rather than model-bound.
        string body;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
        {
            body = await reader.ReadToEndAsync(cancellationToken);
        }

        if (!SvixSignatureVerifier.Verify(options.Value.WebhookSecret, id ?? string.Empty, timestamp ?? string.Empty, signature ?? string.Empty, body, time.GetUtcNow()))
        {
            return Unauthorized();
        }

        var result = await handler.HandleAsync(id ?? string.Empty, body, cancellationToken);
        return result == WebhookResult.Invalid ? BadRequest() : Ok();
    }
}
