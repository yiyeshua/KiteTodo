using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;

namespace KiteTodo.Views.Tools;

public partial class ExcalidrawToolView : UserControl
{
    private static ExcalidrawToolView? _sharedInstance;
    private static bool _webViewInitialized;

    public ExcalidrawToolView()
    {
        InitializeComponent();
    }

    public static ExcalidrawToolView GetSharedInstance()
    {
        if (_sharedInstance == null)
            _sharedInstance = new ExcalidrawToolView();

        return _sharedInstance;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_webViewInitialized)
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            DrawBrowser.Visibility = Visibility.Visible;
            return;
        }

        _webViewInitialized = true;

        await DrawBrowser.EnsureCoreWebView2Async();

        DrawBrowser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
        DrawBrowser.CoreWebView2.Settings.AreDevToolsEnabled = false;
        DrawBrowser.CoreWebView2.Settings.IsStatusBarEnabled = false;
        DrawBrowser.CoreWebView2.Settings.IsSwipeNavigationEnabled = false;
        DrawBrowser.CoreWebView2.NewWindowRequested += OnNewWindowRequested;

        DrawBrowser.CoreWebView2.Navigate("https://excalidraw.com");

        LoadingOverlay.Visibility = Visibility.Collapsed;
        DrawBrowser.Visibility = Visibility.Visible;
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = e.Uri,
                UseShellExecute = true
            });
        }
        catch
        {
            // 忽略启动外部浏览器失败的异常
        }
    }
}
