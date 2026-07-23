using Npgsql;

namespace GekkoSuite.Api.Repositories;


public class UserRepository : BaseRepository, IUserRepository
{
    public UserRepository(NpgsqlDataSource db) : base(db)
    {
    }

    /// <inheritdoc />
    public Task<UserEntity?> FindByEmailAsync(string email)
    {
        const string sql = """
            SELECT
                user_id AS UserId,
                organization_id AS OrganizationId,
                email AS Email,
                password AS Password,
                name AS Name,
                phone AS Phone,
                is_active AS IsActive,
                is_org_owner AS IsOrgOwner,
                created_by_user_id AS CreatedByUserId,
                created_at AS CreatedAt,
                is_deleted AS IsDeleted,
                deleted_at AS DeletedAt
            FROM "user"
            WHERE email = @email AND NOT is_deleted
            """;

        return QuerySingleOrDefaultUnscopedAsync<UserEntity>(sql, new { email });
    }
}
