using Microsoft.Data.Sqlite;
using TissueTrainer.Core.Models;

namespace TissueTrainer.Data.Repositories;

public sealed class ProgressRepository
{
    private readonly string _connectionString;

    public ProgressRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IReadOnlyList<CategoryProgress>> GetCategoryProgressAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT category_id, attempts, correct, accuracy, last_seen_at, status
            FROM category_stats
            WHERE user_id = $userId;
            """;

        var results = new List<CategoryProgress>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$userId", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new CategoryProgress
            {
                CategoryId = reader.GetString(0),
                Attempts = reader.GetInt32(1),
                Correct = reader.GetInt32(2),
                LastSeenAt = reader.IsDBNull(4) ? null : DateTimeOffset.Parse(reader.GetString(4)),
                Status = Enum.Parse<MasteryStatus>(reader.GetString(5), ignoreCase: true)
            });
        }

        return results;
    }

    public async Task<IReadOnlyList<UserQuestionHistory>> GetQuestionHistoryAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT question_id,
                   COUNT(*) AS attempts,
                   MAX(answered_at) AS last_attempt_at,
                   (
                       SELECT correct
                       FROM attempts a2
                       WHERE a2.user_id = attempts.user_id
                         AND a2.question_id = attempts.question_id
                       ORDER BY answered_at DESC
                       LIMIT 1
                   ) AS last_correct
            FROM attempts
            WHERE user_id = $userId
            GROUP BY question_id;
            """;

        var results = new List<UserQuestionHistory>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$userId", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new UserQuestionHistory
            {
                QuestionId = reader.GetString(0),
                Attempts = reader.GetInt32(1),
                LastAttemptAt = reader.IsDBNull(2) ? null : DateTimeOffset.Parse(reader.GetString(2)),
                LastCorrect = reader.IsDBNull(3) ? null : reader.GetInt32(3) == 1
            });
        }

        return results;
    }

    public async Task RecordAttemptAsync(
        long userId,
        long sessionId,
        string questionId,
        bool correct,
        int? responseMs,
        SessionMode mode,
        IEnumerable<string> categoryIds,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await InsertAttemptAsync(connection, userId, sessionId, questionId, correct, responseMs, mode, cancellationToken);
            foreach (var categoryId in categoryIds.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                await UpsertCategoryStatAsync(connection, userId, categoryId, correct, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task InsertAttemptAsync(
        SqliteConnection connection,
        long userId,
        long sessionId,
        string questionId,
        bool correct,
        int? responseMs,
        SessionMode mode,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO attempts (user_id, session_id, question_id, correct, response_ms, mode)
            VALUES ($userId, $sessionId, $questionId, $correct, $responseMs, $mode);
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$userId", userId);
        command.Parameters.AddWithValue("$sessionId", sessionId);
        command.Parameters.AddWithValue("$questionId", questionId);
        command.Parameters.AddWithValue("$correct", correct ? 1 : 0);
        command.Parameters.AddWithValue("$responseMs", (object?)responseMs ?? DBNull.Value);
        command.Parameters.AddWithValue("$mode", mode.ToString().ToLowerInvariant());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpsertCategoryStatAsync(
        SqliteConnection connection,
        long userId,
        string categoryId,
        bool correct,
        CancellationToken cancellationToken)
    {
        const string selectSql = """
            SELECT attempts, correct
            FROM category_stats
            WHERE user_id = $userId AND category_id = $categoryId;
            """;

        await using var selectCommand = connection.CreateCommand();
        selectCommand.CommandText = selectSql;
        selectCommand.Parameters.AddWithValue("$userId", userId);
        selectCommand.Parameters.AddWithValue("$categoryId", categoryId);

        var attempts = 0;
        var correctCount = 0;

        await using (var reader = await selectCommand.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                attempts = reader.GetInt32(0);
                correctCount = reader.GetInt32(1);
            }
        }

        attempts += 1;
        if (correct)
        {
            correctCount += 1;
        }

        var accuracy = attempts == 0 ? 0 : (double)correctCount / attempts;
        var status = attempts >= 10 && accuracy >= 0.90
            ? "mastered"
            : attempts >= 5 && accuracy < 0.75
                ? "weak"
                : "learning";

        const string upsertSql = """
            INSERT INTO category_stats (user_id, category_id, attempts, correct, accuracy, last_seen_at, status)
            VALUES ($userId, $categoryId, $attempts, $correctCount, $accuracy, datetime('now'), $status)
            ON CONFLICT(user_id, category_id) DO UPDATE SET
                attempts = excluded.attempts,
                correct = excluded.correct,
                accuracy = excluded.accuracy,
                last_seen_at = excluded.last_seen_at,
                status = excluded.status;
            """;

        await using var upsertCommand = connection.CreateCommand();
        upsertCommand.CommandText = upsertSql;
        upsertCommand.Parameters.AddWithValue("$userId", userId);
        upsertCommand.Parameters.AddWithValue("$categoryId", categoryId);
        upsertCommand.Parameters.AddWithValue("$attempts", attempts);
        upsertCommand.Parameters.AddWithValue("$correctCount", correctCount);
        upsertCommand.Parameters.AddWithValue("$accuracy", accuracy);
        upsertCommand.Parameters.AddWithValue("$status", status);
        await upsertCommand.ExecuteNonQueryAsync(cancellationToken);
    }
}
