using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace KiteTodo.Views;

public partial class FloatingEntryWindow : Window
{
    private readonly Action _openMainWindow;
    private readonly Action _hideEntry;
    private readonly Action<double, double> _savePosition;

    private bool _wasDragged;

    // 自动贴边
    private readonly DispatcherTimer _autoDockTimer;
    private const double DockMargin = 5;         // 贴边后距屏幕边缘的间距
    private const double AutoDockSeconds = 30;    // 空闲多少秒后自动贴边

    // 番茄钟状态（由外部推送）
    private bool _isPomodoroActive;       // 是否正在计时（包括暂停）
    private bool _isPomodoroPaused;       // 是否已暂停
    private Action? _onPomodoroTogglePause; // 暂停/恢复回调

    public FloatingEntryWindow(
        Action openMainWindow,
        Action hideEntry,
        Action<double, double> savePosition,
        double? initialLeft,
        double? initialTop)
    {
        InitializeComponent();

        _openMainWindow = openMainWindow;
        _hideEntry = hideEntry;
        _savePosition = savePosition;

        if (initialLeft.HasValue && initialTop.HasValue
            && initialLeft.Value > 0 && initialTop.Value > 0)
        {
            Left = initialLeft.Value;
            Top = initialTop.Value;
        }
        else
        {
            var screen = SystemParameters.WorkArea;
            Left = screen.Right - 80;
            Top = screen.Height / 2 - 28;
        }

        // 初始化自动贴边计时器
        _autoDockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(AutoDockSeconds) };
        _autoDockTimer.Tick += (_, _) =>
        {
            _autoDockTimer.Stop();
            AnimateDockToEdge();
        };
        _autoDockTimer.Start();
    }

    /// <summary>重置自动贴边计时器（任何交互后调用）</summary>
    private void ResetAutoDockTimer()
    {
        _autoDockTimer.Stop();
        _autoDockTimer.Start();
    }

    /// <summary>动画贴边到最近的左/右屏幕边缘</summary>
    private void AnimateDockToEdge()
    {
        var screen = SystemParameters.WorkArea;
        double bubbleWidth = 56;
        double centerX = Left + bubbleWidth / 2;

        // 判断贴左还是贴右
        double targetLeft = centerX < screen.Width / 2
            ? screen.Left + DockMargin
            : screen.Right - bubbleWidth - DockMargin;

        // 限制 Top 在屏幕范围内
        double targetTop = Math.Max(screen.Top + DockMargin,
                           Math.Min(Top, screen.Bottom - 56 - DockMargin));

        var animLeft = new DoubleAnimation(targetLeft, TimeSpan.FromMilliseconds(300))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        };
        var animTop = new DoubleAnimation(targetTop, TimeSpan.FromMilliseconds(300))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        };

        animLeft.Completed += (_, _) =>
        {
            _savePosition(Left, Top);
        };

        BeginAnimation(LeftProperty, animLeft);
        BeginAnimation(TopProperty, animTop);
    }

    // ===== Thumb 拖拽事件（由 WPF Thumb 控件原生处理鼠标捕获和坐标追踪）=====

    private void Thumb_DragStarted(object sender, DragStartedEventArgs e)
    {
        _wasDragged = false;
        // 拖拽前清除贴边动画，否则动画会锁定 Left/Top
        BeginAnimation(LeftProperty, null);
        BeginAnimation(TopProperty, null);
        _autoDockTimer.Stop();
    }

    private void Thumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        Left += e.HorizontalChange;
        Top += e.VerticalChange;
        _wasDragged = true;
    }

    private void Thumb_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (_wasDragged)
        {
            _savePosition(Left, Top);
            ResetAutoDockTimer();
        }
        else
        {
            // 没有发生拖拽 → 视为单击
            if (_isPomodoroActive)
            {
                // 计时中：单击暂停/恢复
                _onPomodoroTogglePause?.Invoke();
            }
            // 空闲时单击不做任何操作（右键菜单仍可用）
        }
    }

    // ===== 番茄钟状态更新（由 App.xaml.cs 调用） =====

    /// <summary>设置暂停/恢复回调</summary>
    public void SetPomodoroToggleAction(Action togglePause)
    {
        _onPomodoroTogglePause = togglePause;
    }

    /// <summary>更新浮标的番茄钟显示状态</summary>
    public void UpdatePomodoroDisplay(bool isActive, bool isPaused, bool isFocusing, int remainingMinutes)
    {
        _isPomodoroActive = isActive;
        _isPomodoroPaused = isPaused;

        var border = BubbleThumb.Template.FindName("BubbleBorder", BubbleThumb) as System.Windows.Controls.Border;
        var text = BubbleThumb.Template.FindName("BubbleText", BubbleThumb) as System.Windows.Controls.TextBlock;
        if (border == null || text == null) return;

        if (!isActive)
        {
            // 空闲：恢复默认外观
            border.Background = new SolidColorBrush(Color.FromRgb(0, 120, 212)); // #0078D4
            text.Text = "K";
            text.FontSize = 26;
        }
        else if (isPaused)
        {
            // 已暂停：橙色 + 显示分钟数
            border.Background = new SolidColorBrush(Color.FromRgb(249, 115, 22)); // 橙色
            text.Text = remainingMinutes.ToString();
            text.FontSize = remainingMinutes >= 100 ? 16 : 22;
        }
        else if (isFocusing)
        {
            // 专注中：红色 + 显示分钟数
            border.Background = new SolidColorBrush(Color.FromRgb(220, 50, 50)); // 红色
            text.Text = remainingMinutes.ToString();
            text.FontSize = remainingMinutes >= 100 ? 16 : 22;
        }
        else
        {
            // 休息中：绿色 + 显示分钟数
            border.Background = new SolidColorBrush(Color.FromRgb(34, 160, 90)); // 绿色
            text.Text = remainingMinutes.ToString();
            text.FontSize = remainingMinutes >= 100 ? 16 : 22;
        }
    }

    // ===== 右键菜单事件 =====

    private void OnShowMainWindow(object sender, RoutedEventArgs e)
    {
        // 延迟到 ContextMenu 弹出窗口完全关闭后再激活主窗口，
        // 否则 Windows 会因为前台锁定阻止 SetForegroundWindow
        Dispatcher.BeginInvoke(_openMainWindow);
        ResetAutoDockTimer();
    }

    private void OnHideEntry(object sender, RoutedEventArgs e)
    {
        _hideEntry();
    }
}
