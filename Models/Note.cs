// ============================================================================
// Note.cs — 记事本条目数据模型
// 对应数据库中的 "notes" 集合
// ============================================================================

namespace KiteTodo.Models;

/// <summary>
/// 记事本条目，存储在 LiteDB 的 notes 集合中。
/// 每一条记录代表一个笔记，包含标题和纯文本内容。
/// </summary>
public class Note
{
    /// <summary>LiteDB 自增主键</summary>
    public int Id { get; set; }

    /// <summary>父笔记 ID，null 表示顶级笔记</summary>
    public int? ParentId { get; set; }

    /// <summary>同级排序值，越小越靠前</summary>
    public int SortOrder { get; set; }

    /// <summary>笔记标题</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>笔记纯文本内容</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>旧版富文本内容，保留字段以兼容历史数据</summary>
    public string? RichContent { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>最后修改时间</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
