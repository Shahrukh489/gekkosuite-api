using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Repositories;

namespace GekkoSuite.Api.Services;

public class OrganizationService : IOrganizationService
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUserService _userService;

    public OrganizationService(IOrganizationRepository organizationRepository, IUserService userService)
    {
        _organizationRepository = organizationRepository;
        _userService = userService;
    }

    /// <inheritdoc />
    public async Task<List<string>> GetUserOrganizationPermissionsAsync(Guid organizationId, Guid userId)
    {
        List<MembershipDto>? memberships = await _userService.GetUserOrganizationMembershipsAsync(organizationId, userId);
        if (memberships is null)
        {
            return [];
        }

        // flatten each role's permissions into one deduped set
        HashSet<string> permissions = new HashSet<string>();
        foreach (MembershipDto membership in memberships)
        {
            foreach (string permission in membership.Permissions)
            {
                permissions.Add(permission);
            }
        }

        return permissions.ToList();
    }
}
