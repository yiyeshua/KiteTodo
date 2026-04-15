using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace KiteTodo.Views;

public partial class FloatingEntryWindow : Window
{
    private readonly Action _openMainWindow;
    private readonly Action _hideEntry;
    private readonly Action<string, DateTime> _quickAddTodo;
    private readonly Action<double, double> _savePosition;

    private bool _wasDragged;
    private DateTime _targetDate = DateTime.Today;

    // 自动贴边
    private readonly DispatcherTimer _autoDockTimer;
    private const double DockMargin = 5;         // 贴边后距屏幕边缘的间距
    private const double AutoDockSeconds = 30;    // 空闲多少秒后自动贴边

    public FloatingEntryWindow(
        Action openMainWindow,
        Action hideEntry,
        Action<string, DateTime> quickAddTodo,
        Action<double, double> savePosition,
        double? initialLeft,
        double? initialTop)
    {
        InitializeComponent();

        _openMainWindow = openMainWindow;
        _hideEntry = hideEntry;
        _quickAddTodo = quickAddTodo;
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
            if (MiniPanel.Visibility != Visibility.Visible) // 输入面板展开时不贴边
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
            // 没有发生拖拽 → 视为单击 → 切换面板
            ToggleMiniPanel();
        }
    }

    // ===== 面板展开/收起 =====

    private void ToggleMiniPanel()
    {
        if (MiniPanel.Visibility == Visibility.Visible)
            CollapseMiniPanel();
        else
            ExpandMiniPanel();
    }

    private void ExpandMiniPanel()
    {
        MiniPanel.Visibility = Visibility.Visible;
        _targetDate = DateTime.Today;
        DateLabel.Text = "📅 今天";
        InputBox.Text = "";
        Activate();
        Dispatcher.BeginInvoke(() => InputBox.Focus(), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void CollapseMiniPanel()
    {
        MiniPanel.Visibility = Visibility.Collapsed;
        ResetAutoDockTimer();
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



    // ===== 输入面板操作 =====

    private void OnSetToday(object sender, RoutedEventArgs e)
    {
        _targetDate = DateTime.Today;
        DateLabel.Text = "📅 今天";
    }

    private void OnSetTomorrow(object sender, RoutedEventArgs e)
    {
        _targetDate = DateTime.Today.AddDays(1);
        DateLabel.Text = "📅 明天";
    }

    private void OnCollapse(object sender, RoutedEventArgs e)
    {
        CollapseMiniPanel();
    }

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        AddTodoFromInput();
    }

    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            AddTodoFromInput();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CollapseMiniPanel();
            e.Handled = true;
        }
    }

    private void AddTodoFromInput()
    {
        var text = InputBox.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return;

        _quickAddTodo(text, _targetDate);
        InputBox.Text = "";
        InputBox.Focus();
        ResetAutoDockTimer();
    }
}
