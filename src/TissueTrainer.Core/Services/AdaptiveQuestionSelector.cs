using TissueTrainer.Core.Models;

namespace TissueTrainer.Core.Services;

public sealed class AdaptiveQuestionSelector
{
    public AdaptiveSelectionResult SelectQuestions(AdaptiveSelectionRequest request)
    {
        var eligible = request.AvailableQuestions
            .Where(q => q.Active)
            .Where(q => !request.ExcludeQuestionIds.Contains(q.Id, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (eligible.Count == 0)
        {
            return new AdaptiveSelectionResult();
        }

        return request.Mode switch
        {
            SessionMode.Weak => BuildWeakSession(request, eligible),
            SessionMode.Assessment => BuildAssessmentSession(request, eligible),
            SessionMode.Manual => BuildManualSession(request, eligible),
            _ => BuildTrainingSession(request, eligible)
        };
    }

    private AdaptiveSelectionResult BuildTrainingSession(
        AdaptiveSelectionRequest request,
        List<QuestionDefinition> eligible)
    {
        var config = request.Config;
        var sessionSize = Math.Min(config.SessionSize, eligible.Count);
        var historyByQuestion = request.QuestionHistory.ToDictionary(h => h.QuestionId, StringComparer.OrdinalIgnoreCase);
        var progressByCategory = request.CategoryProgress.ToDictionary(p => p.CategoryId, StringComparer.OrdinalIgnoreCase);

        var weakTarget = (int)Math.Round(sessionSize * config.WeakShare);
        var reviewTarget = (int)Math.Round(sessionSize * config.ReviewShare);
        var newTarget = Math.Max(0, sessionSize - weakTarget - reviewTarget);

        var weakCategories = GetWeakCategories(progressByCategory, config)
            .Take(config.WeakCategoryLimit)
            .Select(c => c.CategoryId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var weakPool = FilterByCategories(eligible, weakCategories);
        var reviewPool = eligible
            .Where(q => IsReviewCandidate(q, progressByCategory, historyByQuestion, request.Now, config))
            .ToList();
        var newPool = eligible
            .Where(q => !HasBeenSeen(q.Id, historyByQuestion))
            .ToList();

        var selected = new List<QuestionDefinition>(sessionSize);
        var bucketCounts = new Dictionary<string, int>
        {
            ["weak"] = 0,
            ["review"] = 0,
            ["new"] = 0,
            ["fallback"] = 0
        };

        TakeFromPool(selected, weakPool, weakTarget, sessionSize, bucketCounts, "weak");
        TakeFromPool(selected, reviewPool, reviewTarget, sessionSize, bucketCounts, "review");
        TakeFromPool(selected, newPool, newTarget, sessionSize, bucketCounts, "new");

        if (selected.Count < sessionSize)
        {
            var fallback = eligible
                .Where(q => selected.All(s => s.Id != q.Id))
                .OrderBy(_ => Random.Shared.Next())
                .ToList();

            TakeFromPool(selected, fallback, sessionSize - selected.Count, sessionSize, bucketCounts, "fallback");
        }

        return new AdaptiveSelectionResult
        {
            SelectedQuestions = selected,
            BucketCounts = bucketCounts
        };
    }

    private AdaptiveSelectionResult BuildWeakSession(
        AdaptiveSelectionRequest request,
        List<QuestionDefinition> eligible)
    {
        var sessionSize = Math.Min(request.Config.SessionSize, eligible.Count);
        var progressByCategory = request.CategoryProgress.ToDictionary(p => p.CategoryId, StringComparer.OrdinalIgnoreCase);

        var weakCategories = progressByCategory.Values
            .Where(p => p.Status == MasteryStatus.Weak)
            .OrderByDescending(p => CalculateWeaknessScore(p, request.Now))
            .Select(p => p.CategoryId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (weakCategories.Count == 0)
        {
            weakCategories = GetWeakCategories(progressByCategory, request.Config)
                .Take(3)
                .Select(c => c.CategoryId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        var pool = FilterByCategories(eligible, weakCategories)
            .OrderByDescending(q => ScoreQuestionForWeakness(q, progressByCategory, request.Now))
            .ThenBy(_ => Random.Shared.Next())
            .Take(sessionSize)
            .ToList();

        if (pool.Count < sessionSize)
        {
            var remainder = eligible
                .Where(q => pool.All(p => p.Id != q.Id))
                .OrderBy(_ => Random.Shared.Next())
                .Take(sessionSize - pool.Count);
            pool.AddRange(remainder);
        }

        return new AdaptiveSelectionResult
        {
            SelectedQuestions = pool,
            BucketCounts = new Dictionary<string, int> { ["weak"] = pool.Count }
        };
    }

    private static AdaptiveSelectionResult BuildAssessmentSession(
        AdaptiveSelectionRequest request,
        List<QuestionDefinition> eligible)
    {
        var sessionSize = Math.Min(request.Config.SessionSize, eligible.Count);
        var selected = eligible
            .OrderBy(_ => Random.Shared.Next())
            .Take(sessionSize)
            .ToList();

        return new AdaptiveSelectionResult
        {
            SelectedQuestions = selected,
            BucketCounts = new Dictionary<string, int> { ["assessment"] = selected.Count }
        };
    }

    private static AdaptiveSelectionResult BuildManualSession(
        AdaptiveSelectionRequest request,
        List<QuestionDefinition> eligible)
    {
        var sessionSize = Math.Min(request.Config.SessionSize, eligible.Count);
        var selected = eligible
            .OrderBy(_ => Random.Shared.Next())
            .Take(sessionSize)
            .ToList();

        return new AdaptiveSelectionResult
        {
            SelectedQuestions = selected,
            BucketCounts = new Dictionary<string, int> { ["manual"] = selected.Count }
        };
    }

    private static IEnumerable<CategoryProgress> GetWeakCategories(
        IReadOnlyDictionary<string, CategoryProgress> progressByCategory,
        AdaptiveSelectionConfig config)
    {
        return progressByCategory.Values
            .OrderByDescending(p => CalculateWeaknessScoreStatic(p))
            .Where(p =>
                p.Status == MasteryStatus.Weak ||
                (p.Attempts >= 5 && p.Accuracy < config.WeakAccuracyThreshold));
    }

    public double CalculateWeaknessScore(CategoryProgress progress, DateTimeOffset now)
    {
        var recencyBoost = 0.0;
        if (progress.LastSeenAt.HasValue)
        {
            var days = Math.Max(0, (now - progress.LastSeenAt.Value).TotalDays);
            recencyBoost = (days / 30.0) * 0.2;
        }

        var attemptFactor = Math.Min(progress.Attempts, 20) / 20.0;
        return ((1.0 - progress.Accuracy) * attemptFactor)
               + recencyBoost
               + (progress.RecentMissRate * 0.3);
    }

    private static double ScoreQuestionForWeakness(
        QuestionDefinition question,
        IReadOnlyDictionary<string, CategoryProgress> progressByCategory,
        DateTimeOffset now)
    {
        return question.Categories
            .Where(progressByCategory.ContainsKey)
            .Select(categoryId => progressByCategory[categoryId])
            .DefaultIfEmpty(new CategoryProgress())
            .Max(p => CalculateWeaknessScoreStatic(p, now));
    }

    private static double CalculateWeaknessScoreStatic(CategoryProgress progress, DateTimeOffset now = default)
    {
        var recencyBoost = 0.0;
        if (progress.LastSeenAt.HasValue && now != default)
        {
            var days = Math.Max(0, (now - progress.LastSeenAt.Value).TotalDays);
            recencyBoost = (days / 30.0) * 0.2;
        }

        var attemptFactor = Math.Min(progress.Attempts, 20) / 20.0;
        return ((1.0 - progress.Accuracy) * attemptFactor)
               + recencyBoost
               + (progress.RecentMissRate * 0.3);
    }

    private static bool HasBeenSeen(string questionId, IReadOnlyDictionary<string, UserQuestionHistory> historyByQuestion)
    {
        return historyByQuestion.ContainsKey(questionId);
    }

    private static bool IsReviewCandidate(
        QuestionDefinition question,
        IReadOnlyDictionary<string, CategoryProgress> progressByCategory,
        IReadOnlyDictionary<string, UserQuestionHistory> historyByQuestion,
        DateTimeOffset now,
        AdaptiveSelectionConfig config)
    {
        if (!HasBeenSeen(question.Id, historyByQuestion))
        {
            return false;
        }

        foreach (var categoryId in question.Categories)
        {
            if (!progressByCategory.TryGetValue(categoryId, out var progress))
            {
                continue;
            }

            if (progress.Accuracy < config.ReviewMinAccuracy || progress.Accuracy > config.ReviewMaxAccuracy)
            {
                continue;
            }

            if (!progress.LastSeenAt.HasValue)
            {
                continue;
            }

            var days = (now - progress.LastSeenAt.Value).TotalDays;
            if (days >= config.ReviewMinDays)
            {
                return true;
            }
        }

        return false;
    }

    private static List<QuestionDefinition> FilterByCategories(
        IEnumerable<QuestionDefinition> questions,
        IReadOnlySet<string> categoryIds)
    {
        if (categoryIds.Count == 0)
        {
            return questions.ToList();
        }

        return questions
            .Where(q => q.Categories.Any(categoryIds.Contains))
            .ToList();
    }

    private static void TakeFromPool(
        List<QuestionDefinition> selected,
        List<QuestionDefinition> pool,
        int target,
        int sessionSize,
        Dictionary<string, int> bucketCounts,
        string bucketName)
    {
        if (target <= 0 || pool.Count == 0)
        {
            return;
        }

        foreach (var question in pool.OrderBy(_ => Random.Shared.Next()))
        {
            if (selected.Count >= sessionSize || bucketCounts[bucketName] >= target)
            {
                break;
            }

            if (selected.Any(s => s.Id == question.Id))
            {
                continue;
            }

            selected.Add(question);
            bucketCounts[bucketName] += 1;
        }
    }
}
