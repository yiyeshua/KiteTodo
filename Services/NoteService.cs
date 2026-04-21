// ============================================================================
// NoteService.cs — 记事本业务服务
// 封装所有笔记的 CRUD 操作和转待办功能
// ============================================================================

using KiteTodo.Models;

namespace KiteTodo.Services;

/// <summary>
/// 记事本服务，提供笔记的增删改查和转待办功能。
/// 每个 ViewModel 各自 new 一个实例使用（内部共享 DatabaseService 单例）。
/// </summary>
public class NoteService
{
    private readonly DatabaseService _db = DatabaseService.Instance;
    private readonly TodoService _todoService = new();
    private readonly BacklogService _backlogService = new();

    /// <summary>获取所有笔记（按修改时间倒序排列）</summary>
    public List<Note> GetAll()
    {
        return _db.Notes.FindAll()
            .OrderByDescending(x => x.UpdatedAt)
            .ToList();
    }

    /// <summary>根据 ID 获取单个笔记</summary>
    public Note? GetById(int id)
    {
        return _db.Notes.FindById(id);
    }

    /// <summary>添加新笔记，自动设置创建和修改时间</summary>
    public Note Add(Note note)
    {
        note.CreatedAt = DateTime.Now;
        note.UpdatedAt = DateTime.Now;
        _db.Notes.Insert(note);
        return note;
    }

    /// <summary>更新已有笔记，自动刷新修改时间</summary>
    public void Update(Note note)
    {
        note.UpdatedAt = DateTime.Now;
        _db.Notes.Update(note);
    }

    /// <summary>根据 ID 删除笔记</summary>
    public void Delete(int id)
    {
        _db.Notes.Delete(id);
    }

    /// <summary>
    /// 将笔记转化为待办事项。
    /// 笔记标题 → 待办标题，笔记内容 → 待办描述。
    /// </summary>
    public void ConvertToTodo(int noteId, DateTime scheduledDate)
    {
        var note = GetById(noteId);
        if (note == null) return;

        var todo = new TodoItem
        {
            Title = note.Title,
            Description = note.Content,
            ScheduledDate = scheduledDate,
            Priority = 0
        };
        _todoService.Add(todo);
    }

    /// <summary>
    /// 将笔记转化为事项池事项。
    /// 笔记标题 → 事项标题，笔记内容 → 事项描述。
    /// </summary>
    public void ConvertToBacklog(int noteId)
    {
        var note = GetById(noteId);
        if (note == null) return;

        var backlog = new BacklogItem
        {
            Title = note.Title,
            Description = note.Content,
            Priority = 0,
            Status = BacklogItem.StatusPending
        };
        _backlogService.Add(backlog);
    }

    public List<Note> Search(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return [];

        keyword = keyword.Trim();
        return _db.Notes.FindAll()
            .Where(x => (!string.IsNullOrWhiteSpace(x.Title) && x.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(x.Content) && x.Content.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(x => x.UpdatedAt)
            .ToList();
    }
}
