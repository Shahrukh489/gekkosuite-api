using Npgsql;

using GekkoSuite.Api.Entities;

namespace GekkoSuite.Api.Repositories;

public class StoreProductRepository : BaseRepository, IStoreProductRepository
{
    public StoreProductRepository(NpgsqlDataSource db) : base(db)
    {
    }

    /// <inheritdoc />
    public Task<IEnumerable<StoreProductEntity>> GetStoreProductsAsync(Guid organizationId, Guid storeId)
    {
        const string sql = """
            SELECT
                store_product_id AS StoreProductId,
                store_id AS StoreId,
                organization_id AS OrganizationId,
                name AS Name,
                description AS Description,
                sku AS Sku,
                price AS Price,
                stock AS Stock,
                is_active AS IsActive,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM store_product
            WHERE store_id = @storeId
              AND organization_id = @organizationId
              AND NOT is_deleted
            ORDER BY name
            """;

        return QueryAsync<StoreProductEntity>(organizationId, storeId, sql, new { storeId, organizationId });
    }
}
