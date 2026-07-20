using Dapper;
using Npgsql;

namespace GekkoSuite.Api.Repositories;

/// <summary>
/// Base for all repositories. Most calls open one transaction that first stamps the tenant onto the DB
/// session so Row-Level Security scopes the query, then runs the raw SQL.
///  Two levels (see auth.md):
///   - org actions   → set app.current_org (prevent different orgs from leaking data)
///   - store actions → set app.current_org AND app.current_store (prevent different stores in same org from leaking data)
/// The pre-tenant lookups (e.g. login, where the tenant isn't known yet) instead use
/// QuerySingleOrDefaultUnscopedAsync — see its own doc comment for when that's safe to use.
/// </summary>
public abstract class BaseRepository
{
    private readonly NpgsqlDataSource _dataSource;

    protected BaseRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    /// <summary>
    /// Runs a SELECT and returns every matching row, org-scoped by RLS.
    /// </summary>
    /// <typeparam name="T">The type each row maps to.</typeparam>
    /// <param name="organizationId">The tenant. Set as app.current_org so RLS hides other orgs' rows.</param>
    /// <param name="sql">The raw SQL to run.</param>
    /// <param name="parameters">Dapper parameters for the SQL (anonymous object), or null.</param>
    /// <returns>The matched rows (empty if none).</returns>
    protected Task<IEnumerable<T>> QueryAsync<T>(Guid organizationId, string sql, object? parameters = null)
    {
        return RunAsync(organizationId, null, c => c.QueryAsync<T>(sql, parameters));
    }

    /// <summary>
    /// Runs a SELECT expected to return exactly one row, org-scoped by RLS. Throws if zero or many.
    /// </summary>
    /// <typeparam name="T">The type the row maps to.</typeparam>
    /// <param name="organizationId">The tenant. Set as app.current_org so RLS hides other orgs' rows.</param>
    /// <param name="sql">The raw SQL to run.</param>
    /// <param name="parameters">Dapper parameters for the SQL (anonymous object), or null.</param>
    /// <returns>The single matched row.</returns>
    protected Task<T> QuerySingleAsync<T>(Guid organizationId, string sql, object? parameters = null)
    {
        return RunAsync(organizationId, null, c => c.QuerySingleAsync<T>(sql, parameters));
    }

    /// <summary>
    /// Runs a SELECT expected to return one row or none, org-scoped by RLS. Throws if many.
    /// </summary>
    /// <typeparam name="T">The type the row maps to.</typeparam>
    /// <param name="organizationId">The tenant. Set as app.current_org so RLS hides other orgs' rows.</param>
    /// <param name="sql">The raw SQL to run.</param>
    /// <param name="parameters">Dapper parameters for the SQL (anonymous object), or null.</param>
    /// <returns>The matched row, or default if none.</returns>
    protected Task<T?> QuerySingleOrDefaultAsync<T>(Guid organizationId, string sql, object? parameters = null)
    {
        return RunAsync(organizationId, null, c => c.QuerySingleOrDefaultAsync<T>(sql, parameters));
    }

    /// <summary>
    /// Runs an INSERT/UPDATE/DELETE, org-scoped by RLS.
    /// </summary>
    /// <param name="organizationId">The tenant. Set as app.current_org so RLS scopes the write to this org.</param>
    /// <param name="sql">The raw SQL to run.</param>
    /// <param name="parameters">Dapper parameters for the SQL (anonymous object), or null.</param>
    /// <returns>The number of rows affected.</returns>
    protected Task<int> ExecuteAsync(Guid organizationId, string sql, object? parameters = null)
    {
        return RunAsync(organizationId, null, c => c.ExecuteAsync(sql, parameters));
    }

    /// <summary>
    /// Runs a SELECT and returns every matching row, scoped by RLS to one org and one store.
    /// </summary>
    /// <typeparam name="T">The type each row maps to.</typeparam>
    /// <param name="organizationId">The tenant. Set as app.current_org.</param>
    /// <param name="storeId">The store. Set as app.current_store so RLS can't return another store's rows in the same org.</param>
    /// <param name="sql">The raw SQL to run.</param>
    /// <param name="parameters">Dapper parameters for the SQL (anonymous object), or null.</param>
    /// <returns>The matched rows (empty if none).</returns>
    protected Task<IEnumerable<T>> QueryAsync<T>(Guid organizationId, Guid storeId, string sql, object? parameters = null)
    {
        return RunAsync(organizationId, storeId, c => c.QueryAsync<T>(sql, parameters));
    }

    /// <summary>
    /// Runs a SELECT expected to return exactly one row, scoped by RLS to one org and one store. Throws if zero or many.
    /// </summary>
    /// <typeparam name="T">The type the row maps to.</typeparam>
    /// <param name="organizationId">The tenant. Set as app.current_org.</param>
    /// <param name="storeId">The store. Set as app.current_store so RLS can't return another store's rows in the same org.</param>
    /// <param name="sql">The raw SQL to run.</param>
    /// <param name="parameters">Dapper parameters for the SQL (anonymous object), or null.</param>
    /// <returns>The single matched row.</returns>
    protected Task<T> QuerySingleAsync<T>(Guid organizationId, Guid storeId, string sql, object? parameters = null)
    {
        return RunAsync(organizationId, storeId, c => c.QuerySingleAsync<T>(sql, parameters));
    }

    /// <summary>
    /// Runs a SELECT expected to return one row or none, scoped by RLS to one org and one store. Throws if many.
    /// </summary>
    /// <typeparam name="T">The type the row maps to.</typeparam>
    /// <param name="organizationId">The tenant. Set as app.current_org.</param>
    /// <param name="storeId">The store. Set as app.current_store so RLS can't return another store's rows in the same org.</param>
    /// <param name="sql">The raw SQL to run.</param>
    /// <param name="parameters">Dapper parameters for the SQL (anonymous object), or null.</param>
    /// <returns>The matched row, or default if none.</returns>
    protected Task<T?> QuerySingleOrDefaultAsync<T>(Guid organizationId, Guid storeId, string sql, object? parameters = null)
    {
        return RunAsync(organizationId, storeId, c => c.QuerySingleOrDefaultAsync<T>(sql, parameters));
    }

    /// <summary>
    /// Runs an INSERT/UPDATE/DELETE, scoped by RLS to one org and one store.
    /// </summary>
    /// <param name="organizationId">The tenant. Set as app.current_org.</param>
    /// <param name="storeId">The store. Set as app.current_store so the write can't land in another store in the same org.</param>
    /// <param name="sql">The raw SQL to run.</param>
    /// <param name="parameters">Dapper parameters for the SQL (anonymous object), or null.</param>
    /// <returns>The number of rows affected.</returns>
    protected Task<int> ExecuteAsync(Guid organizationId, Guid storeId, string sql, object? parameters = null)
    {
        return RunAsync(organizationId, storeId, c => c.ExecuteAsync(sql, parameters));
    }

    /// <summary>
    /// Runs a SELECT expected to return one row or none, WITHOUT stamping a tenant onto the session — so
    /// it runs outside RLS's org/store scoping entirely. This is a narrow, deliberate exception to the
    /// tenant-first pattern above: it exists only for lookups that must happen *before* the tenant is
    /// known, e.g. finding a user by email during login (you don't know their org until you've found
    /// them). Only safe for queries that don't need a tenant filter to stay correct — e.g. matching on a
    /// column that's already globally unique, like user.email.
    /// </summary>
    /// <typeparam name="T">The type the row maps to.</typeparam>
    /// <param name="sql">The raw SQL to run.</param>
    /// <param name="parameters">Dapper parameters for the SQL (anonymous object), or null.</param>
    /// <returns>The matched row, or default if none.</returns>
    protected async Task<T?> QuerySingleOrDefaultUnscopedAsync<T>(string sql, object? parameters = null)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        return await connection.QuerySingleOrDefaultAsync<T>(sql, parameters);
    }

    /// <summary>
    /// The single place that opens a connection + transaction, stamps the tenant onto the DB session for
    /// RLS, runs the caller's query on that same connection, then commits. Set-tenant and query must share
    /// one transaction — that's what makes set_config's is_local (SET LOCAL) apply and stops the setting
    /// leaking to the next request that borrows this pooled connection.
    /// </summary>
    /// <typeparam name="TResult">What the query returns (rows, a row, or an affected-count).</typeparam>
    /// <param name="organizationId">The tenant, set as app.current_org.</param>
    /// <param name="storeId">The store to also scope to (app.current_store), or null for an org-only action.</param>
    /// <param name="query">The Dapper call to run on the tenant-stamped connection.</param>
    /// <returns>Whatever the query produced.</returns>
    private async Task<TResult> RunAsync<TResult>(Guid organizationId, Guid? storeId, Func<NpgsqlConnection, Task<TResult>> query)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        // for RLS set the organization_id, any rows not in this organization are invisible to query
        await connection.ExecuteAsync("SELECT set_config('app.current_org', @org, true)", new { org = organizationId.ToString() });

        // for RLS set the store_id, any rows not in this store are invisible to query
        if (storeId is not null)
        {
            await connection.ExecuteAsync("SELECT set_config('app.current_store', @store, true)", new { store = storeId.Value.ToString() });
        }

        var result = await query(connection);

        await transaction.CommitAsync();
        return result;
    }
}
