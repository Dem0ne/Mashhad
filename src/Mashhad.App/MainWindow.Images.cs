using System.Globalization;
using Mashhad.Core.Imaging;
using Mashhad.Core.Media;
using Mashhad.Editing;
using Microsoft.UI.Xaml;

namespace Mashhad.App;

public sealed partial class MainWindow
{
    private ImageView _view = ImageView.Fit;
    private int _userRotation;
    private bool _cropMode;
    private (int X, int Y) _cropAnchor;
    private PixelRect _cropRect;

    /// <summary>Current window and displayed-image sizes in physical pixels, straight from mpv.</summary>
    private Viewport GetViewport()
    {
        if (_mpv is null || _host is null) return default;
        long w = _mpv.GetLong("video-params/dw") ?? _mpv.GetLong("video-params/w") ?? 0;
        long h = _mpv.GetLong("video-params/dh") ?? _mpv.GetLong("video-params/h") ?? 0;
        int fileRotation = (int)(_mpv.GetLong("video-params/rotate") ?? 0);
        var (rw, rh) = CropMath.RotatedSize((int)w, (int)h, fileRotation + _userRotation);
        return new Viewport(_host.Width, _host.Height, rw, rh);
    }

    private void ApplyView(ImageView view)
    {
        _view = view;
        if (_mpv is null) return;
        _mpv.Set("video-zoom", view.Zoom);
        _mpv.Set("video-pan-x", view.PanX);
        _mpv.Set("video-pan-y", view.PanY);
        UpdateZoomText();
        if (_cropMode) DrawCropOverlay();
    }

    private void UpdateZoomText()
    {
        if (!_kind.IsImageLike() || _queue.Current is null) return;
        var vp = GetViewport();
        ZoomButton.Content = vp.IsValid
            ? Math.Round(_view.Percent(vp)).ToString(CultureInfo.InvariantCulture) + "%"
            : "100%";
    }

    private void OnViewportResized()
    {
        if (!_kind.IsImageLike()) return;
        var vp = GetViewport();
        if (vp.IsValid && _view != ImageView.Fit) ApplyView(_view.Clamp(vp));
        else UpdateZoomText();
    }

    private void ZoomBy(double delta)
    {
        if (!_kind.IsImageLike()) return;
        ApplyView(_view.ZoomBy(GetViewport(), delta));
    }

    private void ZoomAt(double delta, int x, int y)
    {
        if (!_kind.IsImageLike()) return;
        ApplyView(_view.ZoomAt(GetViewport(), delta, x, y));
    }

    private void ZoomFit()
    {
        if (_kind.IsImageLike()) ApplyView(ImageView.Fit);
    }

    private void ZoomActual()
    {
        if (!_kind.IsImageLike()) return;
        var vp = GetViewport();
        if (vp.IsValid) ApplyView(ImageView.ActualSize(vp));
    }

    private void PanBy(double dx, double dy)
    {
        if (!_kind.IsImageLike()) return;
        ApplyView(_view.PanBy(GetViewport(), dx, dy));
    }

    private void Rotate(int degrees)
    {
        if (_kind != MediaKind.Image || _mpv is null) return;
        if (_cropMode) EndCrop();
        _userRotation = CropMath.NormalizeRotation(_userRotation + degrees);
        _mpv.Set("video-rotate", _userRotation.ToString(CultureInfo.InvariantCulture));
        ApplyView(ImageView.Fit);
        SaveImageButton.IsEnabled = _userRotation != 0;
    }

    // ---- crop -----------------------------------------------------------------------------

    private void BeginCrop()
    {
        if (_kind != MediaKind.Image || _mpv is null || !_fileReady) return;
        _cropMode = true;
        _cropRect = default;
        CropBar.Visibility = Visibility.Visible;
        CropSizeText.Text = "";
        if (_isFullscreen) ShowFullscreenChrome();
    }

    private void EndCrop()
    {
        _cropMode = false;
        _cropRect = default;
        CropBar.Visibility = Visibility.Collapsed;
        _mpv?.TryCommand("osd-overlay", "1", "none", "");
        SaveImageButton.IsEnabled = _userRotation != 0;
    }

    private void CropStart(int x, int y)
    {
        _cropAnchor = (x, y);
        _cropRect = default;
        DrawCropOverlay();
    }

    private void CropDrag(int x, int y)
    {
        var vp = GetViewport();
        _cropRect = CropMath.WindowToImage(vp, _view, _cropAnchor.X, _cropAnchor.Y, x, y);
        CropSizeText.Text = _cropRect.IsEmpty ? "" : $"{_cropRect.Width} × {_cropRect.Height}";
        DrawCropOverlay();
    }

    private void DrawCropOverlay()
    {
        if (_mpv is null || _host is null) return;
        if (_cropRect.IsEmpty)
        {
            _mpv.TryCommand("osd-overlay", "1", "none", "");
            return;
        }
        var vp = GetViewport();
        var rect = CropMath.ImageToWindow(vp, _view, _cropRect);
        string ass = CropMath.BuildOverlayAss(_host.Width, _host.Height, rect);
        _mpv.TryCommand("osd-overlay", "1", "ass-events", ass,
            _host.Width.ToString(CultureInfo.InvariantCulture), _host.Height.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>Writes the rotated and/or cropped image as a new file next to the original.</summary>
    private async Task SaveImageAsync()
    {
        var item = _queue.Current;
        if (item is null || _kind != MediaKind.Image) return;
        PixelRect? crop = _cropMode && !_cropRect.IsEmpty ? _cropRect : null;
        if (crop is null && _userRotation == 0)
        {
            ShowStatus(Loc.T(_cropMode ? "Crop.SelectFirst" : "Image.NothingToSave"));
            return;
        }
        if (!EnsureTools(out var runner)) return;

        // JPEG stays JPEG (re-encoded at high quality); everything else is written losslessly as PNG.
        var format = FfmpegArgs.FormatForExtension(item.Path) == FrameFormat.Jpeg ? FrameFormat.Jpeg : FrameFormat.Png;
        string output = ExportPlanner.DerivedPath(item.Path, crop is null ? "rotated" : "cropped",
            FfmpegArgs.ExtensionFor(format), NullIfEmpty(Settings.ExportDirectory));
        var args = FfmpegArgs.TransformImage(item.Path, output, _userRotation, crop, format);

        await RunToolAsync(Loc.T("Status.SavingImage"), (progress, ct) => runner.RunAsync(args, 0, progress, ct), output);
        if (_cropMode) EndCrop();
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => ZoomBy(0.25);
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => ZoomBy(-0.25);
    private void ZoomFit_Click(object sender, RoutedEventArgs e) => ZoomFit();
    private void ZoomActual_Click(object sender, RoutedEventArgs e) => ZoomActual();
    private void RotateCw_Click(object sender, RoutedEventArgs e) => Rotate(90);
    private void RotateCcw_Click(object sender, RoutedEventArgs e) => Rotate(-90);
    private void Crop_Click(object sender, RoutedEventArgs e) { if (_cropMode) EndCrop(); else BeginCrop(); }
    private void CropApply_Click(object sender, RoutedEventArgs e) => _ = SaveImageAsync();
    private void CropCancel_Click(object sender, RoutedEventArgs e) => EndCrop();
    private void SaveImage_Click(object sender, RoutedEventArgs e) => _ = SaveImageAsync();
}
