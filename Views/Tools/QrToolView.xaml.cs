using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KiteTodo.Services;
using Microsoft.Win32;
using QRCoder;
using ZXing;
using ZXing.Common;

namespace KiteTodo.Views.Tools;

public partial class QrToolView : UserControl
{
    private readonly QRCodeGenerator _qrCodeGenerator = new();
    private BitmapSource? _generatedQrBitmap;
    private BitmapSource? _logoBitmap;
    private BitmapSource? _parseBitmap;

    public QrToolView()
    {
        InitializeComponent();
        UpdateGeneratedPreview(null);
        UpdateParsePreview(null);
    }

    private void OnGeneratorInputTextChanged(object sender, TextChangedEventArgs e)
    {
        RegenerateQrCode();
    }

    private void OnPickLogo(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择中心 Logo",
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|所有文件|*.*"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            _logoBitmap = LoadBitmapFromFile(dialog.FileName);
            LogoPathTextBlock.Text = dialog.FileName;
            RegenerateQrCode();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Logo 加载失败：{ex.Message}", "二维码生成", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnClearLogo(object sender, RoutedEventArgs e)
    {
        _logoBitmap = null;
        LogoPathTextBlock.Text = "未选择 Logo";
        RegenerateQrCode();
    }

    private void OnCopyQrImage(object sender, RoutedEventArgs e)
    {
        if (_generatedQrBitmap == null)
            return;

        Clipboard.SetImage(_generatedQrBitmap);
        AlertService.ShowCornerToast("二维码已复制到剪贴板", 2);
    }

    private void OnSaveQrImage(object sender, RoutedEventArgs e)
    {
        if (_generatedQrBitmap == null)
            return;

        var dialog = new SaveFileDialog
        {
            Title = "保存二维码图片",
            Filter = "PNG 图片|*.png",
            FileName = $"qrcode-{DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.png"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            SaveBitmapAsPng(_generatedQrBitmap, dialog.FileName);
            AlertService.ShowCornerToast("二维码已保存", 2);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败：{ex.Message}", "二维码生成", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnClearGenerator(object sender, RoutedEventArgs e)
    {
        GeneratorInputTextBox.Clear();
        _logoBitmap = null;
        LogoPathTextBlock.Text = "未选择 Logo";
        UpdateGeneratedPreview(null);
    }

    private void OnPickParseImage(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择二维码图片",
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|所有文件|*.*"
        };

        if (dialog.ShowDialog() != true)
            return;

        LoadAndParseImage(dialog.FileName);
    }

    private void OnParseCurrentImage(object sender, RoutedEventArgs e)
    {
        if (_parseBitmap == null)
            return;

        DecodeCurrentParseBitmap();
    }

    private void OnCopyParsedText(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ParsedResultTextBox.Text))
            return;

        Clipboard.SetText(ParsedResultTextBox.Text);
        AlertService.ShowCornerToast("解析结果已复制", 2);
    }

    private void OnClearParseArea(object sender, RoutedEventArgs e)
    {
        ParsedResultTextBox.Clear();
        ParseImagePathTextBlock.Text = "未选择图片";
        UpdateParsePreview(null);
    }

    private void OnParseImageDragEnter(object sender, DragEventArgs e)
    {
        UpdateDragState(e);
    }

    private void OnParseImageDragOver(object sender, DragEventArgs e)
    {
        UpdateDragState(e);
    }

    private void OnParseImageDragLeave(object sender, DragEventArgs e)
    {
        ParseDropZoneBorder.BorderBrush = CreateBrush("#C9D6E5");
        ParseDropZoneBorder.Background = CreateBrush("#F8FBFE");
    }

    private void OnParseImageDrop(object sender, DragEventArgs e)
    {
        ParseDropZoneBorder.BorderBrush = CreateBrush("#C9D6E5");
        ParseDropZoneBorder.Background = CreateBrush("#F8FBFE");

        if (!TryGetDraggedImagePath(e, out var imagePath))
            return;

        LoadAndParseImage(imagePath);
    }

    private void RegenerateQrCode()
    {
        var text = GeneratorInputTextBox.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            UpdateGeneratedPreview(null);
            return;
        }

        try
        {
            using var qrData = _qrCodeGenerator.CreateQrCode(text, _logoBitmap == null ? QRCodeGenerator.ECCLevel.Q : QRCodeGenerator.ECCLevel.H);
            var qrCode = new PngByteQRCode(qrData);
            var pngBytes = qrCode.GetGraphic(18);
            var qrBitmap = LoadBitmapFromBytes(pngBytes);

            _generatedQrBitmap = _logoBitmap == null ? qrBitmap : ComposeQrWithLogo(qrBitmap, _logoBitmap);
            UpdateGeneratedPreview(_generatedQrBitmap);
        }
        catch (Exception ex)
        {
            _generatedQrBitmap = null;
            UpdateGeneratedPreview(null);
            MessageBox.Show($"二维码生成失败：{ex.Message}", "二维码生成", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void LoadAndParseImage(string filePath)
    {
        try
        {
            _parseBitmap = LoadBitmapFromFile(filePath);
            ParseImagePathTextBlock.Text = filePath;
            UpdateParsePreview(_parseBitmap);
            DecodeCurrentParseBitmap();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"图片加载失败：{ex.Message}", "二维码解析", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void DecodeCurrentParseBitmap()
    {
        if (_parseBitmap == null)
            return;

        try
        {
            var result = DecodeQrCode(_parseBitmap);
            ParsedResultTextBox.Text = string.IsNullOrWhiteSpace(result)
                ? "未识别到二维码内容"
                : result;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"二维码解析失败：{ex.Message}", "二维码解析", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void UpdateGeneratedPreview(BitmapSource? bitmap)
    {
        _generatedQrBitmap = bitmap;
        GeneratedQrImage.Source = bitmap;
        GeneratedQrPlaceholderText.Visibility = bitmap == null ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateParsePreview(BitmapSource? bitmap)
    {
        _parseBitmap = bitmap;
        ParsePreviewImage.Source = bitmap;
        ParsePreviewPlaceholderText.Visibility = bitmap == null ? Visibility.Visible : Visibility.Collapsed;
    }

    private static BitmapSource ComposeQrWithLogo(BitmapSource qrBitmap, BitmapSource logoBitmap)
    {
        var size = Math.Min(qrBitmap.PixelWidth, qrBitmap.PixelHeight);
        var logoBoxSize = Math.Max(56, (int)(size * 0.22));
        var padding = Math.Max(6, logoBoxSize / 8);
        var safeBoxSize = logoBoxSize + padding * 2;

        var scale = Math.Min((double)logoBoxSize / logoBitmap.PixelWidth, (double)logoBoxSize / logoBitmap.PixelHeight);
        var renderedLogoWidth = logoBitmap.PixelWidth * scale;
        var renderedLogoHeight = logoBitmap.PixelHeight * scale;
        var centerX = qrBitmap.PixelWidth / 2.0;
        var centerY = qrBitmap.PixelHeight / 2.0;

        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawImage(qrBitmap, new Rect(0, 0, qrBitmap.PixelWidth, qrBitmap.PixelHeight));
            context.DrawRoundedRectangle(Brushes.White, null,
                new Rect(centerX - safeBoxSize / 2.0, centerY - safeBoxSize / 2.0, safeBoxSize, safeBoxSize), 16, 16);
            context.DrawImage(logoBitmap,
                new Rect(centerX - renderedLogoWidth / 2.0, centerY - renderedLogoHeight / 2.0, renderedLogoWidth, renderedLogoHeight));
        }

        var target = new RenderTargetBitmap(qrBitmap.PixelWidth, qrBitmap.PixelHeight, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        target.Freeze();
        return target;
    }

    private static string? DecodeQrCode(BitmapSource bitmap)
    {
        var formatted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        var stride = formatted.PixelWidth * 4;
        var pixels = new byte[stride * formatted.PixelHeight];
        formatted.CopyPixels(pixels, stride, 0);

        var luminance = new RGBLuminanceSource(pixels, formatted.PixelWidth, formatted.PixelHeight, RGBLuminanceSource.BitmapFormat.BGRA32);
        var reader = new BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = new DecodingOptions
            {
                TryHarder = true,
                PossibleFormats = [BarcodeFormat.QR_CODE]
            }
        };

        return reader.Decode(luminance)?.Text;
    }

    private static BitmapImage LoadBitmapFromFile(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        memory.Position = 0;
        return LoadBitmapFromStream(memory);
    }

    private static BitmapImage LoadBitmapFromBytes(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return LoadBitmapFromStream(stream);
    }

    private static BitmapImage LoadBitmapFromStream(Stream stream)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static void SaveBitmapAsPng(BitmapSource bitmap, string filePath)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = File.Create(filePath);
        encoder.Save(stream);
    }

    private void UpdateDragState(DragEventArgs e)
    {
        if (TryGetDraggedImagePath(e, out _))
        {
            e.Effects = DragDropEffects.Copy;
            ParseDropZoneBorder.BorderBrush = CreateBrush("#3B82F6");
            ParseDropZoneBorder.Background = CreateBrush("#EFF6FF");
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private static bool TryGetDraggedImagePath(DragEventArgs e, out string imagePath)
    {
        imagePath = string.Empty;
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return false;

        var files = e.Data.GetData(DataFormats.FileDrop) as string[];
        var candidate = files?.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(candidate) || !IsSupportedImageFile(candidate))
            return false;

        imagePath = candidate;
        return true;
    }

    private static bool IsSupportedImageFile(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp";
    }

    private static SolidColorBrush CreateBrush(string hex)
    {
        return (SolidColorBrush)new BrushConverter().ConvertFrom(hex)!;
    }
}