using Microsoft.UI.Xaml;
using TissueTrainer.Pages;
using TissueTrainer.Services;

namespace TissueTrainer;

public sealed partial class MainWindow : Window
{
    private bool _isInitialized;

    public MainWindow()
    {
        InitializeComponent();

        var appWindow = AppWindow;
        if (appWindow is not null)
        {
            appWindow.Resize(new Windows.Graphics.SizeInt32(1280, 800));
        }

        Activated += OnActivated;
    }

    private async void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;

        try
        {
            await AppServices.Host.InitializeAsync();
            RootFrame.Navigate(typeof(HomePage));
        }
        catch (Exception ex)
        {
            var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
            {
                Title = "Startup failed",
                Content = ex.Message,
                CloseButtonText = "Close",
                XamlRoot = Content.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }
}
