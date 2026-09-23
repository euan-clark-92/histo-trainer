using Microsoft.Data.Sqlite;
using TissueTrainer.Core.Models;

namespace TissueTrainer.Data.Repositories;

public sealed class SessionRepository
{
    private readonly string _connectionString;

    public SessionRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<long> CreateSessionAsync(
        long userId,
        SessionMode mode,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO sessions (user_id, mode)
            VALUES ($userId, $mode);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$userId", userId);
        command.Parameters.AddWithValue("$mode", mode.ToString().ToLowerInvariant());

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result);
    }

    public async Task CompleteSessionAsync(
        long sessionId,
        int questionCount,
        int correctCount,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE sessions
            SET completed_at = datetime('now'),
                question_count = $questionCount,
                correct_count = $correctCount
            WHERE id = $sessionId;
            """;
        command.Parameters.AddWithValue("$sessionId", sessionId);
        command.Parameters.AddWithValue("$questionCount", questionCount);
        command.Parameters.AddWithValue("$correctCount", correctCount);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
