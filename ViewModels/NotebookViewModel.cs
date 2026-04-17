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

    /// <summary>所有笔记列表（按修改时间倒序）</summary>
    [ObservableProperty]
    private ObservableCollection<Note> _notes = new();

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
        _isSaving = true;
        try
        {
            var selectedId = SelectedNote.Id;
            SelectedNote.Title = EditTitle;
            SelectedNote.Content = EditContent;
            SelectedNote.RichContent = null;
            _noteService.Update(SelectedNote);
            LoadNotes();
            SelectedNote = Notes.FirstOrDefault(n => n.Id == selectedId);
        }
        finally
        {
            _isSaving = false;
        }
    }

    /// <summary>从数据库加载所有笔记</summary>
    public void LoadNotes()
    {
        var list = _noteService.GetAll();
        Notes = new ObservableCollection<Note>(list);
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
        LoadNotes();
        SelectedNote = Notes.FirstOrDefault(n => n.Id == note.Id);
    }

    /// <summary>删除选中的笔记</summary>
    [RelayCommand]
    private void DeleteNote(Note note)
    {
        _noteService.Delete(note.Id);
        LoadNotes();
        // 如果删除的是当前选中的，清空选中
        if (SelectedNote?.Id == note.Id)
            SelectedNote = null;
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
}
