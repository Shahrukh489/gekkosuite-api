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

    /// <inheritdoc />
    public Task<IEnumerable<SubscriptionEntity>> GetOrganizationSubscriptionsAsync(Guid organizationId)
    {
        const string sql = """
            SELECT
                s.subscription_id AS SubscriptionId,
                off.offering_id AS OfferingId,
                off.name AS OfferingName,
                off.type::text AS OfferingType,
                s.status::text AS Status,
                off.price_per_store AS PricePerStore,
                s.trial_ends_at AS TrialEndsAt,
                s.current_period_end AS CurrentPeriodEnd
            FROM subscription s
            JOIN offering off ON off.offering_id = s.offering_id
            WHERE s.organization_id = @organizationId
              AND s.status IN ('ACTIVE', 'TRIALING')
            ORDER BY off.name
            """;

        return QueryAsync<SubscriptionEntity>(organizationId, sql, new { organizationId });
    }

    /// <inheritdoc />
    public Task<IEnumerable<string>> GetOrganizationFeaturesAsync(Guid organizationId)
    {
        const string sql = """
            SELECT DISTINCT f.code
            FROM subscription s
            JOIN offering_feature ofe ON ofe.offering_id = s.offering_id
            JOIN feature f ON f.feature_id = ofe.feature_id AND f.scope = 'ORGANIZATION'
            WHERE s.organization_id = @organizationId
              AND s.status IN ('ACTIVE', 'TRIALING')
            ORDER BY f.code
            """;

        return QueryAsync<string>(organizationId, sql, new { organizationId });
    }
}
