using Npgsql;

using GekkoSuite.Api.Entities;

namespace GekkoSuite.Api.Repositories;

public class AuthenticationRepository : BaseRepository, IAuthenticationRepository
{
    public AuthenticationRepository(NpgsqlDataSource db) : base(db)
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
}
