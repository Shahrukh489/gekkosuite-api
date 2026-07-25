using Npgsql;

using GekkoSuite.Api.Entities;

namespace GekkoSuite.Api.Repositories;

public class StoreRepository : BaseRepository, IStoreRepository
{
    public StoreRepository(NpgsqlDataSource db) : base(db)
    {
    }

    /// <inheritdoc />
    public Task<IEnumerable<UserEntity>> GetStoreUsersAsync(Guid organizationId, Guid storeId)
    {
        const string sql = """
            SELECT
                u.user_id AS UserId,
                u.first_name AS FirstName,
                u.last_name AS LastName,
                u.email AS Email,
                u.is_active AS IsActive,
                json_agg(
                    json_build_object(
                        'membershipId', m.membership_id,
                        'assignmentId', ma.assignment_id,
                        'scope', m.scope,
                        'storeId', m.store_id,
                        'name', s.name,
                        'roleId', r.role_id,
                        'roleName', r.name,
                        'assignedAt', ma.assigned_at,
                        'expiresAt', ma.expires_at
                    ) ORDER BY r.name
                ) AS Memberships
            FROM membership m
            JOIN membership_assignment ma ON ma.membership_id = m.membership_id
                AND (ma.expires_at IS NULL OR ma.expires_at > now())
            JOIN role r ON r.role_id = ma.role_id AND r.scope = 'STORE'
            JOIN store s ON s.store_id = m.store_id AND NOT s.is_deleted
            JOIN user_account u ON u.user_id = m.user_id AND NOT u.is_deleted
            WHERE m.store_id = @storeId
              AND m.organization_id = @organizationId
              AND m.scope = 'STORE'
              AND m.is_active AND NOT m.is_deleted
            GROUP BY u.user_id, u.first_name, u.last_name, u.email, u.is_active
            ORDER BY u.first_name
            """;

        return QueryAsync<UserEntity>(organizationId, storeId, sql, new { organizationId, storeId });
    }

    /// <inheritdoc />
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
