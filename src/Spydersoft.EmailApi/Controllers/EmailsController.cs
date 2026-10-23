using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Spydersoft.Email.Client;
using Spydersoft.EmailApi.Services;

namespace Spydersoft.EmailApi.Controllers;

[ApiController]
[Route("api/emails")]
public sealed class EmailsController(IEmailService emails) : ControllerBase
{
    [HttpPost]
    [Authorize(AuthorizationPolicies.Send)]
    public async Task<IActionResult> Send([FromBody] EmailMessage message, CancellationToken cancellationToken)
    {
        var sender = new SenderIdentity(
            User.FindFirstValue("client_id"),
            User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier));

        SendOutcome outcome;
        try
        {
            outcome = await emails.SendAsync(message, sender, cancellationToken);
        }
        catch (EmailValidationException ex)
        {
            return Problem(ex.Message, statusCode: ex.TooLarge ? StatusCodes.Status413PayloadTooLarge : StatusCodes.Status400BadRequest);
        }

        if (outcome.FailureStatusCode is { } failure)
        {
            return Problem(outcome.FailureDetail, statusCode: failure);
        }

        return outcome.Existing ? Ok(outcome.Result) : Accepted(outcome.Result);
    }
}
