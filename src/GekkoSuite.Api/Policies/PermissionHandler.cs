using Microsoft.AspNetCore.Authorization;

using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Enums;
using GekkoSuite.Api.Extensions;
using GekkoSuite.Api.Services;

namespace GekkoSuite.Api.Policies;

public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IUserService _userService;
    private readonly ILogger<PermissionHandler> _logger;

    public PermissionHandler(IUserService userService, ILogger<PermissionHandler> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    /// <summary>
    /// Authorizes the request: the caller must hold a live membership of the required scope, and — if the
    /// requirement names a permission — one of the roles on those memberships must grant it.
    /// </summary>
    /// <param name="context">The authorization context, carrying the validated user.</param>
    /// <param name="requirement">The scope and optional permission the endpoint requires.</param>
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var userId = context.User.GetUserId();
        var organizationId = context.User.GetOrganizationId();

        if (requirement.Scope == MembershipScope.ORGANIZATION)
        {
            _logger.LogDebug("Authorizing user {UserId} for an ORGANIZATION action in org {OrganizationId}, required permission {Permission}.", userId, organizationId, requirement.Permission);
            List<MembershipDto>? memberships = await _userService.GetUserOrganizationMembershipsAsync(organizationId, userId);
            if (Grants(memberships, requirement.Permission))
            {
                context.Succeed(requirement);
            }
        }

        if (requirement.Scope == MembershipScope.STORE)
        {
            Guid? storeId = GetRouteStoreId(context);
            if (storeId is null)
            {
                _logger.LogDebug("Denying STORE action for user {UserId}: no valid {RouteParam} in the route.", userId, Constants.STORE_ID);
                return;
            }

            _logger.LogDebug("Authorizing user {UserId} for a STORE action on store {StoreId} in org {OrganizationId}, required permission {Permission}.", userId, storeId.Value, organizationId, requirement.Permission);
            List<MembershipDto>? memberships = await _userService.GetUserStoreMembershipsByStoreIdAsync(organizationId, userId, storeId.Value);
            if (Grants(memberships, requirement.Permission))
            {
                context.Succeed(requirement);
            }

            // @TODO: an ORGANIZATION membership reaches every store — allow it here too, filtered to
            // store-relevant (non-elevated) permissions. Needs the store-relevant-org-permissions query.
        }
    }

    /// <summary>
    /// Reads the {storeId} route value as a Guid, or null if it is absent or malformed (fail closed).
    /// </summary>
    /// <param name="context">The authorization context whose Resource is the HttpContext.</param>
    /// <returns>The parsed store id, or null.</returns>
    private static Guid? GetRouteStoreId(AuthorizationHandlerContext context)
    {
        if (context.Resource is not HttpContext httpContext)
        {
            return null;
        }

        object? routeValue = httpContext.Request.RouteValues[Constants.STORE_ID];
        if (routeValue is not null && Guid.TryParse(routeValue.ToString(), out Guid storeId))
        {
            return storeId;
        }

        return null;
    }

    // @TODO: how to check if role and membership scope are same along with whether or now the permission is elevated
    // so it cant be on store role accidentally/.

    /// <summary>
    /// True when the caller is authorized: they hold at least one membership, and — if a permission is
    /// required — one of those memberships' roles grants it.
    /// </summary>
    /// <param name="memberships">The caller's memberships at the place, or null if they hold none.</param>
    /// <param name="permission">The required permission code, or null if membership alone suffices.</param>
    /// <returns>Whether the memberships authorize the request.</returns>
    private bool Grants(List<MembershipDto>? memberships, string? permission)
    {
        if (memberships is null || memberships.Count == 0)
        {
            _logger.LogDebug("Denied: the user holds no membership at this place.");
            return false;
        }

        // no permission required — holding the membership is enough
        if (permission is null)
        {
            _logger.LogDebug("Allowed: membership is sufficient, no permission required.");
            return true;
        }

        foreach (MembershipDto membership in memberships)
        {
            if (membership.Permissions.Contains(permission))
            {
                _logger.LogDebug("Allowed: membership {MembershipId} role {RoleName} ({RoleId}) grants permission {Permission}.", membership.MembershipId, membership.RoleName, membership.RoleId, permission);
                return true;
            }
        }

        _logger.LogDebug("Denied: none of the user's roles grant permission {Permission}.", permission);
        return false;
    }
}
