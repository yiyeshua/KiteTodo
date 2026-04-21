// ============================================================================
// TodoItem.cs - 待办事项数据模型
// 对应数据库中的 "todos" 集合，是整个应用最核心的数据结构
// ============================================================================

namespace KiteTodo.Models;

/// <summary>
/// 待办事项实体类，存储在 LiteDB 的 todos 集合中。
/// 每一条记录代表一个待办事项。
/// </summary>
public class TodoItem
{
    /// <summary>LiteDB 自增主键</summary>
    public int Id { get; set; }

    /// <summary>待办标题（必填）</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>待办详细描述（可选）</summary>
    public string? Description { get; set; }

    /// <summary>计划日期（按天分组查询的依据）</summary>
    public DateTime ScheduledDate { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>完成时间（null 表示未完成，有值表示已完成）</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>优先级：0=无, 1=低, 2=中, 3=高</summary>
    public int Priority { get; set; }

    /// <summary>完成进度百分比：0-100，步进10%。设为100时自动标记完成</summary>
    public int Progress { get; set; }

    /// <summary>分类标签</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>提醒时间（到达该时间时弹出 Toast 通知）</summary>
    public DateTime? ReminderTime { get; set; }

    /// <summary>排序权重（用于同一天内的手动排序）</summary>
    public int SortOrder { get; set; }

    /// <summary>关联的番茄钟完成次数</summary>
    public int PomodoroCount { get; set; }

    /// <summary>是否标记为待验证（功能已完成但未经验证）</summary>
    public bool NeedsVerification { get; set; }

    /// <summary>周期任务规则</summary>
    public TodoRecurrenceType RecurrenceType { get; set; }

    /// <summary>由上一条周期任务生成时，记录其来源待办 Id。</summary>
    public int? RecurrenceSourceTodoId { get; set; }

    /// <summary>子任务清单</summary>
    public List<TodoSubtask> Subtasks { get; set; } = [];

    /// <summary>是否已完成（计算属性，根据 CompletedAt 是否有值判断）</summary>
    public bool IsCompleted => CompletedAt.HasValue;

    public bool IsOverdue => !IsCompleted && ScheduledDate.Date < DateTime.Today;

    public int CompletedSubtaskCount => Subtasks.Count(x => x.IsCompleted);

    public int TotalSubtaskCount => Subtasks.Count;

    public bool HasSubtasks => TotalSubtaskCount > 0;

    public string SubtaskSummary => HasSubtasks ? $"清单 {CompletedSubtaskCount}/{TotalSubtaskCount}" : string.Empty;

    public string RecurrenceText => RecurrenceType switch
    {
        TodoRecurrenceType.Daily => "每天重复",
        TodoRecurrenceType.Weekly => "每周重复",
        TodoRecurrenceType.Monthly => "每月重复",
        _ => string.Empty
    };
}
