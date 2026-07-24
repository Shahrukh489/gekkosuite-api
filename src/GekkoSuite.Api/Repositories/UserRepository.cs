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
}
