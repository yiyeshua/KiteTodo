// ============================================================================
// App.xaml.cs - 应用程序入口和全局管理
//
// 职责：
// 1. 应用启动初始化（数据库、主题、提醒服务、系统托盘）
// 2. 系统托盘图标和右键菜单
// 3. 主窗口的显示/隐藏（关闭窗口时最小化到托盘而非退出）
// 4. 深色/浅色主题切换（动态替换资源字典 + 强制刷新窗口背景）
// 5. 应用退出清理
// ============================================================================

using System.Drawing;
using System.Windows;
using System.Windows.Media;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using KiteTodo.Services;
using Wpf.Ui.Appearance;

namespace KiteTodo;

public partial class App : Application
{
    private TrayIconWithContextMenu? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Initialize database
        _ = DatabaseService.Instance;

        // Apply saved theme
        var settings = DatabaseService.Instance.GetSettings();
        ApplyTheme(settings.ThemeMode);

        // Start reminder service
        ReminderService.Instance.Start();

        // Setup tray icon
        SetupTrayIcon();
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new TrayIconWithContextMenu
        {
            ToolTip = "KiteTodo - 风筝待办",
            Icon = CreateDefaultIcon().Handle
        };

        _trayIcon.ContextMenu = new PopupMenu
        {
            Items =
            {
                new PopupMenuItem("显示主窗口", (_, _) =>
                    Dispatcher.BeginInvoke(ShowMainWindow)),
                new PopupMenuSeparator(),
                new PopupMenuItem("退出", (_, _) =>
                    Dispatcher.BeginInvoke(new Action(ExitApp)))
            }
        };

        _trayIcon.MessageWindow.MouseEventReceived += (_, me) =>
        {
            if (me.MouseEvent == MouseEvent.IconLeftMouseUp)
            {
                Dispatcher.BeginInvoke(ShowMainWindow);
            }
        };

        _trayIcon.Create();
    }

    private Icon CreateDefaultIcon()
    {
        var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(System.Drawing.Color.FromArgb(0, 120, 212));
            using var font = new Font("Segoe UI", 18, System.Drawing.FontStyle.Bold);
            using var brush = new SolidBrush(System.Drawing.Color.White);
            var size = g.MeasureString("K", font);
            g.DrawString("K", font, brush,
                (32 - size.Width) / 2,
                (32 - size.Height) / 2);
        }
        return Icon.FromHandle(bitmap.GetHicon());
    }

    public void ShowMainWindow()
    {
        if (MainWindow == null)
        {
            MainWindow = new MainWindow();
        }
        MainWindow.Show();
        MainWindow.WindowState = WindowState.Normal;
        MainWindow.Activate();
        MainWindow.Topmost = true;
        MainWindow.Topmost = false;
        MainWindow.Focus();
    }

    public void ApplyTheme(string theme)
    {
        var isDark = theme == "Dark";
        var appTheme = isDark ? ApplicationTheme.Dark : ApplicationTheme.Light;

        // 1. Swap the ThemesDictionary in MergedDictionaries
        var merged = Resources.MergedDictionaries;
        for (int i = merged.Count - 1; i >= 0; i--)
        {
            if (merged[i] is Wpf.Ui.Markup.ThemesDictionary)
            {
                merged.RemoveAt(i);
                merged.Insert(i, new Wpf.Ui.Markup.ThemesDictionary { Theme = appTheme });
                break;
            }
        }

        // 2. Also call ApplicationThemeManager for accent colors
        ApplicationThemeManager.Apply(appTheme);

        // 3. Force solid window background (Mica not available on Win10)
        Resources["CustomWindowBg"] = new SolidColorBrush(
            isDark ? ColorFromHex("#1E1E1E") : ColorFromHex("#F3F3F3"));
        Resources["CustomNavBg"] = new SolidColorBrush(
            isDark ? ColorFromHex("#252525") : ColorFromHex("#F5F5F5"));
        Resources["CustomContentBg"] = new SolidColorBrush(
            isDark ? ColorFromHex("#1E1E1E") : ColorFromHex("#FAFAFA"));

        // 4. Update custom resources for week bar, etc.
        Resources["CustomCardBg"] = new SolidColorBrush(
            isDark ? ColorFromHex("#2B2B2B") : ColorFromHex("#F0F4F8"));
        Resources["CustomBorderBrush"] = new SolidColorBrush(
            isDark ? ColorFromHex("#404040") : ColorFromHex("#D0D0D0"));
        Resources["CustomSubtleBg"] = new SolidColorBrush(
            isDark ? ColorFromHex("#2A2A2A") : ColorFromHex("#F5F5F5"));
        Resources["CustomSubtleBorder"] = new SolidColorBrush(
            isDark ? ColorFromHex("#3A3A3A") : ColorFromHex("#E0E0E0"));
        Resources["CustomFg"] = new SolidColorBrush(
            isDark ? ColorFromHex("#E4E4E4") : ColorFromHex("#222222"));
        Resources["CustomFgSecondary"] = new SolidColorBrush(
            isDark ? ColorFromHex("#AAAAAA") : ColorFromHex("#666666"));
        Resources["CustomFgTertiary"] = new SolidColorBrush(
            isDark ? ColorFromHex("#777777") : ColorFromHex("#999999"));
        Resources["CustomTodayBg"] = new SolidColorBrush(
            isDark ? ColorFromHex("#1A3A5C") : ColorFromHex("#E8F0FE"));
        Resources["CustomOverlayBg"] = new SolidColorBrush(
            isDark ? ColorFromHex("#2D2D2D") : Colors.White);

        // 5. Force background on existing main window
        if (MainWindow != null)
        {
            MainWindow.Background = new SolidColorBrush(
                isDark ? ColorFromHex("#1E1E1E") : ColorFromHex("#F3F3F3"));
        }
    }

    private static System.Windows.Media.Color ColorFromHex(string hex)
    {
        return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
    }

    private void ExitApp()
    {
        ReminderService.Instance.Stop();
        _trayIcon?.Dispose();
        _trayIcon = null;
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ReminderService.Instance.Stop();
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
