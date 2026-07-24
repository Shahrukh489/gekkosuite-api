using Npgsql;

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
            FROM "user"
            WHERE email = @email
                AND NOT is_deleted
                AND is_active
            """;

        return QuerySingleOrDefaultUnscopedAsync<UserEntity>(sql, new { email });
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
            FROM "user"
            WHERE user_id = @userId
              AND organization_id = @organizationId
              AND NOT is_deleted
            """;

        return QuerySingleOrDefaultAsync<UserEntity>(organizationId, sql, new { userId, organizationId });
    }

    /// <inheritdoc />
    public Task<MembershipEntity?> GetOrgMembershipAsync(Guid organizationId, Guid userId)
    {
        const string sql = """
            SELECT
                o.organization_id AS OrganizationId,
                o.name AS Name,
                r.name AS RoleName
            FROM membership m
            JOIN membership_assignment ma ON ma.membership_id = m.membership_id
            JOIN role r ON r.role_id = ma.role_id AND r.scope = 'ORGANIZATION'
            JOIN organization o ON o.organization_id = m.organization_id AND NOT o.is_deleted
            JOIN "user" u ON u.user_id = m.user_id
            WHERE m.user_id = @userId
              AND m.organization_id = @organizationId
              AND m.scope = 'ORGANIZATION'
              AND u.is_active AND NOT u.is_deleted
              AND m.is_active AND NOT m.is_deleted
              AND (ma.expires_at IS NULL OR ma.expires_at > now())
            """;

        return QuerySingleOrDefaultAsync<MembershipEntity>(organizationId, sql, new { userId, organizationId });
    }

    /// <inheritdoc />
    public Task<IEnumerable<MembershipEntity>> GetStoreMembershipsAsync(Guid organizationId, Guid userId)
    {
        const string sql = """
            SELECT
                s.store_id AS StoreId,
                s.name AS Name,
                r.name AS RoleName
            FROM membership m
            JOIN membership_assignment ma ON ma.membership_id = m.membership_id
            JOIN role r ON r.role_id = ma.role_id AND r.scope = 'STORE'
            JOIN store s ON s.store_id = m.store_id AND NOT s.is_deleted
            JOIN "user" u ON u.user_id = m.user_id
            WHERE m.user_id = @userId
              AND m.organization_id = @organizationId
              AND m.scope = 'STORE'
              AND u.is_active AND NOT u.is_deleted
              AND m.is_active AND NOT m.is_deleted
              AND (ma.expires_at IS NULL OR ma.expires_at > now())
            ORDER BY m.created_at
            """;

        return QueryAsync<MembershipEntity>(organizationId, sql, new { userId, organizationId });
    }
}
