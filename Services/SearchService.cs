using KiteTodo.Models;

namespace KiteTodo.Services;

public class SearchService
{
    private readonly TodoService _todoService = new();
    private readonly BacklogService _backlogService = new();
    private readonly NoteService _noteService = new();

    public List<SearchResultItem> Search(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return [];

        var results = new List<SearchResultItem>();

        results.AddRange(_todoService.Search(keyword).Select(item => new SearchResultItem
        {
            TargetType = SearchTargetType.Todo,
            Id = item.Id,
            Section = "待办",
            Title = item.Title,
            Description = item.Description,
            ScheduledDate = item.ScheduledDate,
            MetaText = $"{item.ScheduledDate:yyyy-MM-dd} · {(item.IsCompleted ? "已完成" : "未完成")}"
        }));

        results.AddRange(_backlogService.Search(keyword).Select(item => new SearchResultItem
        {
            TargetType = SearchTargetType.Backlog,
            Id = item.Id,
            Section = "事项池",
            Title = item.Title,
            Description = item.Description,
            MetaText = item.StatusText
        }));

        results.AddRange(_noteService.Search(keyword).Select(item => new SearchResultItem
        {
            TargetType = SearchTargetType.Note,
            Id = item.Id,
            Section = "记事本",
            Title = item.Title,
            Description = item.Content,
            MetaText = $"更新于 {item.UpdatedAt:yyyy-MM-dd HH:mm}"
        }));

        return results
            .OrderBy(result => result.Section)
            .ThenByDescending(result => result.ScheduledDate ?? DateTime.MinValue)
            .ThenBy(result => result.Title)
            .ToList();
    }
}