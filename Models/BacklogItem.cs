namespace KiteTodo.Models;

/// <summary>
/// 待排期事项（事项池）数据模型。
/// 用于记录暂时无法排期、等待他人协作、或需要后续跟进的重要事项。
/// </summary>
public class BacklogItem
{
    /// <summary>LiteDB 自增主键</summary>
    public int Id { get; set; }

    /// <summary>事项标题</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>详细描述</summary>
    public string? Description { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>优先级：0=无, 1=低, 2=中, 3=高</summary>
    public int Priority { get; set; }

    /// <summary>分类标签</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// 状态：0=待处理, 1=等待他人, 2=已排期
    /// </summary>
    public int Status { get; set; }

    /// <summary>等待说明（例如"等张三完成底盘对接"）</summary>
    public string? WaitingFor { get; set; }

    /// <summary>转为待办后关联的 TodoItem Id（null 表示未转换）</summary>
    public int? ScheduledTodoId { get; set; }

    /// <summary>排期日期（转为待办时记录）</summary>
    public DateTime? ScheduledDate { get; set; }

    // 状态常量
    public const int StatusPending = 0;
    public const int StatusWaiting = 1;
    public const int StatusScheduled = 2;

    /// <summary>状态显示文本</summary>
    public string StatusText => Status switch
    {
        StatusWaiting => "等待他人",
        StatusScheduled => "已排期",
        _ => "待处理"
    };

    /// <summary>是否已排期</summary>
    public bool IsScheduled => Status == StatusScheduled;
}
