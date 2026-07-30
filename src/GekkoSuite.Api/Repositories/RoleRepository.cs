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
    public Task<RoleEntity?> GetRoleByIdAsync(Guid organizationId, Guid roleId, MembershipScope? scope = null)
    {
        const string sql = """
            SELECT
                r.role_id AS RoleId,
                r.name AS Name,
                r.description AS Description,
                r.scope::text AS Scope,
                r.is_managed AS IsManaged,
                COALESCE(
                    json_agg(
                        json_build_object(
                            'permissionId', p.permission_id,
                            'resource', p.resource,
                            'action', p.action,
                            'isElevated', p.is_elevated
                        ) ORDER BY p.resource, p.action
                    ) FILTER (WHERE p.permission_id IS NOT NULL),
                    '[]'
                ) AS Permissions
            FROM role r
            LEFT JOIN role_permission rp ON rp.role_id = r.role_id
            LEFT JOIN permission p ON p.permission_id = rp.permission_id
            WHERE r.role_id = @roleId
              AND (@scope IS NULL OR r.scope::text = @scope)
              AND (r.is_managed OR r.organization_id = @organizationId)
            GROUP BY r.role_id, r.name, r.description, r.scope, r.is_managed
            """;

        return QuerySingleOrDefaultAsync<RoleEntity>(organizationId, sql, new { roleId, organizationId, scope = scope?.ToString() });
    }
}
