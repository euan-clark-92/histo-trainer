using Microsoft.Data.Sqlite;

namespace TissueTrainer.Data.Repositories;

public sealed class UserRepository
{
    private readonly string _connectionString;

    public UserRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<long> GetOrCreateUserAsync(
        string displayName,
        string? employeeId = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var select = connection.CreateCommand())
        {
            select.CommandText = """
                SELECT id
                FROM users
                WHERE display_name = $displayName
                LIMIT 1;
                """;
            select.Parameters.AddWithValue("$displayName", displayName);
            var existing = await select.ExecuteScalarAsync(cancellationToken);
            if (existing is long id)
            {
                return id;
            }
        }

        await using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO users (display_name, employee_id)
            VALUES ($displayName, $employeeId);
            SELECT last_insert_rowid();
            """;
        insert.Parameters.AddWithValue("$displayName", displayName);
        insert.Parameters.AddWithValue("$employeeId", (object?)employeeId ?? DBNull.Value);
        var created = await insert.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(created);
    }
}
