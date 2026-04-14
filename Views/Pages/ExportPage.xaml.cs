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
}
