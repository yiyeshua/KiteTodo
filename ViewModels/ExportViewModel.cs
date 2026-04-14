// ============================================================================
// ExportViewModel.cs - 导出页面 ViewModel
// 提供按日期范围导出待办数据为 Markdown 或纯文本的功能
// ============================================================================

using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KiteTodo.Services;

namespace KiteTodo.ViewModels;

/// <summary>
/// 导出页面 ViewModel，用户选择日期范围和格式后，
/// 可预览导出内容或保存为文件。
/// </summary>
public partial class ExportViewModel : ObservableObject
{
    private readonly ExportService _exportService = new();

    /// <summary>导出起始日期（默认一周前）</summary>
    [ObservableProperty]
    private DateTime _startDate = DateTime.Today.AddDays(-7);

    /// <summary>导出结束日期（默认今天）</summary>
    [ObservableProperty]
    private DateTime _endDate = DateTime.Today;

    /// <summary>是否使用 Markdown 格式（false 则为纯文本）</summary>
    [ObservableProperty]
    private bool _isMarkdownFormat = true;

    /// <summary>预览文本内容（显示在页面的文本框中）</summary>
    [ObservableProperty]
    private string _previewText = string.Empty;

    /// <summary>生成预览（不保存文件，只更新预览文本）</summary>
    [RelayCommand]
    private void GeneratePreview()
    {
        PreviewText = IsMarkdownFormat
            ? _exportService.ExportToMarkdown(StartDate, EndDate)
            : _exportService.ExportToText(StartDate, EndDate);
    }

    /// <summary>导出到文件（弹出系统保存文件对话框）</summary>
    [RelayCommand]
    private async Task ExportToFile()
    {
        GeneratePreview();

        // 使用 Windows 系统自带的保存文件对话框
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = $"KiteTodo_{StartDate:yyyyMMdd}_{EndDate:yyyyMMdd}",
            Filter = IsMarkdownFormat
                ? "Markdown files (*.md)|*.md|All files (*.*)|*.*"
                : "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            DefaultExt = IsMarkdownFormat ? ".md" : ".txt"
        };

        if (dialog.ShowDialog() == true)
        {
            await File.WriteAllTextAsync(dialog.FileName, PreviewText, System.Text.Encoding.UTF8);
        }
    }
}
