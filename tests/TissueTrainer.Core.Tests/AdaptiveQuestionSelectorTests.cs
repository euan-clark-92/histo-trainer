using TissueTrainer.Core.Models;
using TissueTrainer.Core.Services;
using Xunit;

namespace TissueTrainer.Core.Tests;

public class AdaptiveQuestionSelectorTests
{
    private readonly AdaptiveQuestionSelector _selector = new();

    [Fact]
    public void SelectQuestions_TrainingMode_PrioritizesWeakCategories()
    {
        var questions = CreateQuestions();
        var request = new AdaptiveSelectionRequest
        {
            Mode = SessionMode.Training,
            AvailableQuestions = questions,
            CategoryProgress = new List<CategoryProgress>
            {
                new()
                {
                    CategoryId = "lung_resp_epithelium",
                    Attempts = 12,
                    Correct = 4,
                    Status = MasteryStatus.Weak,
                    RecentMissRate = 0.7
                },
                new()
                {
                    CategoryId = "colonic_mucosa",
                    Attempts = 10,
                    Correct = 9,
                    Status = MasteryStatus.Mastered,
                    RecentMissRate = 0.1
                }
            },
            QuestionHistory = new List<UserQuestionHistory>
            {
                new() { QuestionId = "q_0001", Attempts = 2 }
            },
            Config = new AdaptiveSelectionConfig
            {
                SessionSize = 5,
                WeakShare = 0.8,
                ReviewShare = 0.1,
                NewShare = 0.1
            }
        };

        var result = _selector.SelectQuestions(request);

        Assert.Equal(5, result.SelectedQuestions.Count);
        Assert.True(result.SelectedQuestions.Count(q =>
            q.Categories.Contains("lung_resp_epithelium")) >= 3);
        Assert.True(result.BucketCounts["weak"] >= 3);
    }

    [Fact]
    public void SelectQuestions_WeakMode_UsesOnlyWeakCategoriesWhenAvailable()
    {
        var questions = CreateQuestions();
        var request = new AdaptiveSelectionRequest
        {
            Mode = SessionMode.Weak,
            AvailableQuestions = questions,
            CategoryProgress = new List<CategoryProgress>
            {
                new()
                {
                    CategoryId = "squamous_metaplasia",
                    Attempts = 8,
                    Correct = 3,
                    Status = MasteryStatus.Weak,
                    RecentMissRate = 0.6
                }
            },
            Config = new AdaptiveSelectionConfig { SessionSize = 3 }
        };

        var result = _selector.SelectQuestions(request);

        Assert.Equal(3, result.SelectedQuestions.Count);
        Assert.All(result.SelectedQuestions, q =>
            Assert.Contains("squamous_metaplasia", q.Categories));
    }

    [Fact]
    public void SelectQuestions_DoesNotRepeatExcludedQuestions()
    {
        var questions = CreateQuestions();
        var request = new AdaptiveSelectionRequest
        {
            Mode = SessionMode.Manual,
            AvailableQuestions = questions,
            ExcludeQuestionIds = new[] { "q_0001", "q_0002" },
            Config = new AdaptiveSelectionConfig { SessionSize = 3 }
        };

        var result = _selector.SelectQuestions(request);

        Assert.Equal(3, result.SelectedQuestions.Count);
        Assert.DoesNotContain(result.SelectedQuestions, q => q.Id is "q_0001" or "q_0002");
    }

    [Fact]
    public void CalculateWeaknessScore_IncreasesWithLowAccuracyAndMissRate()
    {
        var weak = new CategoryProgress
        {
            Attempts = 10,
            Correct = 3,
            RecentMissRate = 0.6,
            LastSeenAt = DateTimeOffset.UtcNow.AddDays(-14)
        };

        var strong = new CategoryProgress
        {
            Attempts = 10,
            Correct = 9,
            RecentMissRate = 0.1,
            LastSeenAt = DateTimeOffset.UtcNow.AddDays(-1)
        };

        var now = DateTimeOffset.UtcNow;
        var weakScore = _selector.CalculateWeaknessScore(weak, now);
        var strongScore = _selector.CalculateWeaknessScore(strong, now);

        Assert.True(weakScore > strongScore);
    }

    private static IReadOnlyList<QuestionDefinition> CreateQuestions()
    {
        return new List<QuestionDefinition>
        {
            CreateQuestion("q_0001", "lung_resp_epithelium"),
            CreateQuestion("q_0002", "squamous_metaplasia"),
            CreateQuestion("q_0003", "liver_lobule"),
            CreateQuestion("q_0004", "colonic_mucosa"),
            CreateQuestion("q_0005", "skin_epidermis"),
            CreateQuestion("q_0006", "smooth_muscle"),
            CreateQuestion("q_0007", "lung_resp_epithelium"),
            CreateQuestion("q_0008", "colonic_mucosa")
        };
    }

    private static QuestionDefinition CreateQuestion(string id, string categoryId)
    {
        return new QuestionDefinition
        {
            Id = id,
            Prompt = $"Question {id}",
            Options = new[] { "A", "B", "C", "D" },
            CorrectIndex = 0,
            Categories = new[] { categoryId },
            Active = true
        };
    }
}
