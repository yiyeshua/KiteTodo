// ============================================================================
// PomodoroViewModel.cs - 番茄钟 ViewModel
// 管理番茄钟计时器的状态、倒计时、自动切换专注/休息阶段
//
// 【番茄工作法流程】
// 1. 专注 25 分钟 → 短休息 5 分钟（重复 N 轮）
// 2. 每完成 N 轮专注后 → 长休息 15 分钟
// 3. 时间参数可在设置页面自定义
// ============================================================================

using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KiteTodo.Models;
using KiteTodo.Services;

namespace KiteTodo.ViewModels;

/// <summary>
/// 番茄钟状态枚举
/// </summary>
public enum PomodoroState
{
    Idle,       // 空闲（未开始）
    Focusing,   // 专注中
    ShortBreak, // 短休息
    LongBreak   // 长休息
}

/// <summary>
/// 番茄钟 ViewModel，使用 DispatcherTimer 驱动倒计时，
/// 自动在专注→休息→专注之间切换，并记录完成的番茄钟到数据库。
/// </summary>
public partial class PomodoroViewModel : ObservableObject
{
    private readonly DatabaseService _db = DatabaseService.Instance;
    private readonly TodoService _todoService = new();

    /// <summary>倒计时定时器（每 200ms 更新一次显示）</summary>
    private DispatcherTimer? _timer;

    /// <summary>本轮计时的开始时间（用于计算已过时间）</summary>
    private DateTime _timerStartTime;

    /// <summary>本轮计时的目标时长</summary>
    private TimeSpan _targetDuration;

    /// <summary>本次会话中已完成的番茄钟数（用于判断是否进入长休息）</summary>
    private int _completedPomodoros;

    /// <summary>当前状态</summary>
    [ObservableProperty]
    private PomodoroState _state = PomodoroState.Idle;

    /// <summary>剩余时间</summary>
    [ObservableProperty]
    private TimeSpan _remainingTime;

    /// <summary>进度（0.0 ~ 1.0），用于绑定进度条</summary>
    [ObservableProperty]
    private double _progress;

    /// <summary>状态标签文本（如 "专注中..."、"短休息"、"已暂停"）</summary>
    [ObservableProperty]
    private string _stateLabel = "准备就绪";

    /// <summary>今日已完成的番茄钟数</summary>
    [ObservableProperty]
    private int _todayPomodoros;

    /// <summary>倒计时显示文本（如 "25:00"）</summary>
    [ObservableProperty]
    private string _timerDisplay = "25:00";

    public PomodoroViewModel()
    {
        var settings = _db.GetSettings();
        RemainingTime = TimeSpan.FromMinutes(settings.PomodoroDuration);
        UpdateTimerDisplay();
        LoadTodayStats();
    }

    /// <summary>统计今日已完成的番茄钟数量</summary>
    private void LoadTodayStats()
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        TodayPomodoros = _db.Pomodoros
            .Find(x => x.StartTime >= today && x.StartTime < tomorrow && x.IsCompleted)
            .Count();
    }

    /// <summary>开始专注</summary>
    [RelayCommand]
    private void StartFocus()
    {
        var settings = _db.GetSettings();
        _targetDuration = TimeSpan.FromMinutes(settings.PomodoroDuration);
        State = PomodoroState.Focusing;
        StateLabel = "专注中...";
        StartTimer(_targetDuration);
    }

    /// <summary>暂停计时器</summary>
    [RelayCommand]
    private void Pause()
    {
        _timer?.Stop();
        StateLabel = "已暂停";
    }

    /// <summary>恢复计时器（从暂停处继续）</summary>
    [RelayCommand]
    private void Resume()
    {
        if (State == PomodoroState.Idle) return;
        // 根据剩余时间倒推开始时间，确保继续计时准确
        _timerStartTime = DateTime.Now - (_targetDuration - RemainingTime);
        _timer?.Start();
        StateLabel = State == PomodoroState.Focusing ? "专注中..." : "休息中";
    }

    /// <summary>取消当前番茄钟，恢复到空闲状态</summary>
    [RelayCommand]
    private void Cancel()
    {
        _timer?.Stop();
        State = PomodoroState.Idle;
        var settings = _db.GetSettings();
        RemainingTime = TimeSpan.FromMinutes(settings.PomodoroDuration);
        Progress = 0;
        StateLabel = "准备就绪";
        UpdateTimerDisplay();
    }

    /// <summary>启动/重启内部定时器</summary>
    private void StartTimer(TimeSpan duration)
    {
        _targetDuration = duration;
        _timerStartTime = DateTime.Now;
        RemainingTime = duration;

        _timer?.Stop();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    /// <summary>定时器每 200ms 触发一次，更新剩余时间和进度</summary>
    private void OnTimerTick(object? sender, EventArgs e)
    {
        var elapsed = DateTime.Now - _timerStartTime;
        RemainingTime = _targetDuration - elapsed;

        if (RemainingTime <= TimeSpan.Zero)
        {
            RemainingTime = TimeSpan.Zero;
            _timer?.Stop();
            OnTimerCompleted();
        }

        Progress = 1.0 - (RemainingTime.TotalSeconds / _targetDuration.TotalSeconds);
        UpdateTimerDisplay();
    }

    /// <summary>
    /// 计时完成时的处理逻辑：
    /// - 专注结束 → 记录到数据库 → 进入短休息或长休息
    /// - 休息结束 → 恢复到空闲状态
    /// </summary>
    private void OnTimerCompleted()
    {
        var settings = _db.GetSettings();

        if (State == PomodoroState.Focusing)
        {
            // 专注完成：记录到数据库
            _completedPomodoros++;
            _db.Pomodoros.Insert(new PomodoroRecord
            {
                StartTime = _timerStartTime,
                DurationMinutes = settings.PomodoroDuration,
                IsCompleted = true
            });
            LoadTodayStats();

            // 判断进入长休息还是短休息
            if (_completedPomodoros % settings.LongBreakInterval == 0)
            {
                State = PomodoroState.LongBreak;
                StateLabel = "长休息";
                StartTimer(TimeSpan.FromMinutes(settings.LongBreakDuration));
            }
            else
            {
                State = PomodoroState.ShortBreak;
                StateLabel = "短休息";
                StartTimer(TimeSpan.FromMinutes(settings.ShortBreakDuration));
            }
        }
        else
        {
            // 休息结束，恢复空闲
            State = PomodoroState.Idle;
            StateLabel = "准备就绪";
            RemainingTime = TimeSpan.FromMinutes(settings.PomodoroDuration);
            Progress = 0;
            UpdateTimerDisplay();
        }
    }

    /// <summary>格式化剩余时间为 "MM:SS" 显示</summary>
    private void UpdateTimerDisplay()
    {
        var ts = RemainingTime;
        if (ts < TimeSpan.Zero) ts = TimeSpan.Zero;
        TimerDisplay = $"{(int)ts.TotalMinutes:D2}:{ts.Seconds:D2}";
    }
}
