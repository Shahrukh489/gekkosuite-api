using Dapper;
using Npgsql;

namespace GekkoSuite.Api.Repositories;

/// <summary>
/// Base for all repositories. Every call opens one transaction that first stamps the tenant onto the DB
/// session so Row-Level Security scopes the query, then runs the raw SQL. Two levels (see auth.md):
///   - org actions   → set app.current_org
///   - store actions → set app.current_org AND app.current_store (two same-org stores share an org, so the
///                      store must be set too, or a Store A request could see Store B's rows).
/// The tenant is passed in per call (fresh from the request's token) — nothing is stored, so repositories
/// stay stateless singletons. See CLAUDE.md: the app must connect as a non-superuser, non-owner role, and
/// the tenant must be transaction-scoped (set_config(..., is_local: true) == SET LOCAL).
/// Callers pass raw SQL strings; the method name picks the return shape.
/// </summary>
public abstract class BaseRepository
{
    private readonly NpgsqlDataSource _dataSource;

    protected BaseRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    // ----- org-scoped (org-owned tables) -----

    /// <summary>Many rows.</summary>
    protected Task<IEnumerable<T>> QueryAsync<T>(Guid organizationId, string sql, object? parameters = null)
    {
        return RunAsync(organizationId, null, c => c.QueryAsync<T>(sql, parameters));
    }

    /// <summary>Exactly one row (throws if not one).</summary>
    protected Task<T> QuerySingleAsync<T>(Guid organizationId, string sql, object? parameters = null)
    {
        return RunAsync(organizationId, null, c => c.QuerySingleAsync<T>(sql, parameters));
    }

    /// <summary>One row or default.</summary>
    protected Task<T?> QuerySingleOrDefaultAsync<T>(Guid organizationId, string sql, object? parameters = null)
    {
        return RunAsync(organizationId, null, c => c.QuerySingleOrDefaultAsync<T>(sql, parameters));
    }

    /// <summary>INSERT/UPDATE/DELETE. Returns rows affected.</summary>
    protected Task<int> ExecuteAsync(Guid organizationId, string sql, object? parameters = null)
    {
        return RunAsync(organizationId, null, c => c.ExecuteAsync(sql, parameters));
    }

    // ----- store-scoped (store-owned tables) — sets org AND store -----

    protected Task<IEnumerable<T>> QueryAsync<T>(Guid organizationId, Guid storeId, string sql, object? parameters = null)
    {
        return RunAsync(organizationId, storeId, c => c.QueryAsync<T>(sql, parameters));
    }

    protected Task<T> QuerySingleAsync<T>(Guid organizationId, Guid storeId, string sql, object? parameters = null)
    {
        return RunAsync(organizationId, storeId, c => c.QuerySingleAsync<T>(sql, parameters));
    }

    protected Task<T?> QuerySingleOrDefaultAsync<T>(Guid organizationId, Guid storeId, string sql, object? parameters = null)
    {
        return RunAsync(organizationId, storeId, c => c.QuerySingleOrDefaultAsync<T>(sql, parameters));
    }

    protected Task<int> ExecuteAsync(Guid organizationId, Guid storeId, string sql, object? parameters = null)
    {
        return RunAsync(organizationId, storeId, c => c.ExecuteAsync(sql, parameters));
    }

    // ----- the one place that opens the tx, stamps the tenant, and runs the query -----

    private async Task<TResult> RunAsync<TResult>(
        Guid organizationId,
        Guid? storeId,
        Func<NpgsqlConnection, Task<TResult>> query)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        // Transaction-scoped (is_local: true) and parameterized, so no SQL injection and no pool leak.
        await connection.ExecuteAsync(
            "SELECT set_config('app.current_org', @org, true)",
            new { org = organizationId.ToString() });

        if (storeId is not null)
        {
            await connection.ExecuteAsync(
                "SELECT set_config('app.current_store', @store, true)",
                new { store = storeId.Value.ToString() });
        }

        var result = await query(connection);

        await transaction.CommitAsync();
        return result;
    }
}
