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
                organization_id AS OrganizationId,
                name AS Name,
                description AS Description,
                billing_status::text AS BillingStatus,
                created_at AS CreatedAt
            FROM organization
            WHERE organization_id = @organizationId
              AND NOT is_deleted
            """;

        return QuerySingleOrDefaultAsync<OrganizationEntity>(organizationId, sql, new { organizationId });
    }
}
