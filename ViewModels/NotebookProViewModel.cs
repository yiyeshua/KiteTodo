// ============================================================================
// NotebookProViewModel.cs - 笔记本 Pro 页面 ViewModel
// 基于 WebView2 嵌入 AINote 前端，提供完整的 Markdown/纯文本编辑体验
// ============================================================================

using CommunityToolkit.Mvvm.ComponentModel;

namespace KiteTodo.ViewModels;

/// <summary>
/// 笔记本 Pro 页面 ViewModel。
/// 实际编辑功能由 WebView2 中的 AINote 前端 + NoteAppBridgeService 桥接提供。
/// </summary>
public partial class NotebookProViewModel : ObservableObject
{
    [ObservableProperty]
    private string _statusText = "就绪";

    [ObservableProperty]
    private bool _isLoading = true;

    public NotebookProViewModel()
    {
    }
}
