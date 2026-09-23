using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TissueTrainer.Core.Models;
using TissueTrainer.Models;
using TissueTrainer.Services;

namespace TissueTrainer.Pages;

public sealed partial class QuizSummaryPage : Page
{
    private QuizSessionResult _result = new();

    public QuizSummaryPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is not QuizSessionResult result)
        {
            Frame.Navigate(typeof(HomePage));
            return;
        }

        _result = result;
        ScoreText.Text = $"{result.CorrectCount} / {result.TotalQuestions} correct ({result.ScorePercent:F0}%)";
        DurationText.Text = $"Duration: {result.Duration:mm\\:ss}";
        ModeText.Text = $"Mode: {result.Mode}";

        if (result.WeakCategories.Count > 0)
        {
            WeakAreasText.Text = "Recommended practice: " + string.Join(", ", result.WeakCategories);
        }
        else
        {
            WeakAreasText.Text = "No weak categories identified this session. Great work.";
        }

        if (result.Mode == SessionMode.Assessment)
        {
            AssessmentReviewPanel.Visibility = Visibility.Visible;
            ReviewList.ItemsSource = result.Answers.Select(answer => new ReviewItem
            {
                Title = $"{(answer.Correct ? "Correct" : "Incorrect")}: {answer.Question.Prompt}",
                Detail = answer.Correct
                    ? answer.Question.Explanation
                    : $"Your answer: {answer.Question.Options[answer.SelectedIndex]}{Environment.NewLine}Correct: {answer.Question.Options[answer.Question.CorrectIndex]}{Environment.NewLine}{answer.Question.Explanation}"
            }).ToList();
        }
    }

    private async void PracticeWeakButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var context = await AppServices.Host.TrainingSessionService.StartSessionAsync(SessionMode.Weak);
            Frame.Navigate(typeof(QuizSessionPage), context);
        }
        catch (Exception ex)
        {
            WeakAreasText.Text = ex.Message;
        }
    }

    private void BackHomeButton_Click(object sender, RoutedEventArgs e) =>
        Frame.Navigate(typeof(HomePage));

    private sealed class ReviewItem
    {
        public string Title { get; init; } = string.Empty;
        public string Detail { get; init; } = string.Empty;
    }
}
