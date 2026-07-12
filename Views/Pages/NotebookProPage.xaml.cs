// ============================================================================
// NotebookProPage.xaml.cs - 笔记本 Pro 代码后台
//
// 使用 WebView2 加载 AINote React 前端，通过 NoteAppBridgeService 桥接
// 后端文件操作。Tauri IPC 调用被 webview-bridge.js 拦截并转为 WebView2
// postMessage，由本页面的 WebMessageReceived 处理并返回结果。
// ============================================================================

using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using KiteTodo.Services;
using KiteTodo.ViewModels;
using Microsoft.Web.WebView2.Core;

namespace KiteTodo.Views.Pages;

/// <summary>
/// 笔记本 Pro 页面，基于 WebView2 + AINote 前端提供 Markdown/纯文本编辑。
/// </summary>
public partial class NotebookProPage : Page
{
    private static NotebookProPage? _activeInstance;

    private readonly NoteAppBridgeService _bridge = new();
    private readonly NotebookProViewModel _vm = new();
    private bool _webViewInitialized;

    /// <summary>
    /// 应用退出时检查是否有未保存的内容
    /// </summary>
    public static bool ConfirmPendingChangesForActivePage()
    {
        // 目前 AINote 前端自行管理保存状态，暂不需要拦截
        return true;
    }

    public NotebookProPage()
    {
        InitializeComponent();
        DataContext = _vm;
    }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        _activeInstance = this;

        if (_webViewInitialized)
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            NoteAppWebView.Visibility = Visibility.Visible;
            return;
        }

        _webViewInitialized = true;
        await InitializeWebViewAsync();
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        if (_activeInstance == this)
            _activeInstance = null;
    }

    /// <summary>
    /// 初始化 WebView2 环境并加载 AINote 前端
    /// </summary>
    private async Task InitializeWebViewAsync()
    {
        try
        {
            // 1. 确保 WebView2 运行时可用
            await NoteAppWebView.EnsureCoreWebView2Async();

            // 2. 配置 WebView2 设置
            NoteAppWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            NoteAppWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            NoteAppWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            NoteAppWebView.CoreWebView2.Settings.IsSwipeNavigationEnabled = false;

            // 3. 接管新窗口请求（在默认浏览器打开外部链接）
            NoteAppWebView.CoreWebView2.NewWindowRequested += (sender, args) =>
            {
                args.Handled = true;
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = args.Uri,
                        UseShellExecute = true
                    });
                }
                catch { /* 忽略 */ }
            };

            // 4. 定位/解压 NoteApp 资源目录
            var noteAppDir = await EnsureNoteAppResourcesAsync();

            // 5. 设置虚拟主机映射
            NoteAppWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "noteapp.local", noteAppDir, CoreWebView2HostResourceAccessKind.DenyCors);

            // 5. 映射工作目录虚拟主机（用于图片等本地文件访问）
            var workDir = _bridge.WorkDir;
            if (Directory.Exists(workDir))
            {
                NoteAppWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "kitenote.local", workDir, CoreWebView2HostResourceAccessKind.Allow);
            }

            // 6. 监听来自前端的 IPC 消息
            NoteAppWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

            // 7. 加载 AINote 前端
            NoteAppWebView.CoreWebView2.Navigate("https://noteapp.local/index.html");

            // 8. 等待页面加载完成后隐藏遮罩
            NoteAppWebView.NavigationCompleted += (sender, args) =>
            {
                Dispatcher.Invoke(() =>
                {
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                    NoteAppWebView.Visibility = Visibility.Visible;
                    _vm.IsLoading = false;
                    _vm.StatusText = "就绪";
                });
            };
        }
        catch (Exception ex)
        {
            LoadingHintText.Text = $"加载失败：{ex.Message}";
        }
    }

    /// <summary>
    /// 处理来自 AINote 前端（通过 webview-bridge.js）的 IPC 消息
    /// </summary>
    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var rawMessage = e.TryGetWebMessageAsString();
            if (string.IsNullOrEmpty(rawMessage))
                return;

            var response = await _bridge.HandleMessageAsync(rawMessage);
            if (response != null)
            {
                NoteAppWebView.CoreWebView2.PostWebMessageAsJson(response);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NotebookProPage] IPC error: {ex.Message}");
        }
    }

    /// <summary>
    /// 确保 NoteApp 资源目录存在。开发环境用本地目录，发布版从嵌入 ZIP 解压到缓存。
    /// </summary>
    private static async Task<string> EnsureNoteAppResourcesAsync()
    {
        // 1. 开发环境（dotnet run）：优先本地目录
        var devPaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "NoteApp"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "Resources", "NoteApp")
        };
        foreach (var devPath in devPaths)
        {
            if (Directory.Exists(devPath) && File.Exists(Path.Combine(devPath, "index.html")))
                return devPath;
        }

        // 2. 发布版本：从嵌入 ZIP 解压
        var cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KiteTodo", "NoteAppCache");
        var indexFile = Path.Combine(cacheDir, "index.html");

        if (File.Exists(indexFile))
            return cacheDir;

        // 提取嵌入 ZIP
        var assembly = typeof(NotebookProPage).Assembly;
        using var stream = assembly.GetManifestResourceStream("KiteTodo.noteapp-win-x64.zip");
        if (stream == null)
            throw new InvalidOperationException("嵌入的 NoteApp 资源未找到");

        if (Directory.Exists(cacheDir))
            Directory.Delete(cacheDir, true);
        Directory.CreateDirectory(cacheDir);

        using var archive = new System.IO.Compression.ZipArchive(stream);
        foreach (var entry in archive.Entries)
        {
            var destPath = Path.Combine(cacheDir, entry.FullName.Replace('/', Path.DirectorySeparatorChar));
            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destPath);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                using var fs = File.Create(destPath);
                using var es = entry.Open();
                es.CopyTo(fs);
            }
        }

        await Task.CompletedTask;
        return cacheDir;
    }
}
