// ============================================================================
// NotebookViewModel.cs — 记事本页面的 ViewModel
// ============================================================================

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KiteTodo.Models;
using KiteTodo.Services;

namespace KiteTodo.ViewModels;

/// <summary>
/// 记事本页面 ViewModel，管理笔记列表、选中笔记、新建/编辑/删除/转待办功能。
/// </summary>
public partial class NotebookViewModel : ObservableObject
{
    private readonly NoteService _noteService = new();
    private readonly HashSet<int> _collapsedNoteIds = [];
    private bool _isSaving;

    /// <summary>所有笔记列表（按修改时间倒序）</summary>
    [ObservableProperty]
    private ObservableCollection<Note> _notes = new();

    /// <summary>左侧层级展示列表（扁平化）</summary>
    [ObservableProperty]
    private ObservableCollection<NotebookListItem> _visibleNotes = new();

    /// <summary>当前选中的左侧列表项</summary>
    [ObservableProperty]
    private NotebookListItem? _selectedListItem;

    /// <summary>左侧摘要</summary>
    [ObservableProperty]
    private string _noteSummary = "共 0 条";

    /// <summary>当前选中的笔记（绑定到右侧编辑区）</summary>
    [ObservableProperty]
    private Note? _selectedNote;

    /// <summary>编辑区标题文本（双向绑定）</summary>
    [ObservableProperty]
    private string _editTitle = string.Empty;

    /// <summary>编辑区内容文本（双向绑定）</summary>
    [ObservableProperty]
    private string _editContent = string.Empty;

    public NotebookViewModel()
    {
        LoadNotes();
    }

    partial void OnSelectedListItemChanged(NotebookListItem? value)
    {
        if (value?.Note == null)
        {
            SelectedNote = null;
            return;
        }

        if (SelectedNote?.Id == value.Note.Id)
            return;

        SelectedNote = value.Note;
    }

    /// <summary>当选中笔记变化时，自动将数据填充到编辑区</summary>
    partial void OnSelectedNoteChanged(Note? value)
    {
        if (_isSaving) return;
        if (value != null)
        {
            SelectedListItem = VisibleNotes.FirstOrDefault(item => item.Note.Id == value.Id);
            EditTitle = value.Title;
            EditContent = value.Content;
        }
        else
        {
            EditTitle = string.Empty;
            EditContent = string.Empty;
        }
    }

    /// <summary>保存当前编辑的笔记</summary>
    [RelayCommand]
    private void SaveCurrentNote()
    {
        if (SelectedNote == null || _isSaving) return;
        SaveNoteSnapshot(SelectedNote.Id, EditTitle, EditContent);
    }

    public bool SaveNoteSnapshot(int noteId, string title, string content)
    {
        if (_isSaving)
            return false;

        var target = _noteService.GetById(noteId);
        if (target == null)
            return false;

        _isSaving = true;
        try
        {
            target.Title = title;
            target.Content = content;
            target.RichContent = null;
            _noteService.Update(target);
            LoadNotes(noteId);
            SelectedNote = Notes.FirstOrDefault(n => n.Id == noteId);
            return true;
        }
        finally
        {
            _isSaving = false;
        }
    }

    /// <summary>从数据库加载所有笔记</summary>
    public void LoadNotes(int? preferredSelectedId = null)
    {
        var list = _noteService.GetAll();
        var targetId = preferredSelectedId ?? SelectedNote?.Id;
        if (targetId.HasValue)
            ExpandAncestors(list, targetId.Value);

        Notes = new ObservableCollection<Note>(list);
        VisibleNotes = new ObservableCollection<NotebookListItem>(BuildVisibleNotes(list));
        NoteSummary = $"共 {Notes.Count} 条";

        if (targetId.HasValue)
        {
            SelectedListItem = VisibleNotes.FirstOrDefault(item => item.Note.Id == targetId.Value);
            SelectedNote = SelectedListItem?.Note;
        }
        else if (VisibleNotes.Count == 0)
        {
            SelectedListItem = null;
            SelectedNote = null;
        }
    }

    /// <summary>新建一个空白笔记</summary>
    [RelayCommand]
    private void CreateNote()
    {
        var note = new Note
        {
            Title = "新笔记",
            Content = string.Empty,
            RichContent = null
        };
        _noteService.Add(note);
        LoadNotes(note.Id);
        SelectedNote = Notes.FirstOrDefault(n => n.Id == note.Id);
    }

    public void CreateChildNote(int parentId)
    {
        _collapsedNoteIds.Remove(parentId);
        var note = _noteService.AddChild(parentId);
        LoadNotes(note.Id);
        SelectedNote = Notes.FirstOrDefault(n => n.Id == note.Id);
    }

    public void ToggleChildren(int noteId)
    {
        var list = Notes.ToList();
        var currentSelectedId = SelectedNote?.Id;

        var isCollapsing = !_collapsedNoteIds.Contains(noteId);
        if (isCollapsing)
            _collapsedNoteIds.Add(noteId);
        else
            _collapsedNoteIds.Remove(noteId);

        if (isCollapsing && currentSelectedId.HasValue && IsDescendantOf(currentSelectedId.Value, noteId, list))
        {
            LoadNotes(noteId);
            return;
        }

        LoadNotes(currentSelectedId);
    }

    /// <summary>删除选中的笔记</summary>
    public void DeleteNote(Note note, bool deleteChildren)
    {
        var currentSelectedId = SelectedNote?.Id;
        var fallbackSelectedId = currentSelectedId;

        if (currentSelectedId == note.Id)
        {
            fallbackSelectedId = deleteChildren
                ? null
                : _noteService.GetChildren(note.Id).FirstOrDefault()?.Id;
        }

        _noteService.Delete(note.Id, deleteChildren);
        LoadNotes(currentSelectedId == note.Id ? fallbackSelectedId : currentSelectedId);
    }

    /// <summary>将笔记转化为待办（标题→待办标题，内容→待办描述）</summary>
    [RelayCommand]
    private void ConvertToTodo(Note note)
    {
        _noteService.ConvertToTodo(note.Id, DateTime.Today);
        // 转待办后删除笔记
        _noteService.Delete(note.Id);
        LoadNotes();
        if (SelectedNote?.Id == note.Id)
            SelectedNote = null;
    }

    /// <summary>将笔记转入事项池</summary>
    [RelayCommand]
    private void ConvertToBacklog(Note note)
    {
        MoveNoteToBacklog(note, deleteAfterTransfer: true);
    }

    public void MoveNoteToBacklog(Note note, bool deleteAfterTransfer)
    {
        _noteService.ConvertToBacklog(note.Id);

        if (deleteAfterTransfer)
            _noteService.Delete(note.Id);

        LoadNotes(deleteAfterTransfer ? null : note.Id);
        if (deleteAfterTransfer && SelectedNote?.Id == note.Id)
            SelectedNote = null;
        else if (!deleteAfterTransfer)
            SelectedNote = Notes.FirstOrDefault(n => n.Id == note.Id);
    }

    public void SelectNote(int noteId)
    {
        LoadNotes(noteId);
        SelectedNote = Notes.FirstOrDefault(n => n.Id == noteId);
    }

    private List<NotebookListItem> BuildVisibleNotes(List<Note> notes)
    {
        var result = new List<NotebookListItem>();
        var noteIds = notes.Select(note => note.Id).ToHashSet();
        var childrenLookup = notes
            .Where(note => note.ParentId.HasValue)
            .GroupBy(note => note.ParentId)
            .ToDictionary(
                group => group.Key!.Value,
                group => OrderNotes(group).ToList());

        var roots = OrderNotes(notes.Where(note => !note.ParentId.HasValue || !noteIds.Contains(note.ParentId.Value)));
        foreach (var root in roots)
            AppendNote(result, root, 0, childrenLookup);

        return result;
    }

    private static IEnumerable<Note> OrderNotes(IEnumerable<Note> notes)
    {
        return notes
            .OrderBy(note => note.SortOrder)
            .ThenByDescending(note => note.UpdatedAt)
            .ThenBy(note => note.Id);
    }

    private void AppendNote(
        ICollection<NotebookListItem> result,
        Note note,
        int depth,
        IReadOnlyDictionary<int, List<Note>> childrenLookup)
    {
        childrenLookup.TryGetValue(note.Id, out var children);
        var childList = children ?? [];
        var isExpanded = !_collapsedNoteIds.Contains(note.Id);

        var siblingCount = 0;
        var siblingIndex = 0;
        if (depth > 0 && note.ParentId.HasValue && childrenLookup.TryGetValue(note.ParentId.Value, out var siblings))
        {
            siblingCount = siblings.Count;
            siblingIndex = siblings.FindIndex(item => item.Id == note.Id);
        }

        var isLastChild = depth > 0 && siblingCount > 0 && siblingIndex == siblingCount - 1;
        result.Add(new NotebookListItem(note, depth, childList.Count, isExpanded, isLastChild));

        if (!isExpanded)
            return;

        foreach (var child in childList)
            AppendNote(result, child, depth + 1, childrenLookup);
    }

    private void ExpandAncestors(IEnumerable<Note> notes, int noteId)
    {
        var noteLookup = notes.ToDictionary(note => note.Id);
        if (!noteLookup.TryGetValue(noteId, out var current))
            return;

        while (current.ParentId.HasValue && noteLookup.TryGetValue(current.ParentId.Value, out var parent))
        {
            _collapsedNoteIds.Remove(parent.Id);
            current = parent;
        }
    }

    private static bool IsDescendantOf(int noteId, int ancestorId, IEnumerable<Note> notes)
    {
        var noteLookup = notes.ToDictionary(note => note.Id);
        if (!noteLookup.TryGetValue(noteId, out var current))
            return false;

        while (current.ParentId.HasValue && noteLookup.TryGetValue(current.ParentId.Value, out var parent))
        {
            if (parent.Id == ancestorId)
                return true;

            current = parent;
        }

        return false;
    }
}
