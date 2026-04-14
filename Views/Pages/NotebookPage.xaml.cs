// ============================================================================
// NotebookPage.xaml.cs — 记事本页面的代码后台
// ============================================================================

using System.Windows;
using System.Windows.Controls;
using KiteTodo.Models;
using KiteTodo.ViewModels;

namespace KiteTodo.Views.Pages;

/// <summary>
/// 记事本页面，左侧标题列表，右侧编辑区。
/// 支持新建/删除笔记，右键菜单可"转化为待办"。
/// </summary>
public partial class NotebookPage : Page
{
    private readonly NotebookViewModel _vm = new();

    public NotebookPage()
    {
        InitializeComponent();
        DataContext = _vm;
    }

    /// <summary>点击保存按钮时保存当前笔记</summary>
    private void OnSaveNote(object sender, System.Windows.RoutedEventArgs e)
    {
        _vm.SaveCurrentNoteCommand.Execute(null);
    }

    /// <summary>右键菜单 → 删除笔记</summary>
    private void OnDeleteNote(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is Note note)
            _vm.DeleteNoteCommand.Execute(note);
    }

    /// <summary>右键菜单 → 转化为待办</summary>
    private void OnConvertToTodo(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is Note note)
            _vm.ConvertToTodoCommand.Execute(note);
    }
}
