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
    protected Task<IEnumerable<T>> QueryAsync<T>(Guid organizationId, string sql, object? parameters = null)
    {
        return RunAsync(organizationId, null, c => c.QueryAsync<T>(sql, parameters));
    }

    /// <summary>
    /// Runs a SELECT expected to return exactly one row, org-scoped by RLS. 
    /// </summary>
    protected Task<T> QuerySingleAsync<T>(Guid organizationId, string sql, object? parameters = null)
    {
        return RunAsync(organizationId, null, c => c.QuerySingleAsync<T>(sql, parameters));
    }

    /// <summary>
    /// Runs a SELECT expected to return one row or none, org-scoped by RLS.
    /// </summary>
    protected Task<T?> QuerySingleOrDefaultAsync<T>(Guid organizationId, string sql, object? parameters = null)
    {
        return RunAsync(organizationId, null, c => c.QuerySingleOrDefaultAsync<T>(sql, parameters));
    }

    /// <summary>
    /// Runs an INSERT/UPDATE/DELETE, org-scoped by RLS.
    /// </summary>
    protected Task<int> ExecuteAsync(Guid organizationId, string sql, object? parameters = null)
    {
        return RunAsync(organizationId, null, c => c.ExecuteAsync(sql, parameters));
    }

    /// <summary>
    /// Runs a SELECT and returns every matching row, scoped by RLS to one org and one store.
    /// </summary>
    protected Task<IEnumerable<T>> QueryAsync<T>(Guid organizationId, Guid storeId, string sql, object? parameters = null)
    {
        return RunAsync(organizationId, storeId, c => c.QueryAsync<T>(sql, parameters));
    }

    /// <summary>
    /// Runs a SELECT expected to return exactly one row, scoped by RLS to one org and one store. 
    /// </summary>
    protected Task<T> QuerySingleAsync<T>(Guid organizationId, Guid storeId, string sql, object? parameters = null)
    {
        return RunAsync(organizationId, storeId, c => c.QuerySingleAsync<T>(sql, parameters));
    }

    /// <summary>
    /// Runs a SELECT expected to return one row or none, scoped by RLS to one org and one store. 
    /// </summary>
    protected Task<T?> QuerySingleOrDefaultAsync<T>(Guid organizationId, Guid storeId, string sql, object? parameters = null)
    {
        return RunAsync(organizationId, storeId, c => c.QuerySingleOrDefaultAsync<T>(sql, parameters));
    }

    /// <summary>
    /// Runs an INSERT/UPDATE/DELETE, scoped by RLS to one org and one store.
    /// </summary>
    protected Task<int> ExecuteAsync(Guid organizationId, Guid storeId, string sql, object? parameters = null)
    {
        return RunAsync(organizationId, storeId, c => c.ExecuteAsync(sql, parameters));
    }

    /// <summary>
    /// Runs a SELECT expected to return one row or none not using RLS
    protected async Task<T?> QuerySingleOrDefaultUnscopedAsync<T>(string sql, object? parameters = null)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        return await connection.QuerySingleOrDefaultAsync<T>(sql, parameters);
    }

    /// <summary>
    /// Runs a SELECT and returns every matching row, not using RLS (for global, tenant-less catalog tables).
    /// </summary>
    protected async Task<IEnumerable<T>> QueryUnscopedAsync<T>(string sql, object? parameters = null)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        return await connection.QueryAsync<T>(sql, parameters);
    }

    /// <summary>
    /// The single place that opens a connection + transaction, stamps the tenant onto the DB session for RLS
    /// </summary>
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
