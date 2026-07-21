using Microsoft.AspNetCore.Mvc;

using GekkoSuite.Api.Services;

namespace GekkoSuite.Api.Controllers;

public class LoginRequest
{
    /// <summary>The account's login email.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>The account's plaintext password, checked against the stored Argon2id hash.</summary>
    public string Password { get; set; } = string.Empty;
}


[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    // Reject oversized input BEFORE it reaches Argon2 — a huge password would make the (deliberately
    // expensive) hash a resource-DoS. 254 is the RFC 5321 max email length; 128 is a generous password cap.
    private const int MaxEmailLength = 254;
    private const int MaxPasswordLength = 128;
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Exchanges an email + password for an access token (docs/api.md). Public — no token needed to call
    /// this, since the caller doesn't have one yet.
    /// Deferred, deliberately, not forgotten: IP/account rate-limiting and lockout (auth.md's Security
    /// Review, R5) — this endpoint is the realistic target for credential stuffing and doesn't have it yet.
    /// </summary>
    /// <param name="request">The login credentials.</param>
    /// <returns>
    /// 200 with the access token on success; 400 if a field is missing or oversized; 401 for anything else
    /// that refuses login — unknown email, wrong password, and a disabled account all look identical on
    /// purpose (see IAuthService.LoginAsync); 500 if an unexpected error occurs.
    /// </returns>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest("Email and password are required.");
            }

            // Length caps run before the service, so an oversized password never reaches Argon2.
            if (request.Email.Length > MaxEmailLength || request.Password.Length > MaxPasswordLength)
            {
                return BadRequest("Email or password is too long.");
            }

            var response = await _authService.LoginAsync(request.Email, request.Password);
            if (response is null)
            {
                return Unauthorized();
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            // A failure HERE is an infrastructure problem (DB down, malformed stored hash, etc.), NOT bad
            // credentials — so log it and return 500. Returning 401 would hide real outages as "login failed".
            _logger.LogError(ex, "Unexpected error during login.");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}
