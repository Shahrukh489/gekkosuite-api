using Npgsql;

using GekkoSuite.Api.Entities;

namespace GekkoSuite.Api.Repositories;

public class CustomerRepository : BaseRepository, ICustomerRepository
{
    public CustomerRepository(NpgsqlDataSource db) : base(db)
    {
    }

    /// <inheritdoc />
    public Task<IEnumerable<CustomerEntity>> GetStoreCustomersAsync(Guid organizationId, Guid storeId)
    {
        const string sql = """
            SELECT
                store_customer_id AS CustomerId,
                store_id AS StoreId,
                organization_id AS OrganizationId,
                name AS Name,
                email AS Email,
                phone AS Phone,
                is_active AS IsActive,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM store_customer
            WHERE store_id = @storeId
              AND organization_id = @organizationId
              AND NOT is_deleted
            ORDER BY name
            """;

        return QueryAsync<CustomerEntity>(organizationId, storeId, sql, new { storeId, organizationId });
    }
}
