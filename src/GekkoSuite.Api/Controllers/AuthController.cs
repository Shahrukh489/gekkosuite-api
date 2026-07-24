using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;

using GekkoSuite.Api.Extensions;
using GekkoSuite.Api.Services;
using GekkoSuite.Api.Dtos;

namespace GekkoSuite.Api.Controllers;


[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserService _userService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, IUserService userService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _userService = userService;
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

            LoginResponse? response = await _authService.LoginAsync(request.Email, request.Password);
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

    /// <summary>
    /// Returns the current user (self read). The userId and organizationId are taken only from the
    /// validated token, never from the request. Any authenticated user may call this; no permission needed.
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UserDto>> GetMeAsync()
    {
        try
        {
            var userId = User.GetUserId();
            var organizationId = User.GetOrganizationId();

            UserDto? user = await _userService.GetUserAsync(organizationId, userId);
            if (user is null)
            {
                return NotFound();
            }

            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error.");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}
