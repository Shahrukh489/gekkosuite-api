using Microsoft.AspNetCore.Mvc;

using GekkoSuite.Api.Dtos.Auth;
using GekkoSuite.Api.Services;

namespace GekkoSuite.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Exchanges an email + password for an access token (docs/api.md). Public — no token needed to call
    /// this, since the caller doesn't have one yet.
    /// Deferred, deliberately, not forgotten: IP/account rate-limiting and lockout (auth.md's Security
    /// Review, R5) — this endpoint is the realistic target for credential stuffing and doesn't have it yet.
    /// </summary>
    /// <param name="request">The login credentials.</param>
    /// <returns>
    /// 200 with the access token on success; 400 if a field is missing; 401 for any invalid credentials
    /// (unknown email and wrong password look identical, on purpose — see LoginOutcome); 403 if the
    /// account is disabled.
    /// </returns>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("Email and password are required.");
        }

        var result = await _authService.LoginAsync(request);

        if (result.Outcome == LoginOutcome.InvalidCredentials)
        {
            return Unauthorized();
        }

        if (result.Outcome == LoginOutcome.AccountDisabled)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return Ok(result.Response);
    }
}
