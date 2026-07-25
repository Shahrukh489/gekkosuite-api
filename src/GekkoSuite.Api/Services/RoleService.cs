using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Entities;
using GekkoSuite.Api.Enums;
using GekkoSuite.Api.Repositories;

namespace GekkoSuite.Api.Services;

public class RoleService : IRoleService
{
    private readonly IRoleRepository _roleRepository;

    public RoleService(IRoleRepository roleRepository)
    {
        _roleRepository = roleRepository;
    }

    /// <inheritdoc />
    public async Task<List<RoleDto>> GetRolesAsync(Guid organizationId, MembershipScope? scope)
    {
        IEnumerable<RoleEntity> roleEntities = await _roleRepository.GetRolesAsync(organizationId, scope);
        return new RoleDto().FromEntityList(roleEntities.ToList());
    }

    /// <inheritdoc />
    public async Task<RoleDto?> GetRoleByIdAsync(Guid organizationId, Guid roleId)
    {
        RoleEntity? roleEntity = await _roleRepository.GetRoleByIdAsync(organizationId, roleId);
        if (roleEntity is null)
        {
            return null;
        }

        RoleDto roleDto = new RoleDto().FromEntity(roleEntity);

        IEnumerable<PermissionEntity> permissionEntities = await _roleRepository.GetRolePermissionsAsync(organizationId, roleId);
        roleDto.Permissions = new PermissionDto().FromEntityList(permissionEntities.ToList());

        return roleDto;
    }
}
