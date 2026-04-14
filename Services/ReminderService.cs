// ============================================================================
// ReminderService.cs - 待办提醒服务（单例）
// 每 30 秒检查一次是否有到期的提醒，到期则弹出 Windows Toast 通知
// ============================================================================

using System.Windows.Threading;
using KiteTodo.Models;
using Microsoft.Toolkit.Uwp.Notifications;

namespace KiteTodo.Services;

/// <summary>
/// 提醒服务，使用 DispatcherTimer 定时轮询数据库中设有提醒时间的未完成待办。
/// 当某待办的 ReminderTime 到达当前时间时，弹出 Windows 系统 Toast 通知。
/// 
/// 使用方式：ReminderService.Instance.Start() / .Stop()
/// </summary>
public class ReminderService
{
    private static readonly Lazy<ReminderService> _instance = new(() => new ReminderService());
    public static ReminderService Instance => _instance.Value;

    private readonly DatabaseService _db = DatabaseService.Instance;
    private readonly DispatcherTimer _timer;

    // 已通知过的待办 ID 集合，避免同一个待办重复弹出通知
    private readonly HashSet<int> _notifiedIds = new();

    private ReminderService()
    {
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30) // 每 30 秒检查一次
        };
        _timer.Tick += CheckReminders;
    }

    /// <summary>启动提醒定时器</summary>
    public void Start()
    {
        _timer.Start();
    }

    /// <summary>停止提醒定时器</summary>
    public void Stop()
    {
        _timer.Stop();
    }

    /// <summary>
    /// 定时器回调：查询所有设有提醒且未完成的待办，
    /// 如果提醒时间已到且尚未通知过，则弹出 Toast 通知。
    /// </summary>
    private void CheckReminders(object? sender, EventArgs e)
    {
        try
        {
            var now = DateTime.Now;
            // 查询所有有提醒时间且未完成的待办
            var todos = _db.Todos.Find(x =>
                x.ReminderTime != null &&
                x.CompletedAt == null)
                .ToList();

            foreach (var todo in todos)
            {
                // 提醒时间已到 且 还没通知过
                if (todo.ReminderTime.HasValue &&
                    todo.ReminderTime.Value <= now &&
                    !_notifiedIds.Contains(todo.Id))
                {
                    ShowReminder(todo);
                    _notifiedIds.Add(todo.Id); // 标记为已通知
                }
            }

            // 防止内存泄漏：通知 ID 集合过大时清空
            if (_notifiedIds.Count > 1000)
                _notifiedIds.Clear();
        }
        catch
        {
            // 静默忽略提醒检查失败（不影响主程序运行）
        }
    }

    /// <summary>弹出 Windows Toast 通知</summary>
    private void ShowReminder(TodoItem todo)
    {
        try
        {
            new ToastContentBuilder()
                .AddText($"待办提醒：{todo.Title}")
                .AddText(string.IsNullOrWhiteSpace(todo.Description)
                    ? $"计划日期: {todo.ScheduledDate:yyyy-MM-dd}"
                    : todo.Description)
                .Show();
        }
        catch
        {
            // 某些系统环境下 Toast 可能不可用，静默忽略
        }
    }

    /// <summary>清除指定待办的已通知标记（待办修改提醒时间后可重新触发通知）</summary>
    public void ClearNotified(int todoId)
    {
        _notifiedIds.Remove(todoId);
    }
}
