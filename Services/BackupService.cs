using System.Text.Json;
using System.Text.Json.Serialization;
using KiteTodo.Models;

namespace KiteTodo.Services;

/// <summary>
/// 备份数据结构，包含所有集合的数据。
/// </summary>
public class BackupData
{
    public DateTime ExportedAt { get; set; }
    public List<TodoItem> Todos { get; set; } = new();
    public List<Note> Notes { get; set; } = new();
    public List<PomodoroRecord> Pomodoros { get; set; } = new();
    public List<BacklogItem> Backlogs { get; set; } = new();
    public AppSettings? Settings { get; set; }
}

/// <summary>
/// 数据备份与恢复服务。
/// 将全部数据导出为 JSON 文件，或从 JSON 文件恢复。
/// </summary>
public class BackupService
{
    private readonly DatabaseService _db = DatabaseService.Instance;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>导出全部数据为 JSON 字符串</summary>
    public string ExportToJson()
    {
        var data = new BackupData
        {
            ExportedAt = DateTime.Now,
            Todos = _db.Todos.FindAll().ToList(),
            Notes = _db.Notes.FindAll().ToList(),
            Pomodoros = _db.Pomodoros.FindAll().ToList(),
            Backlogs = _db.Backlogs.FindAll().ToList(),
            Settings = _db.GetSettings()
        };
        return JsonSerializer.Serialize(data, _jsonOptions);
    }

    /// <summary>从 JSON 字符串恢复全部数据（覆盖现有数据）</summary>
    public (bool Success, string Message) ImportFromJson(string json)
    {
        try
        {
            var data = JsonSerializer.Deserialize<BackupData>(json, _jsonOptions);
            if (data == null)
                return (false, "备份文件格式无效");

            // 清空并重新写入所有集合
            _db.Todos.DeleteAll();
            foreach (var item in data.Todos)
                _db.Todos.Insert(item);

            _db.Notes.DeleteAll();
            foreach (var item in data.Notes)
                _db.Notes.Insert(item);

            _db.Pomodoros.DeleteAll();
            foreach (var item in data.Pomodoros)
                _db.Pomodoros.Insert(item);

            _db.Backlogs.DeleteAll();
            foreach (var item in data.Backlogs)
                _db.Backlogs.Insert(item);

            if (data.Settings != null)
            {
                data.Settings.Id = 1;
                _db.Settings.DeleteAll();
                _db.Settings.Insert(data.Settings);
            }

            var msg = $"恢复成功！待办 {data.Todos.Count} 条，笔记 {data.Notes.Count} 条，" +
                      $"番茄钟记录 {data.Pomodoros.Count} 条，事项池 {data.Backlogs.Count} 条。建议重启应用以刷新所有页面。";
            return (true, msg);
        }
        catch (JsonException)
        {
            return (false, "备份文件格式无效，无法解析 JSON");
        }
        catch (Exception ex)
        {
            return (false, $"恢复失败：{ex.Message}");
        }
    }
}
