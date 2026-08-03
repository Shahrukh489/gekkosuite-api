using Dapper;
using Npgsql;

namespace GekkoSuite.Api.Repositories;


public abstract class BaseRepository
{
    private readonly NpgsqlDataSource _dataSource;

    protected BaseRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    /// <summary>
    /// Runs a SELECT and returns every matching row
    /// RLS scope is organization level.
    /// </summary>
    protected Task<IEnumerable<T>> QueryAsync<T>(Guid organizationId, string sql, object? parameters = null)
    {
        return RunAsync(organizationId, null, c => c.QueryAsync<T>(sql, parameters));
    }

    /// <summary>
    /// Runs a SELECT expected to return exactly one row
    /// RLS scope is organization level.
    /// </summary>
    protected Task<T> QuerySingleAsync<T>(Guid organizationId, string sql, object? parameters = null)
    {
        return RunAsync(organizationId, null, c => c.QuerySingleAsync<T>(sql, parameters));
    }

    /// <summary>
    /// Runs a SELECT expected to return one row or none
    /// RLS scope is organization level.
    /// </summary>
    protected Task<T?> QuerySingleOrDefaultAsync<T>(Guid organizationId, string sql, object? parameters = null)
    {
        return RunAsync(organizationId, null, c => c.QuerySingleOrDefaultAsync<T>(sql, parameters));
    }

    /// <summary>
    /// Runs an INSERT/UPDATE/DELETE
    /// RLS scope is organization level.
    /// </summary>
    protected Task<int> ExecuteAsync(Guid organizationId, string sql, object? parameters = null)
    {
        return RunAsync(organizationId, null, c => c.ExecuteAsync(sql, parameters));
    }

    /// <summary>
    /// Runs a SELECT and returns every matching row,
    /// RLS scope is store and organization level.
    /// </summary>
    protected Task<IEnumerable<T>> QueryAsync<T>(Guid organizationId, Guid storeId, string sql, object? parameters = null)
    {
        return RunAsync(organizationId, storeId, c => c.QueryAsync<T>(sql, parameters));
    }

    /// <summary>
    /// Runs a SELECT expected to return exactly one row
    /// RLS scope is store and organization level.
    /// </summary>
    protected Task<T> QuerySingleAsync<T>(Guid organizationId, Guid storeId, string sql, object? parameters = null)
    {
        return RunAsync(organizationId, storeId, c => c.QuerySingleAsync<T>(sql, parameters));
    }

    /// <summary>
    /// Runs a SELECT expected to return one row or none
    /// RLS scope is store and organization level.
    /// </summary>
    protected Task<T?> QuerySingleOrDefaultAsync<T>(Guid organizationId, Guid storeId, string sql, object? parameters = null)
    {
        return RunAsync(organizationId, storeId, c => c.QuerySingleOrDefaultAsync<T>(sql, parameters));
    }

    /// <summary>
    /// Runs an INSERT/UPDATE/DELETE
    /// RLS scope is store and organization level.
    /// </summary>
    protected Task<int> ExecuteAsync(Guid organizationId, Guid storeId, string sql, object? parameters = null)
    {
        return RunAsync(organizationId, storeId, c => c.ExecuteAsync(sql, parameters));
    }

    /// <summary>
    /// Runs a SELECT expected to return one row or none 
    /// Bypasses RLS
    /// </summary>
    protected async Task<T?> QuerySingleOrDefaultUnscopedAsync<T>(string sql, object? parameters = null)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        return await connection.QuerySingleOrDefaultAsync<T>(sql, parameters);
    }

    /// <summary>
    /// Runs a SELECT and returns every matching row
    /// Bypasses RLS
    /// </summary>
    protected async Task<IEnumerable<T>> QueryUnscopedAsync<T>(string sql, object? parameters = null)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        return await connection.QueryAsync<T>(sql, parameters);
    }

    /// <summary>
    /// Runs the database query and adds the Organization RLS scope and optional Store RLS scope
    /// </summary>
    private async Task<TResult> RunAsync<TResult>(Guid organizationId, Guid? storeId, Func<NpgsqlConnection, Task<TResult>> query)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        // @TODO: add later
        // for RLS set the organization_id, any rows not in this organization are invisible to query
        // await connection.ExecuteAsync("SELECT set_config('app.current_org', @org, true)", new { org = organizationId.ToString() });

        // // for RLS set the store_id, any rows not in this store are invisible to query
        // if (storeId is not null)
        // {
        //     await connection.ExecuteAsync("SELECT set_config('app.current_store', @store, true)", new { store = storeId.Value.ToString() });
        // }

        var result = await query(connection);

        await transaction.CommitAsync();
        return result;
    }
}
