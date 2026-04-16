// ============================================================================
// PomodoroPage.xaml.cs — 番茄钟页面的代码后台 (Code-Behind)
// ============================================================================
// 功能说明：
//   番茄钟（Pomodoro Technique）计时页面，帮助用户进行专注工作。
//   包含 4 个操作按钮：开始、暂停、继续、取消，根据当前状态动态显示/隐藏。
//
// 状态机说明：
//   番茄钟有以下几种状态（定义在 PomodoroViewModel 中）：
//   - Idle（空闲）：初始状态，显示"开始"按钮
//   - Focus（专注中）：倒计时进行中，显示"暂停"和"取消"按钮
//   - 已暂停：暂停状态，显示"继续"和"取消"按钮
//   - ShortBreak / LongBreak（休息）：自动进入休息倒计时
//
// WPF 知识点：
//   - INotifyPropertyChanged：当 ViewModel 属性变化时，会触发 PropertyChanged 事件
//   - 这里通过监听该事件来手动控制按钮的 Visibility（显示/折叠）
//   - Visibility.Visible = 显示，Visibility.Collapsed = 隐藏且不占位
//   - 之所以用代码控制而非 XAML 绑定+转换器，是因为按钮的显隐逻辑涉及多个条件组合
// ============================================================================

using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using KiteTodo.Services;
using KiteTodo.ViewModels;

namespace KiteTodo.Views.Pages;

/// <summary>
/// 番茄钟页面，提供专注计时功能，包含开始/暂停/继续/取消四个操作按钮。
/// </summary>
public partial class PomodoroPage : Page
{
    /// <summary>番茄钟的 ViewModel 实例，管理计时状态和数据</summary>
    private readonly PomodoroViewModel _vm = PomodoroViewModel.Instance;

    public PomodoroPage()
    {
        InitializeComponent();
        DataContext = _vm;

        // 监听 ViewModel 的属性变化事件，当状态改变时更新按钮的显示/隐藏
        _vm.PropertyChanged += OnVmPropertyChanged;

        // (专注开始对话框已移至 OnStart 中弹出)

        // 初始化时设置一次按钮可见性（此时应该是 Idle 状态，只显示"开始"按钮）
        UpdateButtonVisibility();

        // 页面每次展示时刷新待办列表和检查绑定请求
        Loaded += OnPageLoaded;
    }

    /// <summary>页面加载时刷新待办列表并检查待绑定请求</summary>
    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        _vm.RefreshIdleDisplay();
        _vm.LoadAvailableTodos();
        _vm.CheckPendingRequest();
        UpdateBindingUI();
        UpdateBreathingAnimation();

        // 同步 ComboBox 选中项
        if (_vm.BoundTodoId.HasValue)
        {
            var match = _vm.AvailableTodos.FirstOrDefault(t => t.Id == _vm.BoundTodoId.Value);
            TodoSelector.SelectedItem = match;
        }
    }

    /// <summary>
    /// ViewModel 属性变化时的回调。
    /// 当 State（状态枚举）或 StateLabel（状态文本）发生变化时，重新计算按钮可见性。
    /// </summary>
    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PomodoroViewModel.State) ||
            e.PropertyName == nameof(PomodoroViewModel.StateLabel))
        {
            UpdateButtonVisibility();
            UpdateBreathingAnimation();
        }
    }

    /// <summary>
    /// 根据当前番茄钟状态，决定 4 个按钮的显示/隐藏。
    /// - 空闲(Idle)：只显示"开始"
    /// - 运行中(非空闲且非暂停)：显示"暂停"和"取消"
    /// - 已暂停：显示"继续"和"取消"
    /// </summary>
    private void UpdateButtonVisibility()
    {
        var isIdle = _vm.State == PomodoroState.Idle;       // 是否处于空闲状态
        var isPaused = _vm.StateLabel == "已暂停";            // 是否处于暂停状态
        var isRunning = !isIdle && !isPaused;                // 是否正在运行（非空闲且非暂停）

        // BtnStart / BtnPause / BtnResume / BtnCancel 是在 XAML 中通过 x:Name 定义的按钮控件
        BtnStart.Visibility = isIdle ? Visibility.Visible : Visibility.Collapsed;
        BtnPause.Visibility = isRunning ? Visibility.Visible : Visibility.Collapsed;
        BtnResume.Visibility = isPaused ? Visibility.Visible : Visibility.Collapsed;
        BtnCancel.Visibility = !isIdle ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---- 按钮事件回调，每个操作都委托给 ViewModel 并刷新按钮状态 ----

    /// <summary>点击"开始"按钮，根据设置决定是否弹出专注开始对话框，然后启动计时</summary>
    private void OnStart(object sender, RoutedEventArgs e)
    {
        var settings = DatabaseService.Instance.GetSettings();

        if (settings.ShowFocusStartDialog)
        {
            var dialog = new FocusCompleteDialog
            {
                Owner = Window.GetWindow(this) ?? Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true)
            {
                _vm.PendingNote = dialog.FocusNote;
                _vm.PendingMood = dialog.SelectedMood;
            }
            else
            {
                _vm.PendingNote = null;
                _vm.PendingMood = 0;
            }
        }
        else
        {
            _vm.PendingNote = null;
            _vm.PendingMood = 0;
        }

        _vm.StartFocusCommand.Execute(null);
        UpdateButtonVisibility();
    }

    /// <summary>点击"暂停"按钮，暂停当前计时</summary>
    private void OnPause(object sender, RoutedEventArgs e)
    {
        _vm.PauseCommand.Execute(null);
        UpdateButtonVisibility();
    }

    /// <summary>点击"继续"按钮，恢复暂停的计时</summary>
    private void OnResume(object sender, RoutedEventArgs e)
    {
        _vm.ResumeCommand.Execute(null);
        UpdateButtonVisibility();
    }

    /// <summary>点击"取消"按钮，取消本次计时并回到空闲状态</summary>
    private void OnCancel(object sender, RoutedEventArgs e)
    {
        _vm.CancelCommand.Execute(null);
        UpdateButtonVisibility();
    }

    /// <summary>切换到前一天的专注记录</summary>
    private void OnPrevDay(object sender, RoutedEventArgs e) => _vm.GoPrevDay();

    /// <summary>切换到后一天的专注记录</summary>
    private void OnNextDay(object sender, RoutedEventArgs e) => _vm.GoNextDay();

    /// <summary>删除一条专注记录</summary>
    private void OnDeleteRecord(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is PomodoroRecordDisplay record)
        {
            _vm.DeleteRecord(record.Id);
        }
    }

    /// <summary>将专注记录转为今日待办</summary>
    private void OnConvertToTodo(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is PomodoroRecordDisplay record)
        {
            _vm.ConvertToTodo(record);
            // 重新加载列表以刷新 ConvertedToTodo 状态
            _vm.RefreshRecords();
        }
    }

    /// <summary>下拉选择待办时绑定到番茄钟</summary>
    private void OnTodoSelected(object sender, SelectionChangedEventArgs e)
    {
        if (TodoSelector.SelectedItem is KiteTodo.Models.TodoItem item)
        {
            _vm.BindTodo(item.Id, item.Title);
            UpdateBindingUI();
        }
    }

    /// <summary>解除任务绑定</summary>
    private void OnUnbindTodo(object sender, RoutedEventArgs e)
    {
        _vm.UnbindTodo();
        TodoSelector.SelectedItem = null;
        UpdateBindingUI();
    }

    /// <summary>更新绑定状态的 UI 显示</summary>
    private void UpdateBindingUI()
    {
        var hasBound = _vm.BoundTodoId.HasValue;
        BtnUnbind.Visibility = hasBound ? Visibility.Visible : Visibility.Collapsed;
        BoundTaskLabel.Visibility = hasBound ? Visibility.Visible : Visibility.Collapsed;
        BoundTaskLabel.Text = hasBound ? $"当前绑定: {_vm.BoundTodoTitle}" : "";
        TimerBoundTitle.Visibility = hasBound ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---- 专注完成弹窗 ----

    // ---- 圆环呼吸动画 ----

    /// <summary>根据当前状态启动或停止呼吸动画</summary>
    private void UpdateBreathingAnimation()
    {
        if (_vm.State == PomodoroState.Idle || _vm.StateLabel == "已暂停")
        {
            StopBreathingAnimation();
        }
        else
        {
            // 专注：蓝色呼吸光晕；休息：绿色呼吸光晕
            var glowColor = _vm.State == PomodoroState.Focusing
                ? Color.FromRgb(74, 144, 217)
                : Color.FromRgb(52, 160, 90);
            StartBreathingAnimation(glowColor);
        }
    }

    /// <summary>启动圆环边框的呼吸发光动画</summary>
    private void StartBreathingAnimation(Color glowColor)
    {
        StopBreathingAnimation();

        var baseColor = (Color)ColorConverter.ConvertFromString("#DDDDDD");
        var animation = new ColorAnimation
        {
            From = baseColor,
            To = glowColor,
            Duration = TimeSpan.FromSeconds(2.5),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };

        var brush = new SolidColorBrush(baseColor);
        TimerRing.BorderBrush = brush;
        brush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
    }

    /// <summary>停止呼吸动画，恢复默认边框颜色</summary>
    private void StopBreathingAnimation()
    {
        if (TimerRing.BorderBrush is SolidColorBrush brush)
            brush.BeginAnimation(SolidColorBrush.ColorProperty, null);
        TimerRing.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DDDDDD"));
    }
}
