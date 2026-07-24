using Microsoft.AspNetCore.Authorization;

using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Enums;
using GekkoSuite.Api.Extensions;
using GekkoSuite.Api.Services;

namespace GekkoSuite.Api.Policies;

public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IUserService _userService;
    private readonly IStoreService _storeService;
    private readonly ILogger<PermissionHandler> _logger;

    public PermissionHandler(IUserService userService, IStoreService storeService, ILogger<PermissionHandler> logger)
    {
        _userService = userService;
        _storeService = storeService;
        _logger = logger;
    }


    /// <summary>
    /// Checks whether the user's ORGANIZATION membership grants access — the membership exists and one of its
    /// roles grants the permission.
    /// </summary>
    private async Task<bool> CheckOrganizationAccess(Guid organizationId, Guid userId, string permission)
    {
        _logger.LogDebug("Checking to authorize user {UserId} for a ORGANIZATION Action on organization {OrganizationId}, required permission {Permission}.", userId, organizationId, permission);
        List<MembershipDto>? memberships = await _userService.GetUserOrganizationMembershipsAsync(organizationId, userId);
        return Grants(memberships, permission);
    }

    /// <summary>
    /// Checks whether the user may act on the given store — the membership exists and one of its roles grants
    /// the permission.
    /// </summary>
    private async Task<bool> CheckStoreAccess(Guid organizationId, Guid userId, Guid storeId, string permission)
    {
        _logger.LogDebug("Checking to authorize user {UserId} for a STORE action on store {StoreId} in org {OrganizationId}, required permission {Permission}.", userId, storeId, organizationId, permission);
        List<MembershipDto>? storeMemberships = await _userService.GetUserStoreMembershipsByStoreIdAsync(organizationId, userId, storeId);
        return Grants(storeMemberships, permission);
    }

    /// <summary>
    /// Reads the {storeId} route value as a Guid, or null if it is absent or malformed (fail closed).
    /// </summary>
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
    private bool Grants(List<MembershipDto>? memberships, string permission)
    {
        if (memberships is null || memberships.Count == 0)
        {
            _logger.LogDebug("Denied: the user holds no membership at this place.");
            return false;
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

    /// <summary>
    /// Authorizes the request: the caller must hold a live membership of the required scope, and — if the
    /// requirement names a permission — one of the roles on those memberships must grant it.
    /// </summary>
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var userId = context.User.GetUserId();
        var organizationId = context.User.GetOrganizationId();

        if (requirement.Scope == MembershipScope.ORGANIZATION)
        {
            if (await CheckOrganizationAccess(organizationId, userId, requirement.Permission))
            {
                context.Succeed(requirement);
            }

            return;
        }

        if (requirement.Scope == MembershipScope.STORE)
        {
            Guid? storeId = GetRouteStoreId(context);
            if (storeId is null)
            {
                _logger.LogDebug("Denying STORE action for user {UserId}: no valid {RouteParam} in the route.", userId, Constants.STORE_ID);
                return;
            }

            // the store must belong to the caller's org; a null result means it doesn't exist for them
            StoreDto? store = await _storeService.GetStoreByIdAsync(organizationId, storeId.Value);
            if (store is null)
            {
                _logger.LogDebug("Denying STORE action for user {UserId}: store {StoreId} is not in org {OrganizationId}.", userId, storeId, organizationId);
                return;
            }

            // a store membership at this store grants access directly
            if (await CheckStoreAccess(organizationId, userId, storeId.Value, requirement.Permission))
            {
                context.Succeed(requirement);
                return;
            }

            // no store membership granted it — an ORGANIZATION membership reaches every store, so check that next
            _logger.LogDebug("No store membership granted access for user {UserId} on store {StoreId}; checking their ORGANIZATION membership.", userId, storeId);
            if (await CheckOrganizationAccess(organizationId, userId, requirement.Permission))
            {
                context.Succeed(requirement);
            }
        }
    }
}
