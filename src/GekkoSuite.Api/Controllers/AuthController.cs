using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;

using GekkoSuite.Api.Extensions;
using GekkoSuite.Api.Services;
using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Enums;
using GekkoSuite.Api.Policies;

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
    /// Exchanges an email + password for an access token
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [Consumes(MediaTypeNames.Application.Json)]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LoginResponse>> LoginAsync([FromBody] LoginRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "Email and password are required." });
            }

            if (request.Email.Length > 254 || request.Password.Length > 128)
            {
                return BadRequest(new { message = "Email or password is too long."});
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
    /// Returns the current user (self read). The userId and organizationId are taken only from the
    /// validated token, never from the request. Any authenticated user may call this; no permission needed.
    /// </summary>
    [HttpGet("me")]
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
                return NotFound(new { message = "User not found."});
            }

            return Ok(user);
        }
        catch (Exception ex)
        {
            return ErrorResponse(ex);
        }
    }

    /// <summary>
    /// Returns the caller's flat set of permissions in their organization. The org membership is required
    /// (enforced by the policy); no specific permission is needed beyond holding an org membership.
    /// </summary>
    [HttpGet("me/organization/permissions")]
    [HasPermission(MembershipScope.ORGANIZATION)]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> GetCurrentUserOrganizationPermissionsAsync()
    {
        try
        {
            var userId = User.GetUserId();
            var organizationId = User.GetOrganizationId();

            List<string> permissions = await _authService.GetCurrentUserOrganizationPermissionsAsync(organizationId, userId);
            return Ok(permissions);
        }
        catch (Exception ex)
        {
            return ErrorResponse(ex);
        }
    }

    /// <summary>
    /// Returns the caller's flat set of permissions at the given store — their effective set for that store.
    /// </summary>
    [HttpGet("me/stores/{" + Constants.STORE_ID + "}/permissions")]
    [HasPermission(MembershipScope.STORE)]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> GetCurrentUserStorePermissionsByStoreIdAsync(Guid storeId)
    {
        try
        {
            var userId = User.GetUserId();
            var organizationId = User.GetOrganizationId();

            List<string> permissions = await _authService.GetCurrentUserStorePermissionsByStoreIdAsync(organizationId, userId, storeId);
            return Ok(permissions);
        }
        catch (Exception ex)
        {
            return ErrorResponse(ex);
        }
    }
}
