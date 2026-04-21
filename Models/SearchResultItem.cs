namespace KiteTodo.Models;

public enum SearchTargetType
{
    Todo,
    Backlog,
    Note
}

public class SearchResultItem
{
    public SearchTargetType TargetType { get; set; }

    public int Id { get; set; }

    public string Section { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime? ScheduledDate { get; set; }

    public string MetaText { get; set; } = string.Empty;
}