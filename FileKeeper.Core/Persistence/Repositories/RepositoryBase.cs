using Dapper;
using ErrorOr;
using FileKeeper.Core.Interfaces.Persistence;

namespace FileKeeper.Core.Persistence.Repositories;

public abstract class RepositoryBase
{
    protected readonly IDatabaseService DatabaseService;

    protected RepositoryBase(IDatabaseService databaseService)
    {
        DatabaseService = databaseService;
    }

    /// <summary>
    /// Executes a query that returns multiple rows.
    /// </summary>
    protected async Task<ErrorOr<IEnumerable<T>>> QueryAsync<T>(
        string sql,
        object? param = null,
        CancellationToken token = default)
    {
        try
        {
            var connection = DatabaseService.GetConnection();
            var result = await connection.QueryAsync<T>(sql, param);
            return result.ToList();
        }
        catch (Exception ex)
        {
            return Error.Failure("repository.query_failed", $"Query execution failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes a query that returns a single entity.
    /// </summary>
    protected async Task<ErrorOr<T?>> QuerySingleOrDefaultAsync<T>(
        string sql,
        object? param = null,
        CancellationToken token = default)
    {
        try
        {
            var connection = DatabaseService.GetConnection();
            return await connection.QuerySingleOrDefaultAsync<T>(sql, param);
        }
        catch (Exception ex)
        {
            return Error.Failure("repository.query_failed", $"Query execution failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes a query that returns a scalar value.
    /// </summary>
    protected async Task<ErrorOr<T?>> QueryScalarAsync<T>(
        string sql,
        object? param = null,
        CancellationToken token = default)
    {
        try
        {
            var connection = DatabaseService.GetConnection();
            return await connection.QuerySingleOrDefaultAsync<T>(sql, param);
        }
        catch (Exception ex)
        {
            return Error.Failure("repository.query_failed", $"Query execution failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes an INSERT, UPDATE, or DELETE command.
    /// </summary>
    protected async Task<ErrorOr<int>> ExecuteAsync(
        string sql,
        object? param = null,
        CancellationToken token = default)
    {
        try
        {
            var connection = DatabaseService.GetConnection();
            var affectedRows = await connection.ExecuteAsync(sql, param);
            return affectedRows;
        }
        catch (Exception ex)
        {
            return Error.Failure("repository.execution_failed", $"Command execution failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes a query that streams results using IAsyncEnumerable.
    /// Useful for large result sets (e.g., BLOBs) to avoid loading everything into memory at once.
    /// </summary>
    protected ErrorOr<IAsyncEnumerable<T>> StreamQueryAsync<T>(
        string sql,
        object? param = null,
        CancellationToken token = default)
    {
        try
        {
            async IAsyncEnumerable<T> StreamResults()
            {
                var connection = DatabaseService.GetConnection();
                var result = await connection.QueryAsync<T>(sql, param);
                foreach (var item in result)
                {
                    token.ThrowIfCancellationRequested();
                    yield return item;
                }
            }

            IAsyncEnumerable<T> stream = StreamResults();
            return stream;
        }
        catch (Exception ex)
        {
            return Error.Failure("repository.query_failed", $"Query execution failed: {ex.Message}");
        }
    }
}