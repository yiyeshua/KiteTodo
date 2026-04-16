// ============================================================================
// BacklogService.cs - 事项池业务服务
// 封装待排期事项的 CRUD 操作和转待办功能
// ============================================================================

using KiteTodo.Models;

namespace KiteTodo.Services;

/// <summary>
/// 事项池服务，提供待排期事项的增删改查和转待办功能。
/// </summary>
public class BacklogService
{
    private readonly DatabaseService _db = DatabaseService.Instance;
    private readonly TodoService _todoService = new();

    /// <summary>获取所有未排期事项（按优先级倒序、创建时间倒序）</summary>
    public List<BacklogItem> GetActive()
    {
        return _db.Backlogs.Find(x => x.Status != BacklogItem.StatusScheduled)
            .OrderByDescending(x => x.Priority)
            .ThenByDescending(x => x.CreatedAt)
            .ToList();
    }

    /// <summary>获取所有事项（包括已排期的历史记录）</summary>
    public List<BacklogItem> GetAll()
    {
        return _db.Backlogs.FindAll()
            .OrderByDescending(x => x.Priority)
            .ThenByDescending(x => x.CreatedAt)
            .ToList();
    }

    /// <summary>按状态筛选</summary>
    public List<BacklogItem> GetByStatus(int status)
    {
        return _db.Backlogs.Find(x => x.Status == status)
            .OrderByDescending(x => x.Priority)
            .ThenByDescending(x => x.CreatedAt)
            .ToList();
    }

    /// <summary>添加事项</summary>
    public BacklogItem Add(BacklogItem item)
    {
        item.CreatedAt = DateTime.Now;
        _db.Backlogs.Insert(item);
        return item;
    }

    /// <summary>更新事项</summary>
    public void Update(BacklogItem item)
    {
        _db.Backlogs.Update(item);
    }

    /// <summary>删除事项</summary>
    public void Delete(int id)
    {
        _db.Backlogs.Delete(id);
    }

    /// <summary>
    /// 将事项池事项转为待办事项（排期到指定日期）。
    /// 事项标题→待办标题，事项描述→待办描述，并记录关联。
    /// </summary>
    public TodoItem ConvertToTodo(int backlogId, DateTime scheduledDate)
    {
        var item = _db.Backlogs.FindById(backlogId);
        if (item == null) return null!;

        var todo = new TodoItem
        {
            Title = item.Title,
            Description = item.Description,
            ScheduledDate = scheduledDate,
            Priority = item.Priority,
            Category = item.Category
        };
        _todoService.Add(todo);

        // 标记事项为已排期
        item.Status = BacklogItem.StatusScheduled;
        item.ScheduledTodoId = todo.Id;
        item.ScheduledDate = scheduledDate;
        _db.Backlogs.Update(item);

        return todo;
    }
}
