using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TissueTrainer.Core.Models;
using TissueTrainer.Services;

namespace TissueTrainer.Pages;

public sealed partial class HomePage : Page
{
    public HomePage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var host = AppServices.Host;
        ContentVersionText.Text = $"Content {host.ContentPack.Manifest.Version} · {host.ContentPack.Questions.Count} questions";
        StatusText.Text = $"Training as user #{host.CurrentUserId}";
    }

    private async void StartTrainingButton_Click(object sender, RoutedEventArgs e) =>
        await StartSessionAsync(SessionMode.Training);

    private async void StartWeakButton_Click(object sender, RoutedEventArgs e) =>
        await StartSessionAsync(SessionMode.Weak);

    private async void StartAssessmentButton_Click(object sender, RoutedEventArgs e) =>
        await StartSessionAsync(SessionMode.Assessment);

    private async Task StartSessionAsync(SessionMode mode)
    {
        try
        {
            SetButtonsEnabled(false);
            var context = await AppServices.Host.TrainingSessionService.StartSessionAsync(mode);
            Frame.Navigate(typeof(QuizSessionPage), context);
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
        finally
        {
            SetButtonsEnabled(true);
        }
    }

    private async void ReloadContentButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await AppServices.Host.ReloadContentAsync();
            OnLoaded(this, new RoutedEventArgs());
            StatusText.Text = "Content pack reloaded.";
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private void SetButtonsEnabled(bool enabled)
    {
        StartTrainingButton.IsEnabled = enabled;
        StartWeakButton.IsEnabled = enabled;
        StartAssessmentButton.IsEnabled = enabled;
        ReloadContentButton.IsEnabled = enabled;
    }
}
