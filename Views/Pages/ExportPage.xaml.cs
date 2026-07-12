// ============================================================================
// ExportPage.xaml.cs — 数据导出页面的代码后台 (Code-Behind)
// ============================================================================
// 功能说明：
//   数据导出页面，允许用户将待办事项导出为文件（如 JSON / CSV 格式）。
//   提供两个操作：
//   - 预览：在页面上生成导出内容的预览
//   - 导出：弹出文件保存对话框，将内容写入文件
//
// WPF 知识点：
//   - async void 在事件处理程序中是合法的（事件回调是 async void 的唯一合理使用场景）
//   - ExecuteAsync 是 CommunityToolkit.Mvvm 的 AsyncRelayCommand 提供的异步执行方法
// ============================================================================

using System.Windows;
using System.Windows.Controls;
using KiteTodo.ViewModels;

namespace KiteTodo.Views.Pages;

/// <summary>
/// 数据导出页面，支持预览和导出待办数据到文件。
/// </summary>
public partial class ExportPage : Page
{
    /// <summary>导出功能的 ViewModel 实例</summary>
    private readonly ExportViewModel _vm = new();

    public ExportPage()
    {
        InitializeComponent();  // 加载 ExportPage.xaml
        DataContext = _vm;       // 绑定 ViewModel
    }

    /// <summary>点击"预览"按钮，生成导出内容预览（同步操作）</summary>
    private void OnPreview(object sender, RoutedEventArgs e) => _vm.GeneratePreviewCommand.Execute(null);

    /// <summary>
    /// 点击"导出"按钮，异步执行文件导出操作。
    /// 注意：事件回调使用 async void 是 C# 中唯一允许的 async void 场景，
    /// 因为事件签名必须返回 void。
    /// </summary>
    private async void OnExport(object sender, RoutedEventArgs e) => await _vm.ExportToFileCommand.ExecuteAsync(null);

    /// <summary>点击"备份数据"按钮</summary>
    private async void OnBackup(object sender, RoutedEventArgs e) => await _vm.BackupToFileCommand.ExecuteAsync(null);

    /// <summary>点击"恢复数据"按钮，先弹确认框</summary>
    private async void OnRestore(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "恢复数据将覆盖当前所有数据（待办、笔记、番茄钟记录、设置），确认继续？",
            "确认恢复",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
            await _vm.RestoreFromFileCommand.ExecuteAsync(null);
    }

    /// <summary>备份笔记本 Pro 文件</summary>
    private async void OnBackupNotesPro(object sender, RoutedEventArgs e)
        => await _vm.BackupNotesProCommand.ExecuteAsync(null);

    /// <summary>恢复笔记本 Pro 文件</summary>
    private async void OnRestoreNotesPro(object sender, RoutedEventArgs e)
        => await _vm.RestoreNotesProCommand.ExecuteAsync(null);

    /// <summary>迁移旧笔记到新笔记本</summary>
    private void OnMigrateOldNotes(object sender, RoutedEventArgs e)
        => _vm.MigrateOldNotesCommand.Execute(null);

    /// <summary>打开笔记本 Pro 目录</summary>
    private void OnOpenNotesProFolder(object sender, RoutedEventArgs e)
        => _vm.OpenNotesProFolderCommand.Execute(null);
}
