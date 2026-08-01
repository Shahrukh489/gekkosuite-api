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
                o.created_at AS CreatedAt,
                COALESCE(
                    (SELECT 
                            json_agg(
                                    json_build_object(
                                        'subscriptionId', s.subscription_id,
                                        'offeringId', off.offering_id,
                                        'offeringName', off.name,
                                        'offeringType', off.type,
                                        'status', s.status,
                                        'pricePerStore', off.price_per_store,
                                        'trialEndsAt', s.trial_ends_at,
                                        'currentPeriodEnd', s.current_period_end,
                                        'features', 
                                            COALESCE(
                                            (
                                                SELECT array_agg(f.code ORDER BY f.code)
                                                    FROM offering_feature ofe
                                                    JOIN feature f ON f.feature_id = ofe.feature_id AND f.scope = 'ORGANIZATION'
                                                    WHERE ofe.offering_id = off.offering_id),
                                                '{}'
                                            )
                                        ) 
                        ORDER BY off.name
                    )
                     FROM subscription s
                     JOIN offering off ON off.offering_id = s.offering_id
                     WHERE s.organization_id = o.organization_id),
                    '[]'
                ) AS Subscriptions
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
