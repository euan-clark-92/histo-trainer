using Microsoft.Data.Sqlite;

namespace TissueTrainer.Data.Database;

public sealed class MigrationRunner
{
    private readonly string _connectionString;
    private readonly string _migrationsDirectory;

    public MigrationRunner(string connectionString, string migrationsDirectory)
    {
        _connectionString = connectionString;
        _migrationsDirectory = migrationsDirectory;
    }

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(GetDatabasePath())!);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureMigrationTableAsync(connection, cancellationToken);

        var appliedVersions = await GetAppliedVersionsAsync(connection, cancellationToken);
        var migrationFiles = Directory
            .GetFiles(_migrationsDirectory, "*.sql")
            .OrderBy(Path.GetFileName)
            .ToList();

        foreach (var file in migrationFiles)
        {
            var version = ParseMigrationVersion(Path.GetFileName(file));
            if (appliedVersions.Contains(version))
            {
                continue;
            }

            var sql = await File.ReadAllTextAsync(file, cancellationToken);
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                await ExecuteScriptAsync(connection, sql, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }

    private string GetDatabasePath()
    {
        var builder = new SqliteConnectionStringBuilder(_connectionString);
        return builder.DataSource;
    }

    private static int ParseMigrationVersion(string fileName)
    {
        var prefix = fileName.Split('_')[0];
        return int.Parse(prefix);
    }

    private static async Task EnsureMigrationTableAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                version     INTEGER PRIMARY KEY,
                name        TEXT NOT NULL,
                applied_at  TEXT NOT NULL DEFAULT (datetime('now'))
            );
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<HashSet<int>> GetAppliedVersionsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var versions = new HashSet<int>();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT version FROM schema_migrations ORDER BY version;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            versions.Add(reader.GetInt32(0));
        }

        return versions;
    }

    private static async Task ExecuteScriptAsync(
        SqliteConnection connection,
        string script,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = script;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
