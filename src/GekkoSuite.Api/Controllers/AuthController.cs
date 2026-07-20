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
    /// 200 with the access token on success; 400 if a field is missing; 401 for anything else that
    /// refuses login — unknown email, wrong password, and a disabled account all look identical on
    /// purpose (see IAuthService.LoginAsync).
    /// </returns>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("Email and password are required.");
        }

        var response = await _authService.LoginAsync(request);
        if (response is null)
        {
            return Unauthorized();
        }

        return Ok(response);
    }
}
