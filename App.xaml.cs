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
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using KiteTodo.Models;
using KiteTodo.Services;
using KiteTodo.ViewModels;
using KiteTodo.Views;
using Wpf.Ui.Appearance;

namespace KiteTodo;

public partial class App : Application
{
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_SHOW = 5;
    private const int SW_RESTORE = 9;

    private TrayIconWithContextMenu? _trayIcon;
    private FloatingEntryWindow? _floatingEntryWindow;
    private MainWindow? _mainWindow;


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
        ReminderService.Instance.ReminderBatchFired += OnReminderBatchFired;

        // Setup tray icon
        SetupTrayIcon();

        // 监听番茄钟状态变化，推送给浮标
        PomodoroViewModel.Instance.PropertyChanged += OnPomodoroPropertyChanged;

        // 番茄钟计时完成时触发桌面提醒
        PomodoroViewModel.Instance.TimerCompleted += OnPomodoroTimerCompleted;

        // 恢复浮标显示状态
        if (settings.ShowMiniWindow)
        {
            ShowFloatingEntry();
        }
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
                new PopupMenuItem("显示/隐藏浮标", (_, _) =>
                    Dispatcher.BeginInvoke(ToggleFloatingEntry)),
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

    private MainWindow EnsureMainWindow()
    {
        if (_mainWindow == null || !_mainWindow.IsLoaded)
        {
            // StartupUri 创建的主窗口会赋给 Application.MainWindow
            _mainWindow = MainWindow as MainWindow;
        }
        if (_mainWindow == null)
        {
            _mainWindow = new MainWindow();
        }
        return _mainWindow;
    }

    public void ShowMainWindow()
    {
        var win = EnsureMainWindow();

        win.Show();
        win.WindowState = WindowState.Normal;
        win.Activate();

        // 使用 Win32 API 可靠地将窗口带到前台
        var hwnd = new WindowInteropHelper(win).Handle;
        if (hwnd != IntPtr.Zero)
        {
            SetForegroundWindow(hwnd);
        }

        win.Topmost = true;
        win.Topmost = false;
    }

    // ===== 浮标窗口管理 =====

    private void ShowFloatingEntry()
    {
        if (_floatingEntryWindow != null)
        {
            _floatingEntryWindow.Show();
            return;
        }

        var settings = DatabaseService.Instance.GetSettings();
        double? left = settings.MiniWindowX > 0 ? settings.MiniWindowX : null;
        double? top = settings.MiniWindowY > 0 ? settings.MiniWindowY : null;

        _floatingEntryWindow = new FloatingEntryWindow(
            openMainWindow: ShowMainWindow,
            hideEntry: HideFloatingEntry,
            savePosition: SaveFloatingPosition,
            initialLeft: left,
            initialTop: top);

        _floatingEntryWindow.Closed += (_, _) => _floatingEntryWindow = null;
        _floatingEntryWindow.Show();

        // 设置番茄钟暂停/恢复回调，并同步当前状态
        _floatingEntryWindow.SetPomodoroToggleAction(PomodoroViewModel.Instance.TogglePause);
        SyncPomodoroToFloating();

        settings.ShowMiniWindow = true;
        DatabaseService.Instance.SaveSettings(settings);
    }

    /// <summary>番茄钟属性变化时推送状态给浮标</summary>
    private void OnPomodoroPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PomodoroViewModel.State)
            or nameof(PomodoroViewModel.StateLabel)
            or nameof(PomodoroViewModel.RemainingTime))
        {
            Dispatcher.BeginInvoke(SyncPomodoroToFloating);
        }
    }

    /// <summary>番茄钟计时完成时弹出大号提示</summary>
    private void OnPomodoroTimerCompleted(PomodoroState completedState)
    {
        Dispatcher.BeginInvoke(() =>
        {
            var settings = DatabaseService.Instance.GetSettings();

            // 播放提示音
            if (settings.EnableCompletionSound)
                AlertService.PlayCompletionSound(settings.CompletionSoundName);

            if (completedState == PomodoroState.Focusing)
                AlertService.ShowOverlayToast("☕ 该休息休息了", "起来走走，喝杯水吧", settings.ToastDurationSeconds);
            else
                AlertService.ShowOverlayToast("🍅 休息结束", "准备开始下一轮专注", settings.ToastDurationSeconds);
        });
    }

    private void OnReminderBatchFired(IReadOnlyList<TodoItem> todos)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (todos.Count == 0)
                return;

            var title = todos.Count == 1
                ? $"今日提醒：{todos[0].Title}"
                : $"今日有 {todos.Count} 条待办到提醒时间";

            var subMessage = todos.Count == 1
                ? "请在方便时处理"
                : BuildReminderSummary(todos);

            AlertService.ShowCornerToast($"{title}\n{subMessage}", 4);
        });
    }

    private static string BuildReminderSummary(IReadOnlyList<TodoItem> todos)
    {
        var topTitles = todos
            .Take(2)
            .Select(todo => todo.Title)
            .ToList();

        if (todos.Count <= 2)
            return string.Join("；", topTitles);

        return $"{string.Join("；", topTitles)} 等 {todos.Count} 条";
    }

    /// <summary>将番茄钟当前状态同步到浮标显示</summary>
    private void SyncPomodoroToFloating()
    {
        if (_floatingEntryWindow == null) return;
        var vm = PomodoroViewModel.Instance;
        bool isActive = vm.State != PomodoroState.Idle;
        bool isPaused = vm.StateLabel == "已暂停";
        bool isFocusing = vm.State == PomodoroState.Focusing;
        int remainingMin = (int)Math.Ceiling(vm.RemainingTime.TotalMinutes);
        _floatingEntryWindow.UpdatePomodoroDisplay(isActive, isPaused, isFocusing, remainingMin);
    }

    private void HideFloatingEntry()
    {
        _floatingEntryWindow?.Close();
        _floatingEntryWindow = null;

        var settings = DatabaseService.Instance.GetSettings();
        settings.ShowMiniWindow = false;
        DatabaseService.Instance.SaveSettings(settings);
    }

    private void ToggleFloatingEntry()
    {
        if (_floatingEntryWindow != null)
            HideFloatingEntry();
        else
            ShowFloatingEntry();
    }

    private void SaveFloatingPosition(double left, double top)
    {
        var settings = DatabaseService.Instance.GetSettings();
        settings.MiniWindowX = left;
        settings.MiniWindowY = top;
        DatabaseService.Instance.SaveSettings(settings);
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
        var mw = MainWindow ?? _mainWindow;
        if (mw != null)
        {
            mw.Background = new SolidColorBrush(
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
