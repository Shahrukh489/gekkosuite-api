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
                store_id AS StoreId,
                organization_id AS OrganizationId,
                name AS Name,
                type::text AS Type,
                is_default AS IsDefault,
                created_at AS CreatedAt
            FROM store
            WHERE store_id = @storeId
              AND organization_id = @organizationId
              AND NOT is_deleted
            """;

        return QuerySingleOrDefaultAsync<StoreEntity>(organizationId, storeId, sql, new { storeId, organizationId });
    }

    /// <inheritdoc />
    public Task<IEnumerable<string>> GetStoreFeaturesAsync(Guid organizationId)
    {
        const string sql = """
            SELECT DISTINCT f.code
            FROM subscription s
            JOIN offering_feature ofe ON ofe.offering_id = s.offering_id
            JOIN feature f ON f.feature_id = ofe.feature_id AND f.scope = 'STORE'
            WHERE s.organization_id = @organizationId
              AND s.status IN ('ACTIVE', 'TRIALING')
            ORDER BY f.code
            """;

        return QueryAsync<string>(organizationId, sql, new { organizationId });
    }
}
