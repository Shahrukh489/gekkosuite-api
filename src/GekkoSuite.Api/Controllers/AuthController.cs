using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;

using GekkoSuite.Api.Extensions;
using GekkoSuite.Api.Services;
using GekkoSuite.Api.Dtos;

namespace GekkoSuite.Api.Controllers;


[ApiController]
[Route("auth")]
public class AuthController : BaseController
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger) : base(logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Gets an accessToken if a user has verified email and password
    /// Public endpoint without any permissions needed
    /// </summary>
    [HttpPost("login")]
    [EndpointName("Login")]
    [AllowAnonymous]
    [Consumes(MediaTypeNames.Application.Json)]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> LoginAsync([FromBody] LoginRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest("Email and password are required." );
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
            return ErrorResponse(ex);
        }
    }

    /// <summary>
    /// Get the current user and his memberships
    /// </summary>
    [HttpGet("me")]
    [EndpointName("GetCurrentUser")]
    [Authorize]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UserDto>> GetCurrentUserAsync()
    {
        try
        {
            var userId = User.GetUserId();
            var organizationId = User.GetOrganizationId();

            UserDto? user = await _authService.GetCurrentUserAsync(organizationId, userId);
            if (user is null)
            {
                return NotFound();
            }

            return Ok(user);
        }
        catch (Exception ex)
        {
            return ErrorResponse(ex);
        }
    }

}
