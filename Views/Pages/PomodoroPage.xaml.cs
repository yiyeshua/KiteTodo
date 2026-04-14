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
using System.Windows;
using System.Windows.Controls;
using KiteTodo.ViewModels;

namespace KiteTodo.Views.Pages;

/// <summary>
/// 番茄钟页面，提供专注计时功能，包含开始/暂停/继续/取消四个操作按钮。
/// </summary>
public partial class PomodoroPage : Page
{
    /// <summary>番茄钟的 ViewModel 实例，管理计时状态和数据</summary>
    private readonly PomodoroViewModel _vm = new();

    public PomodoroPage()
    {
        InitializeComponent();
        DataContext = _vm;

        // 监听 ViewModel 的属性变化事件，当状态改变时更新按钮的显示/隐藏
        _vm.PropertyChanged += OnVmPropertyChanged;

        // 初始化时设置一次按钮可见性（此时应该是 Idle 状态，只显示"开始"按钮）
        UpdateButtonVisibility();
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

    /// <summary>点击"开始"按钮，启动专注计时</summary>
    private void OnStart(object sender, RoutedEventArgs e)
    {
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
}
