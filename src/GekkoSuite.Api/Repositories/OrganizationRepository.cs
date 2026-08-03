using Npgsql;

using GekkoSuite.Api.Entities;

namespace GekkoSuite.Api.Repositories;

public class OrganizationRepository : BaseRepository, IOrganizationRepository
{
    public OrganizationRepository(NpgsqlDataSource db) : base(db)
    {
    }

    /// <inheritdoc />
    public Task<OrganizationEntity?> GetOrganizationByIdAsync(Guid organizationId)
    {
        const string sql = """
            SELECT
                o.organization_id AS OrganizationId,
                o.name AS Name,
                o.description AS Description,
                o.billing_status::text AS BillingStatus,
                o.created_at AS CreatedAt
            FROM organization o
            WHERE o.organization_id = @organizationId
              AND NOT o.is_deleted
            """;

        return QuerySingleOrDefaultAsync<OrganizationEntity>(organizationId, sql, new { organizationId });
    }

    /// <inheritdoc />
    public Task<IEnumerable<StoreEntity>> GetOrganizationStoresAsync(Guid organizationId)
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
            WHERE organization_id = @organizationId
              AND NOT is_deleted
            ORDER BY is_default DESC, name
            """;

        return QueryAsync<StoreEntity>(organizationId, sql, new { organizationId });
    }

    /// <inheritdoc />
    public Task<IEnumerable<UserEntity>> GetOrganizationUsersAsync(Guid organizationId)
    {
        const string sql = """
            SELECT
                user_id AS UserId,
                first_name AS FirstName,
                last_name AS LastName,
                email AS Email,
                phone AS Phone,
                is_active AS IsActive,
                user_type::text AS UserType,
                organization_id AS OrganizationId,
                created_at AS CreatedAt
            FROM user_account
            WHERE organization_id = @organizationId
              AND NOT is_deleted
            ORDER BY first_name
            """;

        return QueryAsync<UserEntity>(organizationId, sql, new { organizationId });
    }

}
