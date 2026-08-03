using Npgsql;

using GekkoSuite.Api.Entities;

namespace GekkoSuite.Api.Repositories;

public class StoreRepository : BaseRepository, IStoreRepository
{
    public StoreRepository(NpgsqlDataSource db) : base(db)
    {
    }

    /// <inheritdoc />
    public Task<StoreEntity?> GetStoreByIdAsync(Guid organizationId, Guid storeId)
    {
        const string sql = """
            SELECT
                s.store_id AS StoreId,
                s.organization_id AS OrganizationId,
                s.name AS Name,
                s.type::text AS Type,
                s.is_default AS IsDefault,
                s.created_at AS CreatedAt
            FROM store s
            WHERE s.store_id = @storeId
              AND s.organization_id = @organizationId
              AND NOT s.is_deleted
            """;

        return QuerySingleOrDefaultAsync<StoreEntity>(organizationId, storeId, sql, new { storeId, organizationId });
    }

    /// <inheritdoc />
    public Task<IEnumerable<UserEntity>> GetStoreUsersAsync(Guid organizationId, Guid storeId)
    {
        // @TODO: look at returning non active membership
        const string sql = """
            SELECT
                u.user_id AS UserId,
                u.first_name AS FirstName,
                u.last_name AS LastName,
                u.email AS Email,
                u.is_active AS IsActive,
                u.user_type::text AS UserType
            FROM membership m
            JOIN user_account u ON u.user_id = m.user_id AND NOT u.is_deleted
            WHERE m.store_id = @storeId
              AND m.organization_id = @organizationId
              AND m.scope = 'STORE'
              AND m.is_active AND NOT m.is_deleted
            ORDER BY u.first_name
            """;

        return QueryAsync<UserEntity>(organizationId, storeId, sql, new { organizationId, storeId });
    }
}
