using System.Windows;
using KiteTodo.Models;

namespace KiteTodo.ViewModels;

public class NotebookListItem
{
    public NotebookListItem(Note note, int depth, int childCount, bool isExpanded, bool isLastChild)
    {
        Note = note;
        Depth = depth;
        ChildCount = childCount;
        IsExpanded = isExpanded;
        IsLastChild = isLastChild;
    }

    public Note Note { get; }

    public int Depth { get; }

    public int ChildCount { get; }

    public bool IsChild => Depth > 0;

    public bool HasChildren => ChildCount > 0;

    public bool IsExpanded { get; }

    public bool IsLastChild { get; }

    public Thickness ItemMargin => new(Depth * 18, 0, 0, 0);

    public string DisplayTitle => string.IsNullOrWhiteSpace(Note.Title) ? "未命名笔记" : Note.Title;

    public string ExpandGlyph => IsExpanded ? "▾" : "▸";

    public bool ShowTreeGuide => IsChild;

    public bool ShowTreeGuideContinuation => IsChild && !IsLastChild;
}