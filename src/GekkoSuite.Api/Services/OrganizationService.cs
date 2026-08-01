using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Entities;
using GekkoSuite.Api.Enums;
using GekkoSuite.Api.Repositories;

namespace GekkoSuite.Api.Services;

public class OrganizationService : IOrganizationService
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IRoleService _roleService;

    public OrganizationService(IOrganizationRepository organizationRepository, IRoleService roleService)
    {
        _organizationRepository = organizationRepository;
        _roleService = roleService;
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
    public async Task<List<UserDto>> GetOrganizationUsersAsync(Guid organizationId)
    {
        IEnumerable<UserEntity> userEntities = await _organizationRepository.GetOrganizationUsersAsync(organizationId);
        return userEntities.Select(UserDto.FromEntity).ToList();
    }

    /// <inheritdoc />
    public async Task<List<RoleDto>> GetOrganizationRolesAsync(Guid organizationId)
    {
        // @TODO: verify if this will return null or empty list
        return await _roleService.GetRolesAsync(organizationId);
    }

    /// <inheritdoc />
    public async Task<RoleDto?> GetOrganizationRoleByIdAsync(Guid organizationId, Guid roleId)
    {
        return await _roleService.GetRoleByIdAsync(organizationId, roleId);
    }

    /// <inheritdoc />
    public async Task<List<StoreDto>> GetOrganizationStoresAsync(Guid organizationId)
    {
        // @TODO: verify if this will return null or empty list
        IEnumerable<StoreEntity> storeEntities = await _organizationRepository.GetOrganizationStoresAsync(organizationId);
        return storeEntities.Select(StoreDto.FromEntity).ToList();
    }

}
