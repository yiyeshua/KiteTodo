using System.IO;
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
    public List<FlowchartStorageItem> Flowcharts { get; set; } = new();
    public AppSettings? Settings { get; set; }
    /// <summary>笔记本 Pro 的文件存储路径（备份时记录，恢复时提示）</summary>
    public string? NotesProDirectory { get; set; }
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
            Flowcharts = _db.Flowcharts.FindAll().ToList(),
            Settings = _db.GetSettings(),
            NotesProDirectory = GetNotesProPath()
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

            _db.Flowcharts.DeleteAll();
            foreach (var item in data.Flowcharts)
                _db.Flowcharts.Insert(item);

            if (data.Settings != null)
            {
                data.Settings.Id = 1;
                _db.Settings.DeleteAll();
                _db.Settings.Insert(data.Settings);
            }

            var msgParts = new List<string>
            {
                $"恢复成功！待办 {data.Todos.Count} 条，笔记 {data.Notes.Count} 条，" +
                $"番茄钟记录 {data.Pomodoros.Count} 条，事项池 {data.Backlogs.Count} 条，流程图 {data.Flowcharts.Count} 条。"
            };
            if (!string.IsNullOrEmpty(data.NotesProDirectory))
                msgParts.Add($"\n备份中包含笔记本 Pro 数据路径：{data.NotesProDirectory}\n如需恢复，请手动复制该目录到：{GetNotesProPath()}");

            msgParts.Add("建议重启应用以刷新所有页面。");
            return (true, string.Join("", msgParts));
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

    /// <summary>备份笔记本 Pro 文件目录（复制整个 notes 目录到目标 ZIP 或文件夹）</summary>
    public (bool Success, string Message) BackupNotesDirectory(string destDir)
    {
        try
        {
            var notesPath = GetNotesProPath();
            if (!Directory.Exists(notesPath))
                return (false, "笔记本 Pro 数据目录不存在，可能尚未使用过");

            var destPath = Path.Combine(destDir, $"KiteTodo_NotesPro_{DateTime.Now:yyyyMMdd_HHmmss}");
            CopyDirectoryRecursive(notesPath, destPath);
            return (true, $"笔记本 Pro 数据已备份到：{destPath}");
        }
        catch (Exception ex)
        {
            return (false, $"备份失败：{ex.Message}");
        }
    }

    /// <summary>恢复笔记本 Pro 文件目录</summary>
    public (bool Success, string Message) RestoreNotesDirectory(string srcDir)
    {
        try
        {
            if (!Directory.Exists(srcDir))
                return (false, "备份目录不存在");

            // 先备份当前数据
            var notesPath = GetNotesProPath();
            if (Directory.Exists(notesPath))
            {
                var backupPath = notesPath + $"_backup_{DateTime.Now:yyyyMMdd_HHmmss}";
                Directory.Move(notesPath, backupPath);
            }

            CopyDirectoryRecursive(srcDir, notesPath);
            return (true, $"笔记本 Pro 数据已从 {srcDir} 恢复");
        }
        catch (Exception ex)
        {
            return (false, $"恢复失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 将旧笔记本数据（LiteDB Note）迁移到笔记本 Pro 目录（.md 文件）。
    /// 每个 Note 生成一个 .md 文件，保留标题和内容。
    /// </summary>
    public (int Success, int Skipped, string Message) MigrateOldNotesToFileSystem()
    {
        try
        {
            var notes = _db.Notes.FindAll().ToList();
            if (notes.Count == 0)
                return (0, 0, "旧笔记本中没有数据，无需迁移");

            var targetDir = GetNotesProPath();
            Directory.CreateDirectory(targetDir);

            int success = 0, skipped = 0;
            foreach (var note in notes)
            {
                // 使用标题作为文件名，清理非法字符
                var safeTitle = SanitizeFileName(note.Title);
                if (string.IsNullOrWhiteSpace(safeTitle))
                    safeTitle = $"笔记_{note.Id}";

                var fileName = $"{safeTitle}.md";
                var filePath = Path.Combine(targetDir, fileName);

                // 如果文件已存在，加序号
                if (File.Exists(filePath))
                {
                    fileName = $"{safeTitle}_{note.Id}.md";
                    filePath = Path.Combine(targetDir, fileName);
                }
                if (File.Exists(filePath))
                {
                    skipped++;
                    continue;
                }

                File.WriteAllText(filePath, note.Content ?? "", System.Text.Encoding.UTF8);
                File.SetCreationTime(filePath, note.CreatedAt);
                File.SetLastWriteTime(filePath, note.UpdatedAt);
                success++;
            }

            var msg = $"迁移完成！\n成功：{success} 条\n跳过：{skipped} 条（文件名冲突）\n\n笔记已保存到：{targetDir}\n\n原 LiteDB 数据未删除，可在旧笔记本中继续查看。";
            return (success, skipped, msg);
        }
        catch (Exception ex)
        {
            return (0, 0, $"迁移失败：{ex.Message}");
        }
    }

    /// <summary>获取笔记本 Pro 的数据目录路径</summary>
    public static string GetNotesProPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KiteTodo", "notes");
    }

    private static void CopyDirectoryRecursive(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var dest = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, dest, true);
        }
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var dest = Path.Combine(destDir, Path.GetFileName(dir));
            CopyDirectoryRecursive(dir, dest);
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new System.Text.StringBuilder();
        foreach (var c in name)
        {
            if (Array.IndexOf(invalid, c) >= 0)
                sanitized.Append('_');
            else
                sanitized.Append(c);
        }
        var result = sanitized.ToString().Trim();
        if (result.Length > 100) result = result[..100];
        return result;
    }
}
