using Npgsql;

using GekkoSuite.Api.Entities;

namespace GekkoSuite.Api.Repositories;

public class UserRepository : BaseRepository, IUserRepository
{
    public UserRepository(NpgsqlDataSource db) : base(db)
    {
    }

    /// <inheritdoc />
    public Task<UserEntity?> GetUserByEmailAsync(string email)
    {
        const string sql = """
            SELECT
                user_id AS UserId,
                organization_id AS OrganizationId,
                password AS Password,
                is_active AS IsActive
            FROM user_account
            WHERE email = @email
                AND NOT is_deleted
                AND is_active
            """;

        return QuerySingleOrDefaultUnscopedAsync<UserEntity>(sql, new { email });
    }

    /// <inheritdoc />
    public Task<Guid?> GetUserOrganizationIdAsync(Guid userId)
    {
        const string sql = """
            SELECT organization_id
            FROM user_account
            WHERE user_id = @userId
                AND is_active
                AND NOT is_deleted
            """;

        return QuerySingleOrDefaultUnscopedAsync<Guid?>(sql, new { userId });
    }

    /// <inheritdoc />
    public Task<UserEntity?> GetUserByIdAsync(Guid organizationId, Guid userId)
    {
        const string sql = """
            SELECT
                user_id AS UserId,
                organization_id AS OrganizationId,
                email AS Email,
                first_name AS FirstName,
                last_name AS LastName,
                phone AS Phone,
                is_active AS IsActive,
                is_org_owner AS IsOrgOwner,
                created_by_user_id AS CreatedByUserId,
                created_at AS CreatedAt
            FROM user_account
            WHERE user_id = @userId
              AND organization_id = @organizationId
              AND NOT is_deleted
            """;

        return QuerySingleOrDefaultAsync<UserEntity>(organizationId, sql, new { userId, organizationId });
    }

    /// <inheritdoc />
    public Task<IEnumerable<MembershipEntity>> GetUserOrganizationMembershipsAsync(Guid organizationId, Guid userId)
    {
        const string sql = """
            SELECT
                m.membership_id AS MembershipId,
                ma.assignment_id AS AssignmentId,
                m.scope::text AS Scope,
                o.organization_id AS OrganizationId,
                o.name AS Name,
                r.role_id AS RoleId,
                r.name AS RoleName,
                ma.assigned_at AS AssignedAt,
                ma.expires_at AS ExpiresAt,
                array_agg(p.resource || ':' || p.action) AS Permissions
            FROM membership m
            JOIN membership_assignment ma ON ma.membership_id = m.membership_id
            JOIN role r ON r.role_id = ma.role_id AND r.scope = 'ORGANIZATION'
            JOIN role_permission rp ON rp.role_id = r.role_id
            JOIN permission p ON p.permission_id = rp.permission_id
            JOIN organization o ON o.organization_id = m.organization_id AND NOT o.is_deleted
            JOIN user_account u ON u.user_id = m.user_id
            WHERE m.user_id = @userId
              AND m.organization_id = @organizationId
              AND m.scope = 'ORGANIZATION'
              AND u.is_active AND NOT u.is_deleted
              AND m.is_active AND NOT m.is_deleted
              AND (ma.expires_at IS NULL OR ma.expires_at > now())
            GROUP BY m.membership_id, ma.assignment_id, m.scope, o.organization_id, o.name, r.role_id, r.name, ma.assigned_at, ma.expires_at
            """;

        return QueryAsync<MembershipEntity>(organizationId, sql, new { userId, organizationId });
    }

    /// <inheritdoc />
    public Task<IEnumerable<MembershipEntity>> GetUserStoresMembershipsAsync(Guid organizationId, Guid userId)
    {
        const string sql = """
            SELECT
                m.membership_id AS MembershipId,
                ma.assignment_id AS AssignmentId,
                m.scope::text AS Scope,
                s.store_id AS StoreId,
                s.name AS Name,
                r.role_id AS RoleId,
                r.name AS RoleName,
                ma.assigned_at AS AssignedAt,
                ma.expires_at AS ExpiresAt,
                array_agg(p.resource || ':' || p.action) AS Permissions
            FROM membership m
            JOIN membership_assignment ma ON ma.membership_id = m.membership_id
            JOIN role r ON r.role_id = ma.role_id AND r.scope = 'STORE'
            JOIN role_permission rp ON rp.role_id = r.role_id
            JOIN permission p ON p.permission_id = rp.permission_id AND NOT p.is_elevated
            JOIN store s ON s.store_id = m.store_id AND NOT s.is_deleted
            JOIN user_account u ON u.user_id = m.user_id
            WHERE m.user_id = @userId
              AND m.organization_id = @organizationId
              AND m.scope = 'STORE'
              AND u.is_active AND NOT u.is_deleted
              AND m.is_active AND NOT m.is_deleted
              AND (ma.expires_at IS NULL OR ma.expires_at > now())
            GROUP BY m.membership_id, ma.assignment_id, m.scope, s.store_id, s.name, r.role_id, r.name, ma.assigned_at, ma.expires_at, m.created_at
            ORDER BY m.created_at
            """;

        return QueryAsync<MembershipEntity>(organizationId, sql, new { userId, organizationId });
    }

    /// <inheritdoc />
    public Task<IEnumerable<MembershipEntity>> GetUserStoreMembershipsAsync(Guid organizationId, Guid userId, Guid storeId)
    {
        const string sql = """
            SELECT
                m.membership_id AS MembershipId,
                ma.assignment_id AS AssignmentId,
                m.scope::text AS Scope,
                s.store_id AS StoreId,
                s.name AS Name,
                r.role_id AS RoleId,
                r.name AS RoleName,
                ma.assigned_at AS AssignedAt,
                ma.expires_at AS ExpiresAt,
                array_agg(p.resource || ':' || p.action) AS Permissions
            FROM membership m
            JOIN membership_assignment ma ON ma.membership_id = m.membership_id
            JOIN role r ON r.role_id = ma.role_id AND r.scope = 'STORE'
            JOIN role_permission rp ON rp.role_id = r.role_id
            JOIN permission p ON p.permission_id = rp.permission_id AND NOT p.is_elevated
            JOIN store s ON s.store_id = m.store_id AND NOT s.is_deleted
            JOIN user_account u ON u.user_id = m.user_id
            WHERE m.user_id = @userId
              AND m.organization_id = @organizationId
              AND m.store_id = @storeId
              AND m.scope = 'STORE'
              AND u.is_active AND NOT u.is_deleted
              AND m.is_active AND NOT m.is_deleted
              AND (ma.expires_at IS NULL OR ma.expires_at > now())
            GROUP BY m.membership_id, ma.assignment_id, m.scope, s.store_id, s.name, r.role_id, r.name, ma.assigned_at, ma.expires_at
            """;

        return QueryAsync<MembershipEntity>(organizationId, storeId, sql, new { userId, organizationId, storeId });
    }
}
