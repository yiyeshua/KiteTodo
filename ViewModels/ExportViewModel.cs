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
    private readonly BackupService _backupService = new();

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

    /// <summary>备份/恢复操作的状态提示</summary>
    [ObservableProperty]
    private string _backupStatusMessage = string.Empty;

    /// <summary>笔记本 Pro 备份状态</summary>
    [ObservableProperty]
    private string _notesProStatusMessage = string.Empty;

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

    /// <summary>备份全部数据到 JSON 文件</summary>
    [RelayCommand]
    private async Task BackupToFile()
    {
        var json = _backupService.ExportToJson();
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = $"KiteTodo_Backup_{DateTime.Now:yyyyMMdd_HHmmss}",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = ".json"
        };
        if (dialog.ShowDialog() == true)
        {
            await File.WriteAllTextAsync(dialog.FileName, json, System.Text.Encoding.UTF8);
            BackupStatusMessage = $"备份成功：{dialog.FileName}";
        }
    }

    /// <summary>从 JSON 文件恢复全部数据</summary>
    [RelayCommand]
    private async Task RestoreFromFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = ".json"
        };
        if (dialog.ShowDialog() != true) return;

        var json = await File.ReadAllTextAsync(dialog.FileName, System.Text.Encoding.UTF8);
        var (success, message) = _backupService.ImportFromJson(json);
        BackupStatusMessage = message;
    }

    /// <summary>备份笔记本 Pro 数据（复制 notes 目录）</summary>
    [RelayCommand]
    private async Task BackupNotesPro()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = $"KiteTodo_NotesPro_{DateTime.Now:yyyyMMdd_HHmmss}",
            Filter = "All files (*.*)|*.*",
            DefaultExt = ""
        };
        if (dialog.ShowDialog() != true) return;

        // 使用选中的目录作为备份目标
        var destDir = Path.GetDirectoryName(dialog.FileName)!;
        await Task.Run(() =>
        {
            var (success, message) = _backupService.BackupNotesDirectory(destDir);
            NotesProStatusMessage = message;
        });
    }

    /// <summary>恢复笔记本 Pro 数据</summary>
    [RelayCommand]
    private async Task RestoreNotesPro()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择笔记本 Pro 备份目录",
            Filter = "All files (*.*)|*.*",
            CheckFileExists = false,
            CheckPathExists = true
        };
        if (dialog.ShowDialog() != true) return;

        // 获取选中的目录路径
        var srcDir = Path.GetDirectoryName(dialog.FileName)!;
        var result = System.Windows.MessageBox.Show(
            $"将从以下目录恢复笔记本 Pro 数据：\n{srcDir}\n\n当前数据将被覆盖，确认继续？",
            "确认恢复", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            await Task.Run(() =>
            {
                var (success, message) = _backupService.RestoreNotesDirectory(srcDir);
                NotesProStatusMessage = message;
            });
        }
    }

    /// <summary>将旧笔记本数据（LiteDB）迁移到笔记本 Pro（.md 文件）</summary>
    [RelayCommand]
    private void MigrateOldNotes()
    {
        var result = System.Windows.MessageBox.Show(
            "将旧笔记本中的所有笔记转换为 .md 文件，保存到笔记本 Pro 目录。\n\n原 LiteDB 数据不会被删除，确认继续？",
            "迁移旧笔记", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            var (success, skipped, message) = _backupService.MigrateOldNotesToFileSystem();
            NotesProStatusMessage = message;
        }
    }

    /// <summary>打开笔记本 Pro 数据目录</summary>
    [RelayCommand]
    private void OpenNotesProFolder()
    {
        var path = BackupService.GetNotesProPath();
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }
}
