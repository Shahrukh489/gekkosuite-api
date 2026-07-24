using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;

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
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Exchanges an email + password for an access token
    /// </summary>
    [AllowAnonymous]
    [HttpPost("login")]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LoginResponse>> LoginAsync([FromBody] LoginRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest("Email and password are required.");
            }

            if (request.Email.Length > 254 || request.Password.Length > 128)
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
            _logger.LogError(ex, "Unexpected error.");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}
