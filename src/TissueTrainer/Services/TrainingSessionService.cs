using TissueTrainer.Core.Models;
using TissueTrainer.Models;

namespace TissueTrainer.Services;

public sealed class TrainingSessionService
{
    private readonly AppHost _host;

    public TrainingSessionService(AppHost host)
    {
        _host = host;
    }

    public async Task<QuizSessionContext> StartSessionAsync(
        SessionMode mode,
        CancellationToken cancellationToken = default)
    {
        var sessionSize = mode switch
        {
            SessionMode.Weak => _host.Settings.WeakSessionSize,
            SessionMode.Assessment => _host.Settings.AssessmentSessionSize,
            _ => _host.Settings.TrainingSessionSize
        };

        var categoryProgress = await _host.ProgressRepository.GetCategoryProgressAsync(
            _host.CurrentUserId,
            cancellationToken);
        var questionHistory = await _host.ProgressRepository.GetQuestionHistoryAsync(
            _host.CurrentUserId,
            cancellationToken);

        var request = new AdaptiveSelectionRequest
        {
            Mode = mode,
            AvailableQuestions = _host.ContentPack.Questions,
            CategoryProgress = categoryProgress,
            QuestionHistory = questionHistory,
            Config = new AdaptiveSelectionConfig { SessionSize = sessionSize },
            Now = DateTimeOffset.UtcNow
        };

        var selection = _host.QuestionSelector.SelectQuestions(request);
        if (selection.SelectedQuestions.Count == 0)
        {
            throw new InvalidOperationException("No questions available for this session.");
        }

        var sessionId = await _host.SessionRepository.CreateSessionAsync(
            _host.CurrentUserId,
            mode,
            cancellationToken);

        return new QuizSessionContext
        {
            SessionId = sessionId,
            Mode = mode,
            Questions = selection.SelectedQuestions.ToList(),
            ShowImmediateFeedback = mode != SessionMode.Assessment
        };
    }
}
