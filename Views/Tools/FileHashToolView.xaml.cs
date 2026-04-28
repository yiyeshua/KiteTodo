using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KiteTodo.Services;
using Microsoft.Win32;

namespace KiteTodo.Views.Tools;

public partial class FileHashToolView : UserControl
{
    private string? _selectedFilePath;
    private bool _isCalculating;
    private readonly Dictionary<string, string> _currentHashes = new(StringComparer.OrdinalIgnoreCase);

    public FileHashToolView()
    {
        InitializeComponent();
        ClearHashOutputs();
    }

    private async void OnPickFile(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择要计算摘要的文件",
            Filter = "所有文件|*.*"
        };

        if (dialog.ShowDialog() != true)
            return;

        await LoadFileAndCalculateAsync(dialog.FileName);
    }

    private async void OnRecalculate(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedFilePath))
            return;

        await CalculateHashesAsync(_selectedFilePath);
    }

    private void OnCopySingleHash(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string tag })
            return;

        var text = tag switch
        {
            "MD5" => Md5TextBox.Text,
            "SHA1" => Sha1TextBox.Text,
            "SHA256" => Sha256TextBox.Text,
            "SHA384" => Sha384TextBox.Text,
            "SHA512" => Sha512TextBox.Text,
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(text) || text == "等待计算")
            return;

        Clipboard.SetText(text);
        AlertService.ShowCornerToast($"{tag} 已复制", 2);
    }

    private void OnCopyAll(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedFilePath))
            return;

        var builder = new StringBuilder();
        builder.AppendLine($"文件: {_selectedFilePath}");
        builder.AppendLine($"MD5: {Md5TextBox.Text}");
        builder.AppendLine($"SHA1: {Sha1TextBox.Text}");
        builder.AppendLine($"SHA256: {Sha256TextBox.Text}");
        builder.AppendLine($"SHA384: {Sha384TextBox.Text}");
        builder.AppendLine($"SHA512: {Sha512TextBox.Text}");
        Clipboard.SetText(builder.ToString());
        AlertService.ShowCornerToast("全部摘要已复制", 2);
    }

    private void OnOutputCaseChanged(object sender, RoutedEventArgs e)
    {
        ApplyHashOutputs();
    }

    private void OnFileDragEnter(object sender, DragEventArgs e)
    {
        UpdateDragState(e);
    }

    private void OnFileDragOver(object sender, DragEventArgs e)
    {
        UpdateDragState(e);
    }

    private void OnFileDragLeave(object sender, DragEventArgs e)
    {
        RestoreDropZoneVisual();
    }

    private async void OnFileDrop(object sender, DragEventArgs e)
    {
        RestoreDropZoneVisual();

        if (!TryGetDraggedFilePath(e, out var filePath))
            return;

        await LoadFileAndCalculateAsync(filePath);
    }

    private async Task LoadFileAndCalculateAsync(string filePath)
    {
        _selectedFilePath = filePath;
        SelectedFilePathTextBlock.Text = filePath;

        var fileInfo = new FileInfo(filePath);
        FileNameTextBlock.Text = $"文件名：{fileInfo.Name}";
        FileSizeTextBlock.Text = $"大小：{FormatFileSize(fileInfo.Length)}";

        await CalculateHashesAsync(filePath);
    }

    private async Task CalculateHashesAsync(string filePath)
    {
        if (_isCalculating)
            return;

        if (!File.Exists(filePath))
        {
            MessageBox.Show("目标文件不存在，可能已被移动或删除。", "文件摘要校验", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _isCalculating = true;
        try
        {
            StatusTextBlock.Text = "状态：计算中...";
            SetHashOutputs("计算中...");

            var result = await Task.Run(() => new Dictionary<string, string>
            {
                ["MD5"] = ComputeHash(filePath, MD5.Create()),
                ["SHA1"] = ComputeHash(filePath, SHA1.Create()),
                ["SHA256"] = ComputeHash(filePath, SHA256.Create()),
                ["SHA384"] = ComputeHash(filePath, SHA384.Create()),
                ["SHA512"] = ComputeHash(filePath, SHA512.Create())
            });

            _currentHashes.Clear();
            foreach (var pair in result)
                _currentHashes[pair.Key] = pair.Value;

            ApplyHashOutputs();
            StatusTextBlock.Text = "状态：计算完成";
        }
        catch (Exception ex)
        {
            ClearHashOutputs();
            StatusTextBlock.Text = "状态：计算失败";
            MessageBox.Show($"文件摘要计算失败：{ex.Message}", "文件摘要校验", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _isCalculating = false;
        }
    }

    private static string ComputeHash(string filePath, HashAlgorithm algorithm)
    {
        using (algorithm)
        {
            using var stream = File.OpenRead(filePath);
            var hashBytes = algorithm.ComputeHash(stream);
            return Convert.ToHexString(hashBytes);
        }
    }

    private void SetHashOutputs(string value)
    {
        Md5TextBox.Text = value;
        Sha1TextBox.Text = value;
        Sha256TextBox.Text = value;
        Sha384TextBox.Text = value;
        Sha512TextBox.Text = value;
    }

    private void ClearHashOutputs()
    {
        _currentHashes.Clear();
        SetHashOutputs("等待计算");
    }

    private void ApplyHashOutputs()
    {
        if (_currentHashes.Count == 0)
            return;

        Md5TextBox.Text = FormatHashValue(_currentHashes, "MD5");
        Sha1TextBox.Text = FormatHashValue(_currentHashes, "SHA1");
        Sha256TextBox.Text = FormatHashValue(_currentHashes, "SHA256");
        Sha384TextBox.Text = FormatHashValue(_currentHashes, "SHA384");
        Sha512TextBox.Text = FormatHashValue(_currentHashes, "SHA512");
    }

    private string FormatHashValue(IReadOnlyDictionary<string, string> hashes, string key)
    {
        if (!hashes.TryGetValue(key, out var value))
            return "等待计算";

        return LowercaseOutputCheckBox.IsChecked == true
            ? value.ToLowerInvariant()
            : value;
    }

    private void UpdateDragState(DragEventArgs e)
    {
        if (!TryGetDraggedFilePath(e, out _))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
        FileDropZoneBorder.BorderBrush = CreateBrush("#60A5FA");
        FileDropZoneBorder.Background = CreateBrush("#EEF6FF");
    }

    private void RestoreDropZoneVisual()
    {
        FileDropZoneBorder.BorderBrush = (Brush)FindResource("CustomSubtleBorder");
        FileDropZoneBorder.Background = (Brush)FindResource("CustomSubtleBg");
    }

    private static bool TryGetDraggedFilePath(DragEventArgs e, out string filePath)
    {
        filePath = string.Empty;
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return false;

        var files = e.Data.GetData(DataFormats.FileDrop) as string[];
        if (files == null || files.Length == 0)
            return false;

        var candidate = files[0];
        if (string.IsNullOrWhiteSpace(candidate) || Directory.Exists(candidate))
            return false;

        filePath = candidate;
        return true;
    }

    private static Brush CreateBrush(string hex)
    {
        return (Brush)new BrushConverter().ConvertFrom(hex)!;
    }

    private static string FormatFileSize(long length)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = length;
        var unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return string.Format(CultureInfo.InvariantCulture, "{0:0.##} {1}", size, units[unitIndex]);
    }
}