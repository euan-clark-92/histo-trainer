using TissueTrainer.Core.Models;

namespace TissueTrainer.Models;

public sealed class QuizSessionContext
{
    public long SessionId { get; init; }
    public SessionMode Mode { get; init; }
    public IReadOnlyList<QuestionDefinition> Questions { get; init; } = Array.Empty<QuestionDefinition>();
    public bool ShowImmediateFeedback { get; init; } = true;
}

public sealed class QuizSessionResult
{
    public SessionMode Mode { get; init; }
    public int TotalQuestions { get; init; }
    public int CorrectCount { get; init; }
    public double ScorePercent => TotalQuestions == 0 ? 0 : (double)CorrectCount / TotalQuestions * 100;
    public TimeSpan Duration { get; init; }
    public IReadOnlyList<string> WeakCategories { get; init; } = Array.Empty<string>();
    public IReadOnlyList<QuizAnswerRecord> Answers { get; init; } = Array.Empty<QuizAnswerRecord>();
}

public sealed class QuizAnswerRecord
{
    public QuestionDefinition Question { get; init; } = new();
    public int SelectedIndex { get; init; }
    public bool Correct { get; init; }
}
