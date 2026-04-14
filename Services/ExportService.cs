// ============================================================================
// ExportService.cs - 数据导出服务
// 将待办数据导出为 Markdown 或纯文本格式
// ============================================================================

using System.Text;
using KiteTodo.Models;

namespace KiteTodo.Services;

/// <summary>
/// 数据导出服务，支持按日期范围导出待办数据为可读文本。
/// 用于导出页面和周报导出功能。
/// </summary>
public class ExportService
{
    private readonly TodoService _todoService = new();

    /// <summary>
    /// 导出为 Markdown 格式（适合粘贴到文档/笔记中）
    /// 输出结构：按日期分组，每个待办带 checkbox 标记和优先级
    /// </summary>
    public string ExportToMarkdown(DateTime start, DateTime end)
    {
        var todos = _todoService.GetTodosByDateRange(start, end);
        var sb = new StringBuilder();
        sb.AppendLine($"# KiteTodo Export");
        sb.AppendLine($"> {start:yyyy-MM-dd} ~ {end:yyyy-MM-dd}");
        sb.AppendLine();

        // 按日期分组显示
        var grouped = todos.GroupBy(x => x.ScheduledDate.Date).OrderBy(g => g.Key);
        foreach (var group in grouped)
        {
            sb.AppendLine($"## {group.Key:yyyy-MM-dd (ddd)}");
            sb.AppendLine();
            foreach (var item in group.OrderBy(x => x.SortOrder))
            {
                var check = item.IsCompleted ? "x" : " ";       // Markdown checkbox
                var priority = item.Priority switch
                {
                    3 => " !!!",   // 高优先级标记
                    2 => " !!",    // 中优先级
                    1 => " !",     // 低优先级
                    _ => ""
                };
                sb.AppendLine($"- [{check}] {item.Title}{priority}");
                if (!string.IsNullOrWhiteSpace(item.Description))
                {
                    sb.AppendLine($"  > {item.Description}");
                }
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// 导出为纯文本格式（适合邮件/简单查阅）
    /// 输出结构：按日期分组，每个待办标注 DONE/TODO 状态
    /// </summary>
    public string ExportToText(DateTime start, DateTime end)
    {
        var todos = _todoService.GetTodosByDateRange(start, end);
        var sb = new StringBuilder();
        sb.AppendLine($"KiteTodo Export: {start:yyyy-MM-dd} ~ {end:yyyy-MM-dd}");
        sb.AppendLine(new string('=', 50));
        sb.AppendLine();

        var grouped = todos.GroupBy(x => x.ScheduledDate.Date).OrderBy(g => g.Key);
        foreach (var group in grouped)
        {
            sb.AppendLine($"[{group.Key:yyyy-MM-dd (ddd)}]");
            foreach (var item in group.OrderBy(x => x.SortOrder))
            {
                var status = item.IsCompleted ? "DONE" : "TODO";
                sb.AppendLine($"  [{status}] {item.Title}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
