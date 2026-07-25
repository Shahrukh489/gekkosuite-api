using Npgsql;

using GekkoSuite.Api.Entities;
using GekkoSuite.Api.Enums;

namespace GekkoSuite.Api.Repositories;

public class RoleRepository : BaseRepository, IRoleRepository
{
    public RoleRepository(NpgsqlDataSource db) : base(db)
    {
    }

    /// <inheritdoc />
    public Task<IEnumerable<RoleEntity>> GetRolesAsync(Guid organizationId, MembershipScope? scope)
    {
        // managed roles (org-null) are visible to everyone; custom roles only to their owning org.
        const string sql = """
            SELECT
                role_id AS RoleId,
                name AS Name,
                description AS Description,
                scope::text AS Scope,
                is_managed AS IsManaged
            FROM role
            WHERE (is_managed OR organization_id = @organizationId)
              AND (@scope IS NULL OR scope::text = @scope)
            ORDER BY name
            """;

        return QueryAsync<RoleEntity>(organizationId, sql, new { organizationId, scope = scope?.ToString() });
    }

    /// <inheritdoc />
    public Task<RoleEntity?> GetRoleByIdAsync(Guid organizationId, Guid roleId)
    {
        const string sql = """
            SELECT
                role_id AS RoleId,
                name AS Name,
                description AS Description,
                scope::text AS Scope,
                is_managed AS IsManaged
            FROM role
            WHERE role_id = @roleId
              AND (is_managed OR organization_id = @organizationId)
            """;

        return QuerySingleOrDefaultAsync<RoleEntity>(organizationId, sql, new { roleId, organizationId });
    }

    /// <inheritdoc />
    public Task<IEnumerable<PermissionEntity>> GetRolePermissionsAsync(Guid organizationId, Guid roleId)
    {
        const string sql = """
            SELECT
                p.permission_id AS PermissionId,
                p.resource AS Resource,
                p.action AS Action,
                p.is_elevated AS IsElevated
            FROM role_permission rp
            JOIN permission p ON p.permission_id = rp.permission_id
            WHERE rp.role_id = @roleId
            ORDER BY p.resource, p.action
            """;

        return QueryAsync<PermissionEntity>(organizationId, sql, new { roleId });
    }
}
