using Npgsql;

using GekkoSuite.Api.Dtos;
using GekkoSuite.Api.Entities;
using GekkoSuite.Api.Exceptions;

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

    /// <inheritdoc />
    public async Task<CustomerEntity> CreateCustomerAsync(CreateCustomerDto dto, Guid customerId, DateTimeOffset now)
    {
        const string sql = """
            INSERT INTO store_customer (
                store_customer_id, store_id, organization_id, name, email, phone,
                is_active, created_at, updated_at, is_deleted
            )
            VALUES (
                @customerId, @storeId, @organizationId, @name, @email, @phone,
                TRUE, @now, @now, FALSE
            )
            RETURNING
                store_customer_id AS CustomerId,
                store_id AS StoreId,
                organization_id AS OrganizationId,
                name AS Name,
                email AS Email,
                phone AS Phone,
                is_active AS IsActive,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            """;

        try
        {
            return await QuerySingleAsync<CustomerEntity>(dto.OrganizationId, dto.StoreId, sql, new
            {
                customerId,
                storeId = dto.StoreId,
                organizationId = dto.OrganizationId,
                name = dto.Name,
                email = dto.Email,
                phone = dto.Phone,
                now,
            });
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new ConflictException("A customer with this email already exists at this store.");
        }
    }
}
