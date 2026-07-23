using Npgsql;

using GekkoSuite.Api.Models;

namespace GekkoSuite.Api.Repositories;

public class UserRepository : BaseRepository, IUserRepository
{
    public UserRepository(NpgsqlDataSource db) : base(db)
    {
    }

    /// <summary>
    /// Finds a live (not soft-deleted) user by their login email.
    /// </summary>
    /// <param name="email">The login email to look up.</param>
    /// <returns>The matching user, or null if no live account has that email.</returns>
    public Task<UserEntity?> FindByEmailAsync(string email)
    {
        // Login has to find the user BEFORE it knows their org, so this can't go through the normal
        // org-scoped helpers on BaseRepository (they require the tenant up front). Safe without a tenant
        // filter because email is globally unique (UNIQUE (email) on "user") — the query can only ever
        // match one account. "AND NOT is_deleted" keeps a soft-deleted account out of login entirely,
        // so it behaves the same as "no such email" (see auth.md's R12 — don't leak which case failed).
        const string sql = """
            SELECT
                user_id AS UserId,
                organization_id AS OrganizationId,
                email AS Email,
                password AS Password,
                name AS Name,
                phone AS Phone,
                is_active AS IsActive,
                is_org_owner AS IsOrgOwner,
                created_by_user_id AS CreatedByUserId,
                created_at AS CreatedAt,
                is_deleted AS IsDeleted,
                deleted_at AS DeletedAt
            FROM "user"
            WHERE email = @email AND NOT is_deleted
            """;

        return QuerySingleOrDefaultUnscopedAsync<UserEntity>(sql, new { email });
    }

    /// <summary>
    /// Finds a live (not soft-deleted) user by id within a specific organization. Used to re-check
    /// user.is_active fresh on every request once the JWT's claims are already validated — a token being
    /// unexpired doesn't mean the account wasn't deactivated a minute ago (see auth.md, §1).
    /// </summary>
    /// <param name="organizationId">The tenant the user must belong to (from the validated token).</param>
    /// <param name="userId">The user id to look up (from the validated token).</param>
    /// <returns>The matching user, or null if no live account matches both.</returns>
    public Task<UserEntity?> FindByIdAsync(Guid organizationId, Guid userId)
    {
        // organization_id is filtered explicitly here, not left to RLS alone — RLS isn't enabled on
        // this table yet (see database.md's RLS section, still @TODO), and even once it is, app-level
        // scoping stays the first line of defense, RLS the backstop (see auth.md's safety nets).
        const string sql = """
            SELECT
                user_id AS UserId,
                organization_id AS OrganizationId,
                email AS Email,
                password AS Password,
                name AS Name,
                phone AS Phone,
                is_active AS IsActive,
                is_org_owner AS IsOrgOwner,
                created_by_user_id AS CreatedByUserId,
                created_at AS CreatedAt,
                is_deleted AS IsDeleted,
                deleted_at AS DeletedAt
            FROM "user"
            WHERE user_id = @userId AND organization_id = @organizationId AND NOT is_deleted
            """;

        return QuerySingleOrDefaultAsync<UserEntity>(organizationId, sql, new { userId, organizationId });
    }
}
