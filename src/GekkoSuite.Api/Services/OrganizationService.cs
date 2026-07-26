using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Entities;
using GekkoSuite.Api.Enums;
using GekkoSuite.Api.Repositories;

namespace GekkoSuite.Api.Services;

public class OrganizationService : IOrganizationService
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUserService _userService;
    private readonly IRoleService _roleService;
    private readonly IStoreService _storeService;

    public OrganizationService(IOrganizationRepository organizationRepository, IUserService userService, IRoleService roleService, IStoreService storeService)
    {
        _organizationRepository = organizationRepository;
        _userService = userService;
        _roleService = roleService;
        _storeService = storeService;
    }

    /// <inheritdoc />
    public async Task<OrganizationDto?> GetOrganizationByIdAsync(Guid organizationId)
    {
        OrganizationEntity? organizationEntity = await _organizationRepository.GetOrganizationByIdAsync(organizationId);
        if (organizationEntity == null)
        {
            return null;
        }

        return OrganizationDto.FromEntity(organizationEntity);
    }

    /// <inheritdoc />
    public async Task<List<UserDto>> GetUsersAsync(Guid organizationId)
    {
        return await _userService.GetUsersWithMembershipsAsync(organizationId);
    }

    /// <inheritdoc />
    public async Task<UserDto?> GetUserByIdAsync(Guid organizationId, Guid userId)
    {
        return await _userService.GetUserByIdWithMembershipsAsync(organizationId, userId);
    }

    /// <inheritdoc />
    public async Task<List<RoleDto>> GetRolesAsync(Guid organizationId, MembershipScope? scope)
    {
        return await _roleService.GetRolesAsync(organizationId, scope);
    }

    /// <inheritdoc />
    public async Task<RoleDto?> GetRoleByIdAsync(Guid organizationId, Guid roleId)
    {
        return await _roleService.GetRoleByIdAsync(organizationId, roleId);
    }

    /// <inheritdoc />
    public async Task<List<StoreDto>> GetStoresAsync(Guid organizationId)
    {
        return await _storeService.GetStoresAsync(organizationId);
    }
}
