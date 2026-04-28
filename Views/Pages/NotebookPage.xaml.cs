// ============================================================================
// NotebookPage.xaml.cs — 记事本页面的代码后台
// ============================================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using System.ComponentModel;
using KiteTodo.Helpers;
using KiteTodo.Models;
using KiteTodo.Services;
using KiteTodo.ViewModels;
using KiteTodo.Views;

namespace KiteTodo.Views.Pages;

/// <summary>
/// 记事本页面，左侧标题列表，右侧编辑区。
/// 支持新建/删除笔记，右键菜单可"转化为待办"。
/// </summary>
public partial class NotebookPage : Page
{
    private static NotebookPage? _activeInstance;
    private readonly NotebookViewModel _vm = new();
    private const double MinEditorFontSize = 12;
    private const double MaxEditorFontSize = 24;
    private const double EditorFontStep = 1;
    private const double CurrentLineMinHeight = 22;
    private ScrollViewer? _editorScrollViewer;
    private bool _editorVisualRefreshQueued;
    private bool _lineNumberRefreshPending = true;
    private int _lastRenderedLineCount = -1;
    private bool _isLoadingEditorText;
    private bool _suppressNextEditorReload;
    private int? _currentEditorNoteId;
    private string _savedTitleSnapshot = string.Empty;
    private string _savedContentSnapshot = string.Empty;
    private bool _allowNavigationAfterConfirmation;

    public static bool ConfirmPendingChangesForActivePage()
    {
        return _activeInstance?.TryConfirmPendingChanges("退出程序前检测到记事本有未保存内容，是否保存？") ?? true;
    }

    public NotebookPage()
    {
        InitializeComponent();
        DataContext = _vm;
        _vm.PropertyChanged += OnViewModelPropertyChanged;
        Loaded += (_, _) =>
        {
            _activeInstance = this;
            _allowNavigationAfterConfirmation = false;
            if (NavigationService != null)
            {
                NavigationService.Navigating -= OnNavigationServiceNavigating;
                NavigationService.Navigating += OnNavigationServiceNavigating;
            }

            ApplySearchNavigationRequest();
            LoadEditorFromViewModel();
        };
        Unloaded += (_, _) =>
        {
            if (NavigationService != null)
                NavigationService.Navigating -= OnNavigationServiceNavigating;

            _allowNavigationAfterConfirmation = false;

            if (ReferenceEquals(_activeInstance, this))
                _activeInstance = null;
        };
    }

    /// <summary>点击保存按钮时保存当前笔记</summary>
    private void OnSaveNote(object sender, System.Windows.RoutedEventArgs e)
    {
        SaveCurrentNotePreservingEditor();
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

    /// <summary>右键菜单 → 转入事项池</summary>
    private void OnConvertToBacklog(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is Note note)
        {
            var choice = ShowBacklogTransferDialog(note);

            if (choice == BacklogTransferDialog.TransferChoice.Cancel)
                return;

            var deleteAfterTransfer = choice == BacklogTransferDialog.TransferChoice.Move;
            _vm.MoveNoteToBacklog(note, deleteAfterTransfer);
            AlertService.ShowCornerToast(deleteAfterTransfer ? "已移动到事项池" : "已复制到事项池");
        }
    }

    private BacklogTransferDialog.TransferChoice ShowBacklogTransferDialog(Note note)
    {
        var dialog = new BacklogTransferDialog(note.Title)
        {
            Owner = Window.GetWindow(this)
        };

        dialog.ShowDialog();
        return dialog.SelectedChoice;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NotebookViewModel.SelectedNote))
        {
            if (_suppressNextEditorReload)
            {
                _suppressNextEditorReload = false;
                QueueEditorVisualRefresh(true);
                return;
            }

            LoadEditorFromViewModel();
        }
    }

    private void LoadEditorFromViewModel()
    {
        if (NoteEditor is null)
            return;

        _isLoadingEditorText = true;
        try
        {
            _currentEditorNoteId = _vm.SelectedNote?.Id;
            var editorContent = _vm.EditContent ?? string.Empty;
            var titleContent = _vm.EditTitle ?? string.Empty;

            _savedTitleSnapshot = titleContent;
            _savedContentSnapshot = editorContent;

            NoteEditor.Text = editorContent;
            NoteTitleEditor.Text = titleContent;
            NoteEditor.CaretIndex = 0;
        }
        finally
        {
            _isLoadingEditorText = false;
        }

        QueueEditorVisualRefresh(true);
    }

    private void ApplySearchNavigationRequest()
    {
        if (!SearchNavigationRequest.PendingNoteId.HasValue)
            return;

        _vm.SelectNote(SearchNavigationRequest.PendingNoteId.Value);
        SearchNavigationRequest.ClearNote();
    }

    private void OnDecreaseFontSize(object sender, RoutedEventArgs e)
    {
        SetEditorFontSize(NoteEditor.FontSize - EditorFontStep);
    }

    private void OnIncreaseFontSize(object sender, RoutedEventArgs e)
    {
        SetEditorFontSize(NoteEditor.FontSize + EditorFontStep);
    }

    private void SetEditorFontSize(double fontSize)
    {
        var size = Math.Max(MinEditorFontSize, Math.Min(MaxEditorFontSize, fontSize));
        NoteEditor.FontSize = size;
        LineNumberGutter.FontSize = size;
        LineNumberGutter.LineHeight = GetEditorLineHeight();
        LineNumberGutter.EditorPaddingTop = NoteEditor.Padding.Top;
        QueueEditorVisualRefresh(true);
    }

    private void OnNoteEditorLoaded(object sender, RoutedEventArgs e)
    {
        _editorScrollViewer = FindDescendant<ScrollViewer>(NoteEditor);

        if (_editorScrollViewer != null)
        {
            _editorScrollViewer.ScrollChanged -= OnEditorScrollChanged;
            _editorScrollViewer.ScrollChanged += OnEditorScrollChanged;
        }

        LineNumberGutter.LineHeight = GetEditorLineHeight();
        LineNumberGutter.EditorPaddingTop = NoteEditor.Padding.Top;

        QueueEditorVisualRefresh(true);
    }

    private void OnNoteEditorSizeChanged(object sender, SizeChangedEventArgs e)
    {
        QueueEditorVisualRefresh(true);
    }

    private void OnNoteEditorTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoadingEditorText)
            return;

        var currentLineCount = Math.Max(1, NoteEditor.LineCount);
        QueueEditorVisualRefresh(currentLineCount != _lastRenderedLineCount);
    }

    private void OnNoteTitleTextChanged(object sender, TextChangedEventArgs e)
    {
    }

    private void OnNoteEditorSelectionChanged(object sender, RoutedEventArgs e)
    {
        QueueEditorVisualRefresh(false);
    }

    private void OnNoteEditorPointerChanged(object sender, MouseButtonEventArgs e)
    {
        QueueEditorVisualRefresh(false);
    }

    private void OnNoteEditorKeyUp(object sender, KeyEventArgs e)
    {
        QueueEditorVisualRefresh(false);
    }

    private void OnNoteEditorGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        QueueEditorVisualRefresh(false);
    }

    private void OnNoteEditorPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
            return;

        SetEditorFontSize(NoteEditor.FontSize + (e.Delta > 0 ? EditorFontStep : -EditorFontStep));
        e.Handled = true;
    }

    private void OnEditorScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        LineNumberGutter.VerticalOffset = e.VerticalOffset;

        QueueEditorVisualRefresh(false);
    }

    private void OnAutoWrapChanged(object sender, RoutedEventArgs e)
    {
        if (NoteEditor is null)
            return;

        var isWrapped = AutoWrapToggle.IsChecked != false;
        NoteEditor.TextWrapping = isWrapped ? TextWrapping.Wrap : TextWrapping.NoWrap;
        NoteEditor.HorizontalScrollBarVisibility = isWrapped ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
        QueueEditorVisualRefresh(true);
    }

    private void QueueEditorVisualRefresh(bool refreshLineNumbers)
    {
        _lineNumberRefreshPending |= refreshLineNumbers;
        if (_editorVisualRefreshQueued)
            return;

        _editorVisualRefreshQueued = true;
        Dispatcher.BeginInvoke(() =>
        {
            _editorVisualRefreshQueued = false;

            if (_lineNumberRefreshPending)
            {
                RefreshLineNumbers();
                _lineNumberRefreshPending = false;
            }

            UpdateCurrentLineHighlight();
        }, DispatcherPriority.Background);
    }

    private void OnNavigationServiceNavigating(object? sender, NavigatingCancelEventArgs e)
    {
        if (_allowNavigationAfterConfirmation)
            return;

        if (!TryConfirmPendingChanges("离开记事本页面前检测到有未保存内容，是否保存？"))
            e.Cancel = true;
    }

    private bool TryConfirmPendingChanges(string message)
    {
        if (!HasUnsavedChanges())
            return true;

        var result = MessageBox.Show(
            message,
            "记事本",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Cancel)
            return false;

        if (result == MessageBoxResult.Yes)
        {
            SaveCurrentNotePreservingEditor();
            if (HasUnsavedChanges())
                return false;
        }

        _allowNavigationAfterConfirmation = true;

        return true;
    }

    private bool HasUnsavedChanges()
    {
        if (!_currentEditorNoteId.HasValue)
            return false;

        var editorText = NoteEditor.Text ?? string.Empty;
        var titleText = NoteTitleEditor.Text ?? string.Empty;

        return !string.Equals(_savedTitleSnapshot, titleText, StringComparison.Ordinal)
            || !string.Equals(_savedContentSnapshot, editorText, StringComparison.Ordinal);
    }

    private void SaveCurrentNotePreservingEditor()
    {
        if (!_currentEditorNoteId.HasValue)
            return;

        var editorText = NoteEditor.Text ?? string.Empty;
        var titleText = NoteTitleEditor.Text ?? string.Empty;

        if (!HasUnsavedChanges())
            return;

        var caretIndex = NoteEditor.CaretIndex;
        var selectionLength = NoteEditor.SelectionLength;
        var verticalOffset = _editorScrollViewer?.VerticalOffset ?? 0;
        var horizontalOffset = _editorScrollViewer?.HorizontalOffset ?? 0;
        var shouldRefocusEditor = NoteEditor.IsKeyboardFocusWithin;

        _suppressNextEditorReload = true;
        _vm.EditTitle = titleText;
        _vm.EditContent = editorText;

        if (!_vm.SaveNoteSnapshot(_currentEditorNoteId.Value, titleText, editorText))
            return;

        _savedTitleSnapshot = titleText;
        _savedContentSnapshot = editorText;

        Dispatcher.BeginInvoke(() =>
        {
            var editor = NoteEditor;
            if (editor is null)
                return;

            if (shouldRefocusEditor)
                editor.Focus();

            var editorText = editor.Text ?? string.Empty;
            editor.CaretIndex = Math.Min(caretIndex, editorText.Length);
            editor.SelectionLength = Math.Min(selectionLength, Math.Max(0, editorText.Length - editor.CaretIndex));
            _editorScrollViewer?.ScrollToVerticalOffset(verticalOffset);
            _editorScrollViewer?.ScrollToHorizontalOffset(horizontalOffset);
            QueueEditorVisualRefresh(false);
        }, DispatcherPriority.Background);
    }

    private void RefreshLineNumbers()
    {
        if (NoteEditor is null || LineNumberGutter is null)
            return;

        var lineCount = Math.Max(1, NoteEditor.LineCount);
        _lastRenderedLineCount = lineCount;
        LineNumberGutter.LineCount = lineCount;
        LineNumberGutter.LineHeight = GetEditorLineHeight();
        LineNumberGutter.EditorPaddingTop = NoteEditor.Padding.Top;
        LineNumberGutter.VerticalOffset = _editorScrollViewer?.VerticalOffset ?? 0;
    }

    private double GetEditorLineHeight()
    {
        return Math.Max(CurrentLineMinHeight, NoteEditor.FontSize * 1.45);
    }

    private void UpdateCurrentLineHighlight()
    {
        if (NoteEditor is null || CurrentLineHighlight is null || !NoteEditor.IsLoaded)
            return;

        var lineIndex = NoteEditor.GetLineIndexFromCharacterIndex(NoteEditor.CaretIndex);
        if (lineIndex < 0)
        {
            CurrentLineHighlight.IsHighlightVisible = false;
            return;
        }

        var caretIndex = NoteEditor.CaretIndex;
        var rect = NoteEditor.GetRectFromCharacterIndex(caretIndex, true);
        if (rect.IsEmpty)
            rect = NoteEditor.GetRectFromCharacterIndex(caretIndex, false);

        if (rect.IsEmpty)
        {
            var lineCharIndex = NoteEditor.GetCharacterIndexFromLineIndex(lineIndex);
            rect = NoteEditor.GetRectFromCharacterIndex(lineCharIndex, false);
        }

        if (rect.IsEmpty && caretIndex > 0)
            rect = NoteEditor.GetRectFromCharacterIndex(caretIndex - 1, true);

        if (rect.IsEmpty && lineIndex + 1 < Math.Max(1, NoteEditor.LineCount))
        {
            var nextLineIndex = NoteEditor.GetCharacterIndexFromLineIndex(lineIndex + 1);
            var nextLineRect = NoteEditor.GetRectFromCharacterIndex(nextLineIndex, false);
            if (!nextLineRect.IsEmpty)
            {
                rect = new Rect(nextLineRect.X, nextLineRect.Y - nextLineRect.Height, nextLineRect.Width, nextLineRect.Height);
            }
        }

        if (rect.IsEmpty)
        {
            var fallbackTop = NoteEditor.Padding.Top + lineIndex * (NoteEditor.FontSize * 1.45);
            rect = new Rect(0, fallbackTop, Math.Max(0, NoteEditor.ActualWidth), Math.Max(CurrentLineMinHeight, NoteEditor.FontSize * 1.6));
        }

        CurrentLineHighlight.HighlightTop = Math.Max(0, rect.Top);
        CurrentLineHighlight.HighlightHeight = Math.Max(CurrentLineMinHeight, rect.Height);
        CurrentLineHighlight.IsHighlightVisible = true;
    }

    private static T? FindDescendant<T>(DependencyObject? root) where T : DependencyObject
    {
        if (root == null)
            return null;

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)

                return match;

            var nested = FindDescendant<T>(child);
            if (nested != null)
                return nested;
        }

        return null;
    }

}
