using Microsoft.AspNetCore.Authorization;

using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Enums;
using GekkoSuite.Api.Extensions;
using GekkoSuite.Api.Services;

namespace GekkoSuite.Api.Policies;

public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IUserService _userService;

    public PermissionHandler(IUserService userService)
    {
        _userService = userService;
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
            List<MembershipDto>? memberships = await _userService.GetUserOrganizationMembershipsAsync(organizationId, userId);
            if (Grants(memberships, requirement.Permission))
            {
                context.Succeed(requirement);
            }
        }
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
    private static bool Grants(List<MembershipDto>? memberships, string? permission)
    {
        if (memberships is null || memberships.Count == 0)
        {
            return false;
        }

        // if we dont have to check a permission then return authorized
        if (permission is null)
        {
            return true;
        }

        // loop through each membership and check if any one them have the required permission
        foreach (MembershipDto membership in memberships)
        {
            if (membership.Permissions.Contains(permission))
            {
                return true;
            }
        }

        return false;
    }
}
