using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TissueTrainer.Core.Models;
using TissueTrainer.Models;
using TissueTrainer.Services;

namespace TissueTrainer.Pages;

public sealed partial class QuizSessionPage : Page
{
    private QuizSessionContext _context = new();
    private readonly List<QuizAnswerRecord> _answers = new();
    private readonly Stopwatch _sessionStopwatch = new();
    private int _currentIndex;
    private int? _selectedOptionIndex;
    private DateTimeOffset _questionStartedAt;
    private bool _isAnswerSubmitted;

    public QuizSessionPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is not QuizSessionContext context)
        {
            Frame.Navigate(typeof(HomePage));
            return;
        }

        _context = context;
        _currentIndex = 0;
        _answers.Clear();
        _sessionStopwatch.Restart();
        await LoadCurrentQuestionAsync();
    }

    private async Task LoadCurrentQuestionAsync()
    {
        _isAnswerSubmitted = false;
        _selectedOptionIndex = null;
        SubmitButton.Visibility = Visibility.Visible;
        NextButton.Visibility = Visibility.Collapsed;
        FeedbackPanel.Visibility = Visibility.Collapsed;
        SubmitButton.IsEnabled = false;

        var question = _context.Questions[_currentIndex];
        var total = _context.Questions.Count;

        ProgressText.Text = $"Question {_currentIndex + 1} of {total}";
        CategoryText.Text = string.Join(" · ", question.Categories.Select(AppServices.Host.GetCategoryLabel));
        PromptText.Text = question.Prompt;
        SessionProgressBar.Value = total == 0 ? 0 : (double)_currentIndex / total * 100;

        BuildOptionButtons(question);
        await ImageViewer.LoadImageAsync(AppServices.Host.GetImagePath(question));
        _questionStartedAt = DateTimeOffset.UtcNow;
        FooterStatusText.Text = _context.ShowImmediateFeedback
            ? "Select an answer and submit."
            : "Assessment mode: feedback will appear at the end.";
    }

    private void BuildOptionButtons(QuestionDefinition question)
    {
        OptionsPanel.Children.Clear();

        for (var i = 0; i < question.Options.Count; i++)
        {
            var index = i;
            var radio = new RadioButton
            {
                Content = question.Options[i],
                GroupName = "QuizOptions",
                Tag = index
            };
            radio.Checked += (_, _) =>
            {
                if (!_isAnswerSubmitted)
                {
                    _selectedOptionIndex = index;
                    SubmitButton.IsEnabled = true;
                }
            };
            OptionsPanel.Children.Add(radio);
        }
    }

    private async void SubmitButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedOptionIndex is null || _isAnswerSubmitted)
        {
            return;
        }

        _isAnswerSubmitted = true;
        var question = _context.Questions[_currentIndex];
        var selectedIndex = _selectedOptionIndex.Value;
        var isCorrect = selectedIndex == question.CorrectIndex;
        var responseMs = (int)(DateTimeOffset.UtcNow - _questionStartedAt).TotalMilliseconds;

        _answers.Add(new QuizAnswerRecord
        {
            Question = question,
            SelectedIndex = selectedIndex,
            Correct = isCorrect
        });

        await AppServices.Host.ProgressRepository.RecordAttemptAsync(
            AppServices.Host.CurrentUserId,
            _context.SessionId,
            question.Id,
            isCorrect,
            responseMs,
            _context.Mode,
            question.Categories);

        if (_context.ShowImmediateFeedback)
        {
            ShowFeedback(question, selectedIndex, isCorrect);
            SubmitButton.Visibility = Visibility.Collapsed;
            NextButton.Visibility = Visibility.Visible;
        }
        else
        {
            await AdvanceOrFinishAsync();
        }
    }

    private void ShowFeedback(QuestionDefinition question, int selectedIndex, bool isCorrect)
    {
        FeedbackPanel.Visibility = Visibility.Visible;
        FeedbackResultText.Text = isCorrect ? "Correct" : "Incorrect";
        FeedbackResultText.Foreground = new SolidColorBrush(
            isCorrect ? Windows.UI.Color.FromArgb(255, 16, 124, 16) : Windows.UI.Color.FromArgb(255, 196, 43, 28));

        FeedbackAnswerText.Text = $"Correct answer: {question.Options[question.CorrectIndex]}";
        FeedbackExplanationText.Text = question.Explanation;
        FeaturesList.ItemsSource = question.Features;

        if (question.ConfusableWith.Count > 0)
        {
            var labels = question.ConfusableWith.Select(AppServices.Host.GetCategoryLabel);
            ConfusionText.Text = "Commonly confused with: " + string.Join(", ", labels);
            ConfusionText.Visibility = Visibility.Visible;
        }
        else
        {
            ConfusionText.Visibility = Visibility.Collapsed;
        }

        HighlightOptions(question, selectedIndex, isCorrect);
        DisableOptionSelection();
    }

    private void HighlightOptions(QuestionDefinition question, int selectedIndex, bool isCorrect)
    {
        foreach (var radio in OptionsPanel.Children.OfType<RadioButton>())
        {
            if (radio.Tag is not int index)
            {
                continue;
            }

            if (index == question.CorrectIndex)
            {
                radio.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 124, 16));
            }
            else if (index == selectedIndex && !isCorrect)
            {
                radio.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 196, 43, 28));
            }
        }
    }

    private void DisableOptionSelection()
    {
        foreach (var radio in OptionsPanel.Children.OfType<RadioButton>())
        {
            radio.IsEnabled = false;
        }
    }

    private async void NextButton_Click(object sender, RoutedEventArgs e) =>
        await AdvanceOrFinishAsync();

    private async Task AdvanceOrFinishAsync()
    {
        if (_currentIndex >= _context.Questions.Count - 1)
        {
            await FinishSessionAsync();
            return;
        }

        _currentIndex += 1;
        await LoadCurrentQuestionAsync();
    }

    private async void EndSessionButton_Click(object sender, RoutedEventArgs e) =>
        await FinishSessionAsync();

    private async Task FinishSessionAsync()
    {
        _sessionStopwatch.Stop();
        var correctCount = _answers.Count(a => a.Correct);

        await AppServices.Host.SessionRepository.CompleteSessionAsync(
            _context.SessionId,
            _answers.Count,
            correctCount);

        var weakCategories = _answers
            .Where(a => !a.Correct)
            .SelectMany(a => a.Question.Categories)
            .GroupBy(id => id)
            .OrderByDescending(g => g.Count())
            .Take(3)
            .Select(g => AppServices.Host.GetCategoryLabel(g.Key))
            .ToList();

        var result = new QuizSessionResult
        {
            Mode = _context.Mode,
            TotalQuestions = _context.Questions.Count,
            CorrectCount = correctCount,
            Duration = _sessionStopwatch.Elapsed,
            WeakCategories = weakCategories,
            Answers = _answers.ToList()
        };

        Frame.Navigate(typeof(QuizSummaryPage), result);
    }
}
