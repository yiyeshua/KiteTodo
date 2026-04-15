// ============================================================================
// PomodoroRecord.cs - 番茄钟记录数据模型
// 对应数据库中的 "pomodoros" 集合，记录每次番茄钟的使用情况
// ============================================================================

namespace KiteTodo.Models;

/// <summary>
/// 番茄钟记录实体类，每完成一次专注时段就插入一条记录。
/// </summary>
public class PomodoroRecord
{
    /// <summary>LiteDB 自增主键</summary>
    public int Id { get; set; }

    /// <summary>关联的待办事项 ID（可选，预留功能）</summary>
    public int? TodoId { get; set; }

    /// <summary>本次专注的开始时间</summary>
    public DateTime StartTime { get; set; }

    /// <summary>专注时长（分钟）</summary>
    public int DurationMinutes { get; set; }

    /// <summary>是否完整完成（中途取消为 false）</summary>
    public bool IsCompleted { get; set; }

    /// <summary>专注备注（一句话记录这次产出了什么）</summary>
    public string? Note { get; set; }

    /// <summary>心情打卡（0=未选, 1=高效, 2=焦虑, 3=疲惫, 4=轻松）</summary>
    public int Mood { get; set; }
}
