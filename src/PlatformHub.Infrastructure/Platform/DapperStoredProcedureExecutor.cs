using System.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using PlatformHub.Infrastructure.Persistence;

namespace PlatformHub.Infrastructure.Platform;

/// <summary>
/// Reusable Dapper executor for SQL Server stored procedures.
/// Uses the current EF Core connection and leaves connection lifetime to EF when already open.
/// </summary>
public interface IDapperStoredProcedureExecutor
{
    Task<IReadOnlyList<T>> QueryAsync<T>(
        string procedureName,
        object? parameters = null,
        CancellationToken cancellationToken = default);
}

public sealed class DapperStoredProcedureExecutor(PlatformDbContext db)
    : IDapperStoredProcedureExecutor
{
    public async Task<IReadOnlyList<T>> QueryAsync<T>(
        string procedureName,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(procedureName))
            throw new ArgumentException("Stored procedure name is required.", nameof(procedureName));

        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync(cancellationToken);

        try
        {
            var command = new CommandDefinition(
                commandText: procedureName,
                parameters: parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken);

            var rows = await connection.QueryAsync<T>(command);
            return rows.AsList();
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }
}
