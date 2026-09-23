namespace TissueTrainer.Core.Models;

public sealed class CategoryProgress
{
    public string CategoryId { get; set; } = string.Empty;
    public int Attempts { get; set; }
    public int Correct { get; set; }
    public double Accuracy => Attempts == 0 ? 0 : (double)Correct / Attempts;
    public DateTimeOffset? LastSeenAt { get; set; }
    public MasteryStatus Status { get; set; } = MasteryStatus.Learning;
    public double RecentMissRate { get; set; }
}

public sealed class UserQuestionHistory
{
    public string QuestionId { get; set; } = string.Empty;
    public int Attempts { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
    public bool? LastCorrect { get; set; }
}

public sealed class AdaptiveSelectionConfig
{
    public int SessionSize { get; set; } = 15;
    public double WeakShare { get; set; } = 0.70;
    public double ReviewShare { get; set; } = 0.20;
    public double NewShare { get; set; } = 0.10;
    public double WeakAccuracyThreshold { get; set; } = 0.75;
    public int ReviewMinDays { get; set; } = 7;
    public double ReviewMinAccuracy { get; set; } = 0.75;
    public double ReviewMaxAccuracy { get; set; } = 0.90;
    public int WeakCategoryLimit { get; set; } = 5;
}

public sealed class AdaptiveSelectionRequest
{
    public SessionMode Mode { get; set; } = SessionMode.Training;
    public IReadOnlyList<QuestionDefinition> AvailableQuestions { get; set; } = Array.Empty<QuestionDefinition>();
    public IReadOnlyList<CategoryProgress> CategoryProgress { get; set; } = Array.Empty<CategoryProgress>();
    public IReadOnlyList<UserQuestionHistory> QuestionHistory { get; set; } = Array.Empty<UserQuestionHistory>();
    public IReadOnlyList<string> ExcludeQuestionIds { get; set; } = Array.Empty<string>();
    public AdaptiveSelectionConfig Config { get; set; } = new();
    public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AdaptiveSelectionResult
{
    public IReadOnlyList<QuestionDefinition> SelectedQuestions { get; init; } = Array.Empty<QuestionDefinition>();
    public IReadOnlyDictionary<string, int> BucketCounts { get; init; } = new Dictionary<string, int>();
}
