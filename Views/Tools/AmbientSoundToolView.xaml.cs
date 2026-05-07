using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;

namespace KiteTodo.Views.Tools;

public partial class AmbientSoundToolView : UserControl
{
    private static AmbientSoundToolView? _sharedInstance;
    private static bool _webViewInitialized;

    public AmbientSoundToolView()
    {
        InitializeComponent();
    }

    public static AmbientSoundToolView GetSharedInstance()
    {
        if (_sharedInstance == null)
            _sharedInstance = new AmbientSoundToolView();

        return _sharedInstance;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_webViewInitialized)
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            SoundBrowser.Visibility = Visibility.Visible;
            return;
        }

        _webViewInitialized = true;

        await SoundBrowser.EnsureCoreWebView2Async();

        SoundBrowser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
        SoundBrowser.CoreWebView2.Settings.AreDevToolsEnabled = false;
        SoundBrowser.CoreWebView2.Settings.IsStatusBarEnabled = false;
        SoundBrowser.CoreWebView2.Settings.IsSwipeNavigationEnabled = false;
        SoundBrowser.CoreWebView2.NewWindowRequested += OnNewWindowRequested;

        SoundBrowser.CoreWebView2.Navigate("https://asoftmurmur.com");

        LoadingOverlay.Visibility = Visibility.Collapsed;
        SoundBrowser.Visibility = Visibility.Visible;
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

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // 不再导航到 about:blank，保持环境音持续播放
    }
}
