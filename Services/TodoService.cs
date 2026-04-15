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
        return item;
    }

    /// <summary>更新已有待办事项的全部字段</summary>
    public void Update(TodoItem item)
    {
        _db.Todos.Update(item);
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
}
