// ============================================================================
// TodoService.cs - 待办事项业务服务
// 封装所有待办事项的 CRUD 操作，ViewModel 通过此服务与数据库交互
// ============================================================================

using KiteTodo.Models;

namespace KiteTodo.Services;

/// <summary>
/// 待办事项服务，提供增删改查和统计功能。
/// 每个 ViewModel 各自 new 一个实例使用（内部共享同一个 DatabaseService 单例）。
/// </summary>
public class TodoService
{
    private readonly DatabaseService _db = DatabaseService.Instance;

    /// <summary>获取指定日期的所有待办（按完成状态、排序权重、优先级排序）</summary>
    public List<TodoItem> GetTodosByDate(DateTime date)
    {
        return GetTodosByDateAndTag(date, null);
    }

    /// <summary>获取指定日期的待办，支持按标签筛选</summary>
    public List<TodoItem> GetTodosByDateAndTag(DateTime date, string? tag)
    {
        var dayStart = date.Date;
        var dayEnd = dayStart.AddDays(1);
        var query = _db.Todos.Find(x => x.ScheduledDate >= dayStart && x.ScheduledDate < dayEnd);
        if (!string.IsNullOrEmpty(tag))
            query = query.Where(x => x.Category == tag);
        return query
            .OrderBy(x => x.IsCompleted)       // 未完成的排前面
            .ThenByDescending(x => x.Priority)  // 高优先级排前面
            .ThenBy(x => x.SortOrder)           // 按手动排序权重
            .ToList();
    }

    public List<TodoItem> GetTodayOpenTodos(string? tag)
    {
        return GetTodosByDateAndTag(DateTime.Today, tag)
            .Where(x => !x.IsCompleted)
            .ToList();
    }

    public List<TodoItem> GetOverdueTodos(string? tag)
    {
        var today = DateTime.Today;
        var query = _db.Todos.Find(x => x.CompletedAt == null && x.ScheduledDate < today);
        if (!string.IsNullOrEmpty(tag))
            query = query.Where(x => x.Category == tag);

        return query
            .OrderBy(x => x.ScheduledDate)
            .ThenByDescending(x => x.Priority)
            .ThenBy(x => x.SortOrder)
            .ToList();
    }

    public List<TodoItem> GetUpcomingTodos(DateTime startDate, int days, string? tag)
    {
        var start = startDate.Date;
        var end = start.AddDays(days);
        var query = _db.Todos.Find(x => x.CompletedAt == null && x.ScheduledDate >= start && x.ScheduledDate < end);
        if (!string.IsNullOrEmpty(tag))
            query = query.Where(x => x.Category == tag);

        return query
            .OrderBy(x => x.ScheduledDate)
            .ThenByDescending(x => x.Priority)
            .ThenBy(x => x.SortOrder)
            .ToList();
    }

    public List<TodoItem> GetNeedsVerificationByTag(string? tag)
    {
        var query = _db.Todos.Find(x => x.NeedsVerification && x.CompletedAt == null);
        if (!string.IsNullOrEmpty(tag))
            query = query.Where(x => x.Category == tag);

        return query
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.ScheduledDate)
            .ThenBy(x => x.SortOrder)
            .ToList();
    }

    /// <summary>获取日期范围内的所有待办（用于周视图、月视图、导出）</summary>
    public List<TodoItem> GetTodosByDateRange(DateTime start, DateTime end)
    {
        return _db.Todos.Find(x => x.ScheduledDate >= start.Date && x.ScheduledDate < end.Date.AddDays(1))
            .OrderBy(x => x.ScheduledDate)
            .ThenBy(x => x.SortOrder)
            .ToList();
    }

    /// <summary>添加待办事项，自动设置创建时间，返回包含 Id 的完整对象</summary>
    public TodoItem Add(TodoItem item)
    {
        item.CreatedAt = DateTime.Now;
        _db.Todos.Insert(item);
        ReminderService.Instance.ClearNotified(item.Id);
        return item;
    }

    /// <summary>更新已有待办事项的全部字段</summary>
    public void Update(TodoItem item)
    {
        _db.Todos.Update(item);
        ReminderService.Instance.ClearNotified(item.Id);
    }

    public TodoItem? EnsureNextRecurringTodo(TodoItem item)
    {
        if (item.RecurrenceType == TodoRecurrenceType.None)
            return null;

        var existing = _db.Todos.FindOne(x => x.RecurrenceSourceTodoId == item.Id);
        if (existing != null)
            return existing;

        var nextDate = item.RecurrenceType switch
        {
            TodoRecurrenceType.Daily => item.ScheduledDate.Date.AddDays(1),
            TodoRecurrenceType.Weekly => item.ScheduledDate.Date.AddDays(7),
            TodoRecurrenceType.Monthly => item.ScheduledDate.Date.AddMonths(1),
            _ => item.ScheduledDate.Date
        };

        var nextTodo = new TodoItem
        {
            Title = item.Title,
            Description = item.Description,
            ScheduledDate = nextDate,
            Priority = item.Priority,
            Progress = 0,
            Category = item.Category,
            ReminderTime = GetNextReminderTime(item.ReminderTime, nextDate),
            SortOrder = item.SortOrder,
            PomodoroCount = 0,
            NeedsVerification = false,
            RecurrenceType = item.RecurrenceType,
            RecurrenceSourceTodoId = item.Id,
            Subtasks = item.Subtasks.Select(subtask => new TodoSubtask
            {
                Title = subtask.Title,
                IsCompleted = false
            }).ToList()
        };

        return Add(nextTodo);
    }

    public void SnoozeReminder(int todoId, TimeSpan delay)
    {
        var todo = GetById(todoId);
        if (todo == null)
            return;

        todo.ReminderTime = DateTime.Now.Add(delay);
        Update(todo);
    }

    public void MoveReminderToTomorrow(int todoId)
    {
        var todo = GetById(todoId);
        if (todo == null)
            return;

        var source = todo.ReminderTime ?? DateTime.Now;
        var next = DateTime.Today.AddDays(1)
            .AddHours(source.Hour)
            .AddMinutes(source.Minute);
        todo.ReminderTime = next;
        Update(todo);
    }

    public List<TodoItem> Search(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return [];

        keyword = keyword.Trim();
        return _db.Todos.FindAll()
            .Where(x => ContainsKeyword(x.Title, keyword)
                || ContainsKeyword(x.Description, keyword)
                || ContainsKeyword(x.Category, keyword))
            .OrderByDescending(x => x.CreatedAt)
            .ToList();
    }

    public ReviewStats GetReviewStats(DateTime anchorDate)
    {
        var weekStart = GetWeekStart(anchorDate);
        var weekEnd = weekStart.AddDays(7);
        var allTodos = _db.Todos.FindAll().ToList();

        return new ReviewStats
        {
            NewCount = allTodos.Count(x => x.CreatedAt >= weekStart && x.CreatedAt < weekEnd),
            CompletedCount = allTodos.Count(x => x.CompletedAt >= weekStart && x.CompletedAt < weekEnd),
            OverdueCount = allTodos.Count(x => x.CompletedAt == null && x.ScheduledDate.Date < DateTime.Today),
            VerificationCount = allTodos.Count(x => x.NeedsVerification && x.CompletedAt == null)
        };
    }

    /// <summary>复制待办到指定日期，返回新建的待办对象。</summary>
    public TodoItem? CopyToDate(int id, DateTime targetDate)
    {
        var source = _db.Todos.FindById(id);
        if (source == null) return null;

        var copy = new TodoItem
        {
            Title = source.Title,
            Description = source.Description,
            ScheduledDate = targetDate.Date,
            Priority = source.Priority,
            Progress = source.IsCompleted ? 0 : source.Progress,
            Category = source.Category,
            ReminderTime = null,
            SortOrder = source.SortOrder,
            PomodoroCount = 0,
            NeedsVerification = source.NeedsVerification
        };

        return Add(copy);
    }

    /// <summary>根据 ID 永久删除待办事项</summary>
    public void Delete(int id)
    {
        _db.Todos.Delete(id);
    }

    /// <summary>
    /// 切换待办完成状态：
    /// - 未完成 → 设置 CompletedAt 为当前时间
    /// - 已完成 → 清除 CompletedAt（恢复为未完成）
    /// </summary>
    public void ToggleComplete(int id)
    {
        var item = _db.Todos.FindById(id);
        if (item == null) return;

        item.CompletedAt = item.IsCompleted ? null : DateTime.Now;
        _db.Todos.Update(item);
    }

    /// <summary>批量标记为已完成（用于一键完成功能）</summary>
    public void BatchComplete(IEnumerable<int> ids)
    {
        foreach (var id in ids)
        {
            var item = _db.Todos.FindById(id);
            if (item != null && !item.IsCompleted)
            {
                item.CompletedAt = DateTime.Now;
                _db.Todos.Update(item);
            }
        }
    }

    /// <summary>
    /// 获取某月的每日统计数据（用于月视图显示）
    /// 返回: Dictionary&lt;日期, (总数, 已完成数)&gt;
    /// </summary>
    public Dictionary<DateTime, (int Total, int Completed)> GetMonthStats(int year, int month)
    {
        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1);
        var todos = _db.Todos.Find(x => x.ScheduledDate >= start && x.ScheduledDate < end).ToList();

        return todos
            .GroupBy(x => x.ScheduledDate.Date)
            .ToDictionary(
                g => g.Key,
                g => (Total: g.Count(), Completed: g.Count(x => x.IsCompleted))
            );
    }

    /// <summary>根据 ID 获取单个待办事项</summary>
    public TodoItem? GetById(int id)
    {
        return _db.Todos.FindById(id);
    }

    /// <summary>获取所有标记为待验证的待办事项</summary>
    public List<TodoItem> GetNeedsVerification()
    {
        return _db.Todos.Find(x => x.NeedsVerification)
            .OrderByDescending(x => x.CompletedAt)
            .ThenByDescending(x => x.ScheduledDate)
            .ToList();
    }

    private static bool ContainsKeyword(string? value, string keyword)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime? GetNextReminderTime(DateTime? reminderTime, DateTime nextDate)
    {
        if (!reminderTime.HasValue)
            return null;

        return nextDate
            .AddHours(reminderTime.Value.Hour)
            .AddMinutes(reminderTime.Value.Minute);
    }

    private static DateTime GetWeekStart(DateTime date)
    {
        var diff = date.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)date.DayOfWeek - 1;
        return date.Date.AddDays(-diff);
    }
}
