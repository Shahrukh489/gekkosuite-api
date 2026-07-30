using Npgsql;

using GekkoSuite.Api.Entities;

namespace GekkoSuite.Api.Repositories;

public class StoreRepository : BaseRepository, IStoreRepository
{
    public StoreRepository(NpgsqlDataSource db) : base(db)
    {
    }

    /// <inheritdoc />
    /// @TODO:L should we move to organizationRepostiry
    public Task<IEnumerable<StoreEntity>> GetStoresAsync(Guid organizationId)
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
    public Task<StoreEntity?> GetStoreByIdAsync(Guid organizationId, Guid storeId)
    {
        const string sql = """
            SELECT
                s.store_id AS StoreId,
                s.organization_id AS OrganizationId,
                s.name AS Name,
                s.type::text AS Type,
                s.is_default AS IsDefault,
                s.created_at AS CreatedAt,
                COALESCE(
                    (SELECT json_agg(
                                json_build_object(
                                    'subscriptionId', sub.subscription_id,
                                    'offeringId', off.offering_id,
                                    'offeringName', off.name,
                                    'offeringType', off.type,
                                    'status', sub.status,
                                    'pricePerStore', off.price_per_store,
                                    'trialEndsAt', sub.trial_ends_at,
                                    'currentPeriodEnd', sub.current_period_end,
                                    'features', COALESCE(
                                        (SELECT array_agg(f.code ORDER BY f.code)
                                         FROM offering_feature ofe
                                         JOIN feature f ON f.feature_id = ofe.feature_id AND f.scope = 'STORE'
                                         WHERE ofe.offering_id = off.offering_id),
                                        '{}'
                                    )
                                ) ORDER BY off.name
                            )
                     FROM subscription sub
                     JOIN offering off ON off.offering_id = sub.offering_id
                     WHERE sub.organization_id = s.organization_id
                       AND sub.status IN ('ACTIVE', 'TRIALING')),
                    '[]'
                ) AS Subscriptions
            FROM store s
            WHERE s.store_id = @storeId
              AND s.organization_id = @organizationId
              AND NOT s.is_deleted
            """;

        return QuerySingleOrDefaultAsync<StoreEntity>(organizationId, storeId, sql, new { storeId, organizationId });
    }
}
