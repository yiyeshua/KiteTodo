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
    private bool _isSaving;
    private const int NotesPageSize = 8;

    /// <summary>所有笔记列表（按修改时间倒序）</summary>
    [ObservableProperty]
    private ObservableCollection<Note> _notes = new();

    /// <summary>当前页显示的笔记列表</summary>
    [ObservableProperty]
    private ObservableCollection<Note> _pagedNotes = new();

    /// <summary>当前分页索引（从 0 开始）</summary>
    [ObservableProperty]
    private int _pageIndex;

    /// <summary>分页信息文本</summary>
    [ObservableProperty]
    private string _pageInfo = "第 1 / 1 页";

    /// <summary>是否可以上一页</summary>
    [ObservableProperty]
    private bool _canGoPreviousPage;

    /// <summary>是否可以下一页</summary>
    [ObservableProperty]
    private bool _canGoNextPage;

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

    /// <summary>当选中笔记变化时，自动将数据填充到编辑区</summary>
    partial void OnSelectedNoteChanged(Note? value)
    {
        if (_isSaving) return;
        if (value != null)
        {
            MovePageToNote(value.Id);
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
        Notes = new ObservableCollection<Note>(list);
        RefreshPagedNotes();

        if (preferredSelectedId.HasValue)
            MovePageToNote(preferredSelectedId.Value);
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

    /// <summary>删除选中的笔记</summary>
    [RelayCommand]
    private void DeleteNote(Note note)
    {
        var currentSelectedId = SelectedNote?.Id;
        _noteService.Delete(note.Id);
        LoadNotes(currentSelectedId == note.Id ? null : currentSelectedId);
        // 如果删除的是当前选中的，清空选中
        if (SelectedNote?.Id == note.Id)
            SelectedNote = null;

        RefreshPagedNotes();
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

    [RelayCommand(CanExecute = nameof(CanGoPreviousPage))]
    private void GoPreviousPage()
    {
        if (PageIndex <= 0)
            return;

        PageIndex--;
        RefreshPagedNotes();
    }

    [RelayCommand(CanExecute = nameof(CanGoNextPage))]
    private void GoNextPage()
    {
        if (PageIndex >= GetTotalPages() - 1)
            return;

        PageIndex++;
        RefreshPagedNotes();
    }

    private void RefreshPagedNotes()
    {
        var totalPages = GetTotalPages();
        if (PageIndex >= totalPages)
            PageIndex = Math.Max(0, totalPages - 1);

        var pageItems = Notes
            .Skip(PageIndex * NotesPageSize)
            .Take(NotesPageSize)
            .ToList();

        PagedNotes = new ObservableCollection<Note>(pageItems);
        PageInfo = $"第 {PageIndex + 1} / {totalPages} 页，共 {Notes.Count} 条";
        CanGoPreviousPage = PageIndex > 0;
        CanGoNextPage = PageIndex < totalPages - 1;
        GoPreviousPageCommand.NotifyCanExecuteChanged();
        GoNextPageCommand.NotifyCanExecuteChanged();
    }

    private int GetTotalPages()
    {
        return Math.Max(1, (int)Math.Ceiling(Notes.Count / (double)NotesPageSize));
    }

    private void MovePageToNote(int noteId)
    {
        var index = Notes
            .Select((note, itemIndex) => new { Note = note, Index = itemIndex })
            .FirstOrDefault(entry => entry.Note.Id == noteId)?.Index;

        if (!index.HasValue)
            return;

        var targetPage = index.Value / NotesPageSize;
        if (targetPage == PageIndex && PagedNotes.Any(n => n.Id == noteId))
            return;

        PageIndex = targetPage;
        RefreshPagedNotes();
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
}
