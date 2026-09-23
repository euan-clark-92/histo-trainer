using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;

namespace TissueTrainer.Controls;

public sealed partial class HistologyImageViewer : UserControl
{
    private const float MinZoom = 0.25f;
    private const float MaxZoom = 4.0f;
    private float _zoomFactor = 1.0f;
    private TranslateTransform? _panTransform;

    public HistologyImageViewer()
    {
        InitializeComponent();
        ResetZoom();
    }

    public async Task LoadImageAsync(string imagePath)
    {
        ResetZoom();

        if (!File.Exists(imagePath))
        {
            SlideImage.Source = null;
            ZoomLevelText.Text = "Image not found";
            return;
        }

        var bitmap = new BitmapImage();
        await bitmap.SetSourceAsync(await Windows.Storage.FileIO.OpenReadAsync(
            await Windows.Storage.StorageFile.GetFileFromPathAsync(imagePath)));

        SlideImage.Source = bitmap;
        UpdateZoomText();
    }

    public void ResetZoom()
    {
        _zoomFactor = 1.0f;
        _panTransform = new TranslateTransform();
        var group = new TransformGroup();
        group.Children.Add(new ScaleTransform { ScaleX = _zoomFactor, ScaleY = _zoomFactor });
        group.Children.Add(_panTransform);
        SlideImage.RenderTransform = group;
        SlideImage.RenderTransformOrigin = new Point(0.5, 0.5);
        ImageScrollViewer.ChangeView(0, 0, 1.0f, disableAnimation: true);
        UpdateZoomText();
    }

    private void ZoomInButton_Click(object sender, RoutedEventArgs e) => ApplyZoom(_zoomFactor * 1.25f);

    private void ZoomOutButton_Click(object sender, RoutedEventArgs e) => ApplyZoom(_zoomFactor / 1.25f);

    private void ResetZoomButton_Click(object sender, RoutedEventArgs e) => ResetZoom();

    private void ImageScrollViewer_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var properties = e.GetCurrentPoint(ImageScrollViewer).Properties;
        if (properties.IsHorizontalMouseWheel || !properties.IsMouseWheel)
        {
            return;
        }

        var delta = properties.MouseWheelDelta;
        var factor = delta > 0 ? 1.1f : 0.9f;
        ApplyZoom(_zoomFactor * factor);
        e.Handled = true;
    }

    private void SlideImage_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
    {
        if (_panTransform is null || _zoomFactor <= 1.0f)
        {
            return;
        }

        _panTransform.X += e.Delta.Translation.X;
        _panTransform.Y += e.Delta.Translation.Y;
    }

    private void ApplyZoom(float targetZoom)
    {
        _zoomFactor = Math.Clamp(targetZoom, MinZoom, MaxZoom);
        if (SlideImage.RenderTransform is TransformGroup group &&
            group.Children.FirstOrDefault() is ScaleTransform scale)
        {
            scale.ScaleX = _zoomFactor;
            scale.ScaleY = _zoomFactor;
        }

        if (_zoomFactor <= 1.0f && _panTransform is not null)
        {
            _panTransform.X = 0;
            _panTransform.Y = 0;
        }

        UpdateZoomText();
    }

    private void UpdateZoomText()
    {
        ZoomLevelText.Text = $"{_zoomFactor:P0}";
    }
}
