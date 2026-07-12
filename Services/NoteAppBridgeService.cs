// ============================================================================
// NoteAppBridgeService.cs - AINote WebView2 与 .NET 的桥接服务
//
// 替代 Tauri Rust 后端，处理来自 WebView2 中 AINote 前端的 IPC 调用。
// 前端通过 webview-bridge.js 将 Tauri invoke() 转为 postMessage，
// 此服务通过 WebMessageReceived 事件接收并处理，返回结果给前端。
//
// 关键：返回值的格式必须与 AINote 前端 services 层期望的格式完全一致。
// Tauri invoke() 直接返回 Rust 命令的返回值，不做额外包装。
// ============================================================================

using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KiteTodo.Services;

public class NoteAppBridgeService
{
    private readonly string _workDir;
    private readonly string _assetsDir;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string WorkDir => _workDir;

    public NoteAppBridgeService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KiteTodo",
            "notes");
        Directory.CreateDirectory(appDataPath);
        _workDir = appDataPath;
        _assetsDir = Path.Combine(_workDir, "assets");
        Directory.CreateDirectory(_assetsDir);

        // 启动标记：每次 KiteTodo 启动都会写入，方便确认版本
        File.AppendAllText(Path.Combine(_workDir, ".ai-debug.log"),
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Bridge v2.0 started, workDir={_workDir}\n", Encoding.UTF8);
    }

    /// <summary>
    /// 处理来自 WebView2 的消息，返回需要 post 回前端的 JSON 响应。
    /// 返回 null 表示不需要响应（纯通知类事件）。
    /// </summary>
    public async Task<string?> HandleMessageAsync(string rawMessage)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawMessage);
            var root = doc.RootElement;
            var type = root.GetProperty("type").GetString();
            if (type != "invoke") return null;

            var id = root.GetProperty("id").GetInt32();
            var cmd = root.GetProperty("cmd").GetString() ?? "";
            var args = root.TryGetProperty("args", out var argsEl) ? argsEl : default;

            // 执行命令并获取原始返回值
            var result = await ExecuteCommandAsync(cmd, args);

            // 序列化响应：result 直接作为 JSON token 嵌入
            var resultJson = result == null ? "null" : JsonSerializer.Serialize(result, _jsonOptions);
            return $"{{\"type\":\"invoke_result\",\"id\":{id},\"result\":{resultJson}}}";
        }
        catch (Exception ex)
        {
            try
            {
                using var doc = JsonDocument.Parse(rawMessage);
                var id = doc.RootElement.GetProperty("id").GetInt32();
                return $"{{\"type\":\"invoke_result\",\"id\":{id},\"error\":\"{EscapeJson(ex.Message)}\"}}";
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// 处理 Tauri plugin 命令（如 plugin:path|join, plugin:path|dirname 等）
    /// </summary>
    private static object? HandlePluginCommand(string cmd, JsonElement args)
    {
        // 格式：plugin:模块名|方法名
        var parts = cmd.Split('|');
        if (parts.Length < 2) throw new InvalidOperationException($"Invalid plugin command: {cmd}");
        var moduleAndMethod = parts[0]; // "plugin:path"
        var method = parts[1];           // "join"
        var module = moduleAndMethod.Replace("plugin:", ""); // "path"

        return module switch
        {
            "dialog" => method switch
            {
                "save" => ShowSaveDialog(args),
                "open" => ShowOpenDialog(args),
                "message" => ShowMessageDialog(args),
                _ => throw new InvalidOperationException($"Unknown dialog method: {method}")
            },
            "path" => method switch
            {
                "join" => PathJoin(args),
                "dirname" => PathDirname(args),
                "extname" => PathExtname(args),
                "basename" => PathBasename(args),
                "resolve" => PathResolve(args),
                "normalize" => PathNormalize(args),
                "is_absolute" => PathIsAbsolute(args),
                "resolve_directory" => ResolveDirectory(args),
                _ => throw new InvalidOperationException($"Unknown path method: {method}")
            },
            "event" => method switch
            {
                "listen" => null,
                "unlisten" => null,
                "emit" => null,
                "emit_to" => null,
                _ => null
            },
            "clipboard" => method switch
            {
                "writeText" => null,
                _ => null
            },
            "fs" => method switch
            {
                "readFile" => ReadFileContent(args),
                "writeFile" => WriteFileAsync(args),
                _ => null
            },
            _ => throw new InvalidOperationException($"Unknown plugin: {module}")
        };
    }

    private static string PathJoin(JsonElement args)
    {
        var paths = new List<string>();
        if (args.TryGetProperty("paths", out var arr))
        {
            foreach (var p in arr.EnumerateArray())
                paths.Add(p.GetString() ?? "");
        }
        // Simple path joining with normalization
        var joined = string.Join("/", paths).Replace('\\', '/');
        while (joined.Contains("//")) joined = joined.Replace("//", "/");
        return joined;
    }

    private static string PathDirname(JsonElement args)
    {
        var path = args.TryGetProperty("path", out var p) ? (p.GetString() ?? "") : "";
        path = path.Replace('\\', '/');
        var lastSlash = path.LastIndexOf('/');
        return lastSlash >= 0 ? path[..lastSlash] : "";
    }

    private static string PathExtname(JsonElement args)
    {
        var path = args.TryGetProperty("path", out var p) ? (p.GetString() ?? "") : "";
        var lastDot = path.LastIndexOf('.');
        return lastDot >= 0 ? path[lastDot..] : "";
    }

    private static string PathBasename(JsonElement args)
    {
        var path = args.TryGetProperty("path", out var p) ? (p.GetString() ?? "") : "";
        var ext = args.TryGetProperty("ext", out var e) ? e.GetString() ?? "" : "";
        path = path.Replace('\\', '/');
        var lastSlash = path.LastIndexOf('/');
        var baseName = lastSlash >= 0 ? path[(lastSlash + 1)..] : path;
        // If ext is provided, strip it from the basename
        if (!string.IsNullOrEmpty(ext) && baseName.EndsWith(ext))
            baseName = baseName[..^ext.Length];
        return baseName;
    }

    private static string PathResolve(JsonElement args)
    {
        var paths = new List<string>();
        if (args.TryGetProperty("paths", out var arr))
            foreach (var p in arr.EnumerateArray())
                paths.Add(p.GetString() ?? "");
        var joined = string.Join("/", paths).Replace('\\', '/');
        while (joined.Contains("//")) joined = joined.Replace("//", "/");
        return joined;
    }

    private static string PathNormalize(JsonElement args)
    {
        var path = args.TryGetProperty("path", out var p) ? (p.GetString() ?? "") : "";
        path = path.Replace('\\', '/');
        while (path.Contains("//")) path = path.Replace("//", "/");
        // Remove trailing slash
        if (path.Length > 1 && path.EndsWith('/')) path = path[..^1];
        return path;
    }

    private static bool PathIsAbsolute(JsonElement args)
    {
        var path = args.TryGetProperty("path", out var p) ? (p.GetString() ?? "") : "";
        return Path.IsPathRooted(path);
    }

    /// <summary>
    /// Tauri path module 的分辨目录方法（homeDir, appDataDir 等）。
    /// 返回对应系统路径。
    /// </summary>
    private static string ResolveDirectory(JsonElement args)
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    // ---- 文件系统插件（plugin:fs|readFile, plugin:fs|writeFile） ----

    /// <summary>
    /// 读取文件返回字节数组（number[]）。
    /// 用于预览中的图片「另存为」功能。
    /// </summary>
    private static byte[] ReadFileContent(JsonElement args)
    {
        var path = args.TryGetProperty("path", out var p) ? p.GetString() ?? "" : "";
        // 路径可能是 URL 编码后的 asset:// 路径
        path = Uri.UnescapeDataString(path);
        if (File.Exists(path)) return File.ReadAllBytes(path);
        throw new FileNotFoundException($"文件不存在: {path}");
    }

    private static object? WriteFileAsync(JsonElement args)
    {
        var path = args.TryGetProperty("path", out var p) ? p.GetString() ?? "" : "";
        var contents = new List<byte>();
        if (args.TryGetProperty("contents", out var c) && c.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in c.EnumerateArray())
                contents.Add(item.GetByte());
        }
        if (contents.Count > 0)
            File.WriteAllBytes(path, contents.ToArray());
        return null;
    }

    // ---- 对话框（使用 .NET 原生对话框替代 Tauri dialog 插件） ----

    private static string? ShowSaveDialog(JsonElement args)
    {
        // options: { defaultPath, filters: [{ name, extensions }], title? }
        var options = args.TryGetProperty("options", out var opts) ? opts : args;

        var dialog = new Microsoft.Win32.SaveFileDialog();
        if (options.TryGetProperty("defaultPath", out var dp))
            dialog.FileName = Path.GetFileName(dp.GetString() ?? "note.txt");
        if (options.TryGetProperty("filters", out var filters))
        {
            var filterStr = new System.Text.StringBuilder();
            foreach (var f in filters.EnumerateArray())
            {
                var name = f.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                var exts = f.TryGetProperty("extensions", out var e) ? string.Join(";", e.EnumerateArray().Select(x => "*." + x.GetString())) : "*.*";
                if (filterStr.Length > 0) filterStr.Append('|');
                filterStr.Append($"{name}|{exts}");
            }
            if (filterStr.Length > 0) dialog.Filter = filterStr.ToString();
        }
        if (options.TryGetProperty("title", out var title))
            dialog.Title = title.GetString();

        var result = dialog.ShowDialog();
        return result == true ? dialog.FileName : null;
    }

    private static string? ShowOpenDialog(JsonElement args)
    {
        var options = args.TryGetProperty("options", out var opts) ? opts : args;

        var dialog = new Microsoft.Win32.OpenFileDialog();
        if (options.TryGetProperty("filters", out var filters))
        {
            var filterStr = new System.Text.StringBuilder();
            foreach (var f in filters.EnumerateArray())
            {
                var name = f.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                var exts = f.TryGetProperty("extensions", out var e) ? string.Join(";", e.EnumerateArray().Select(x => "*." + x.GetString())) : "*.*";
                if (filterStr.Length > 0) filterStr.Append('|');
                filterStr.Append($"{name}|{exts}");
            }
            if (filterStr.Length > 0) dialog.Filter = filterStr.ToString();
        }
        if (options.TryGetProperty("multiple", out var mult) && mult.GetBoolean())
            dialog.Multiselect = true;
        if (options.TryGetProperty("title", out var t))
            dialog.Title = t.GetString();

        var result = dialog.ShowDialog();
        return result == true ? dialog.FileName : null;
    }

    private static string? ShowMessageDialog(JsonElement args)
    {
        var options = args.TryGetProperty("options", out var opts) ? opts : args;
        var message = options.TryGetProperty("message", out var msg) ? msg.GetString() ?? "" : "";
        var title = options.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "提示";

        System.Windows.MessageBox.Show(message, title, System.Windows.MessageBoxButton.OK);
        return null;
    }

    /// <summary>
    /// 处理普通 invoke 命令
    /// </summary>
    private async Task<object?> ExecuteCommandAsync(string cmd, JsonElement args)
    {
        // 处理 Tauri plugin 命令（格式：plugin:模块名|方法名）
        if (cmd.StartsWith("plugin:"))
        {
            return HandlePluginCommand(cmd, args);
        }

        switch (cmd)
        {
            // ---- 文件读写 ----
            case "read_note": return await ReadNoteAsync(args);
            case "write_note": WriteNote(args); return null;
            case "create_note": return CreateNote(args);
            case "delete_note": DeleteNote(args); return null;
            case "rename_note": return RenameNote(args);
            case "move_note": return MoveNote(args);
            case "export_note": return await ExportNoteAsync(args);

            // ---- 目录树 ----
            case "scan_tree": return await ScanTreeAsync(args);
            case "create_folder": return CreateFolder(args);
            case "delete_folder": DeleteFolder(args); return null;
            case "rename_folder": return RenameFolder(args);
            case "reorder_node": return null; // no-op
            case "toggle_pin": return null; // no-op

            // ---- 最近访问 ----
            case "get_recent_notes": return await GetRecentNotesAsync();
            case "add_recent_note": AddRecentNote(args); return null;

            // ---- 元信息 ----
            case "get_all_note_metas": return await GetAllNoteMetasAsync();

            // ---- 图片 ----
            case "save_image": return await SaveImageAsync(args);
            case "delete_image": return null; // no-op

            // ---- 搜索 ----
            case "search_notes": return await SearchNotesAsync(args);
            case "search_files_direct": return await SearchFilesDirectAsync(args);
            case "rebuild_search_index": return 0; // no-op, return 0
            case "health_check": return new Dictionary<string, object>
            {
                ["orphanImages"] = new List<string>(),
                ["emptyFolders"] = new List<string>(),
                ["orphanTags"] = new List<string>()
            };

            // ---- 配置 （注意：前端用的是 load_config / save_config，不是 get_config / set_config） ----
            case "load_config": return GetConfig();
            case "save_config": SaveConfig(args); return null;
            case "set_config_value": return null; // no-op
            case "reset_workspace": return null; // no-op

            // ---- 标签（JSON 文件持久化） ----
            case "get_all_tags": return await GetAllTagsAsync();
            case "create_tag": return await CreateTagAsync(args);
            case "rename_tag": await RenameTagAsync(args); return null;
            case "delete_tag": await DeleteTagAsync(args); return null;
            case "get_note_tags": return await GetNoteTagsAsync(args);
            case "set_note_tags": await SetNoteTagsAsync(args); return null;
            case "get_notes_by_tag": return await GetNotesByTagAsync(args);

            // ---- 版本（mock） ----
            case "save_version": return null;
            case "list_versions": return new List<object>();
            case "restore_version": return "";

            // ---- AI ----
            case "ai_request": return await AiRequestAsync(args);

            // ---- 文件监听 ----
            case "start_watcher": return null;
            case "check_file_changes": return new List<object>(); // 返回空列表表示无变更

            default:
                throw new InvalidOperationException($"Unknown command: {cmd}");
        }
    }

    // ========================================================================
    // 文件读写
    // ========================================================================

    /// <summary>读取笔记内容，直接返回纯文本字符串</summary>
    private async Task<string> ReadNoteAsync(JsonElement args)
    {
        var path = GetArg(args, "path");
        var fullPath = ResolvePath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"文件不存在: {path}");
        return await File.ReadAllTextAsync(fullPath, Encoding.UTF8);
    }

    private void WriteNote(JsonElement args)
    {
        var path = GetArg(args, "path");
        var content = GetArg(args, "content");
        var fullPath = ResolvePath(path);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(fullPath, content, Encoding.UTF8);
    }

    /// <summary>创建笔记，返回相对于工作目录的路径字符串</summary>
    private string CreateNote(JsonElement args)
    {
        var name = GetArg(args, "name") ?? "未命名笔记";
        var mode = GetArg(args, "mode") ?? "md";
        var parentPath = GetArg(args, "parentPath") ?? "";

        var ext = mode == "txt" ? ".txt" : ".md";
        var fileName = name.EndsWith(ext) ? name : name + ext;

        var targetDir = !string.IsNullOrEmpty(parentPath)
            ? ResolvePath(parentPath)
            : _workDir;
        Directory.CreateDirectory(targetDir);

        var fullPath = Path.Combine(targetDir, fileName);
        if (File.Exists(fullPath))
        {
            var baseName = Path.GetFileNameWithoutExtension(fileName);
            var counter = 1;
            do { fileName = $"{baseName}_{counter}{ext}"; fullPath = Path.Combine(targetDir, fileName); counter++; }
            while (File.Exists(fullPath));
        }
        File.WriteAllText(fullPath, "", Encoding.UTF8);
        return Path.GetRelativePath(_workDir, fullPath).Replace('\\', '/');
    }

    private void DeleteNote(JsonElement args)
    {
        var path = GetArg(args, "path");
        var fullPath = ResolvePath(path);
        if (Directory.Exists(fullPath)) Directory.Delete(fullPath, true);
        else if (File.Exists(fullPath)) File.Delete(fullPath);
    }

    private string RenameNote(JsonElement args)
    {
        var path = GetArg(args, "path");
        var newName = GetArg(args, "newName");
        var fullPath = ResolvePath(path);

        // 如果 newName 没有扩展名，保留原来的扩展名
        if (!Path.HasExtension(newName))
        {
            var oldExt = Path.GetExtension(fullPath);
            if (!string.IsNullOrEmpty(oldExt))
                newName = newName + oldExt;
        }

        var dir = Path.GetDirectoryName(fullPath)!;
        var newPath = Path.Combine(dir, newName);

        if (Directory.Exists(fullPath)) Directory.Move(fullPath, newPath);
        else if (File.Exists(fullPath)) File.Move(fullPath, newPath);
        else throw new FileNotFoundException($"文件不存在: {path}");

        return Path.GetRelativePath(_workDir, newPath).Replace('\\', '/');
    }

    private string MoveNote(JsonElement args)
    {
        var srcPath = GetArg(args, "srcPath");
        var destDir = GetArg(args, "destDir");
        var fullSrc = ResolvePath(srcPath);
        var fullDestDir = ResolvePath(destDir);
        var name = Path.GetFileName(fullSrc);
        var fullDest = Path.Combine(fullDestDir, name);
        Directory.CreateDirectory(fullDestDir);

        if (Directory.Exists(fullSrc)) Directory.Move(fullSrc, fullDest);
        else if (File.Exists(fullSrc)) File.Move(fullSrc, fullDest);

        return Path.GetRelativePath(_workDir, fullDest).Replace('\\', '/');
    }

    private async Task<string> ExportNoteAsync(JsonElement args)
    {
        var path = GetArg(args, "path");
        var dest = GetArg(args, "dest");
        var fullPath = ResolvePath(path);
        var content = await File.ReadAllTextAsync(fullPath, Encoding.UTF8);
        await File.WriteAllTextAsync(dest, content, Encoding.UTF8);
        return dest;
    }

    // ========================================================================
    // 目录扫描
    // ========================================================================

    private async Task<List<object>> ScanDirectoryAsync(JsonElement args)
    {
        var subPath = GetArg(args, "path") ?? "";
        var dirPath = string.IsNullOrEmpty(subPath) ? _workDir : ResolvePath(subPath);
        if (!Directory.Exists(dirPath)) return new List<object>();

        var entries = new List<object>();
        foreach (var dir in Directory.GetDirectories(dirPath))
        {
            var name = Path.GetFileName(dir);
            if (name.StartsWith(".") || name == "assets") continue;
            entries.Add(new Dictionary<string, object>
            {
                ["name"] = name,
                ["path"] = Path.GetRelativePath(_workDir, dir).Replace('\\', '/'),
                ["isDir"] = true,
                ["children"] = new List<object>(),
                ["isExpanded"] = false
            });
        }
        foreach (var file in Directory.GetFiles(dirPath))
        {
            var name = Path.GetFileName(file);
            if (name.StartsWith(".")) continue;
            var ext = Path.GetExtension(file).ToLower();
            if (ext != ".md" && ext != ".txt") continue;

            var relPath = Path.GetRelativePath(_workDir, file).Replace('\\', '/');
            var info = new FileInfo(file);
            entries.Add(new Dictionary<string, object>
            {
                ["name"] = name,
                ["path"] = relPath,
                ["isDir"] = false,
                ["mode"] = ext == ".txt" ? "txt" : "md",
                ["isPinned"] = false,
                ["sortOrder"] = 0,
                ["children"] = Array.Empty<object>(),
                ["fileSize"] = info.Length,
                ["createdAt"] = info.CreationTime.ToString("o"),
                ["updatedAt"] = info.LastWriteTime.ToString("o")
            });
        }
        await Task.CompletedTask;
        return entries;
    }

    /// <summary>扫描完整目录树，返回根节点（AINote 期望 TreeNode 直接作为返回值）</summary>
    private async Task<Dictionary<string, object>> ScanTreeAsync(JsonElement args)
    {
        var workDir = GetArg(args, "workDir");
        var rootDir = !string.IsNullOrEmpty(workDir) ? Path.GetFullPath(workDir) : _workDir;
        if (!Directory.Exists(rootDir)) Directory.CreateDirectory(rootDir);

        var tree = BuildTreeNode(rootDir, rootDir);
        await Task.CompletedTask;
        return tree;
    }

    private Dictionary<string, object> BuildTreeNode(string fullPath, string rootPath)
    {
        var name = fullPath == rootPath ? "root" : Path.GetFileName(fullPath);
        var relativePath = fullPath == rootPath
            ? ""
            : Path.GetRelativePath(rootPath, fullPath).Replace('\\', '/');

        var children = new List<object>();

        foreach (var dir in Directory.GetDirectories(fullPath))
        {
            var dirName = Path.GetFileName(dir);
            if (dirName.StartsWith(".") || dirName == "assets") continue;
            children.Add(BuildTreeNode(dir, rootPath));
        }
        foreach (var file in Directory.GetFiles(fullPath))
        {
            var fileName = Path.GetFileName(file);
            if (fileName.StartsWith(".")) continue;
            var ext = Path.GetExtension(file).ToLower();
            if (ext != ".md" && ext != ".txt") continue;

            var relPath = Path.GetRelativePath(rootPath, file).Replace('\\', '/');
            var info = new FileInfo(file);
            children.Add(new Dictionary<string, object>
            {
                ["name"] = fileName,
                ["path"] = relPath,
                ["isDir"] = false,
                ["mode"] = ext == ".txt" ? "txt" : "md",
                ["isPinned"] = false,
                ["sortOrder"] = 0,
                ["children"] = Array.Empty<object>(),
                ["isExpanded"] = false,
                ["fileSize"] = info.Length,
                ["createdAt"] = info.CreationTime.ToString("o"),
                ["updatedAt"] = info.LastWriteTime.ToString("o")
            });
        }

        return new Dictionary<string, object>
        {
            ["name"] = name,
            ["path"] = relativePath,
            ["isDir"] = true,
            ["children"] = children,
            ["isExpanded"] = true
        };
    }

    // ========================================================================
    // 目录树 —— 文件夹操作
    // ========================================================================

    private string CreateFolder(JsonElement args)
    {
        var name = GetArg(args, "name") ?? "新文件夹";
        var parentPath = GetArg(args, "parentPath") ?? "";
        var workDir = GetArg(args, "workDir");
        var baseDir = !string.IsNullOrEmpty(workDir) ? Path.GetFullPath(workDir) : _workDir;
        var targetDir = !string.IsNullOrEmpty(parentPath)
            ? Path.GetFullPath(Path.Combine(baseDir, parentPath))
            : baseDir;

        var fullPath = Path.Combine(targetDir, name);
        if (Directory.Exists(fullPath))
        {
            var counter = 1;
            do { fullPath = Path.Combine(targetDir, $"{name}_{counter}"); counter++; }
            while (Directory.Exists(fullPath));
        }
        Directory.CreateDirectory(fullPath);
        return Path.GetRelativePath(baseDir, fullPath).Replace('\\', '/');
    }

    private void DeleteFolder(JsonElement args)
    {
        var path = GetArg(args, "path");
        var workDir = GetArg(args, "workDir");
        var baseDir = !string.IsNullOrEmpty(workDir) ? Path.GetFullPath(workDir) : _workDir;
        var fullPath = Path.GetFullPath(Path.Combine(baseDir, path));
        if (Directory.Exists(fullPath)) Directory.Delete(fullPath, true);
    }

    private string RenameFolder(JsonElement args)
    {
        var path = GetArg(args, "path");
        var newName = GetArg(args, "newName");
        var workDir = GetArg(args, "workDir");
        var baseDir = !string.IsNullOrEmpty(workDir) ? Path.GetFullPath(workDir) : _workDir;
        var fullPath = Path.GetFullPath(Path.Combine(baseDir, path));
        var parentDir = Path.GetDirectoryName(fullPath)!;
        var newPath = Path.Combine(parentDir, newName);
        if (Directory.Exists(fullPath)) Directory.Move(fullPath, newPath);
        return Path.GetRelativePath(baseDir, newPath).Replace('\\', '/');
    }

    // ========================================================================
    // 最近访问
    // ========================================================================

    private async Task<List<object>> GetRecentNotesAsync()
    {
        // AINote expects an array of NoteMeta-like objects
        var recentPath = Path.Combine(_workDir, ".recent.json");
        if (!File.Exists(recentPath))
            return new List<object>();

        var json = await File.ReadAllTextAsync(recentPath, Encoding.UTF8);
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("paths", out var arr))
            {
                var result = new List<object>();
                foreach (var p in arr.EnumerateArray())
                {
                    var path = p.GetString() ?? "";
                    var fullPath = ResolvePath(path);
                    if (File.Exists(fullPath))
                    {
                        var info = new FileInfo(fullPath);
                        result.Add(new Dictionary<string, object>
                        {
                            ["filePath"] = path,
                            ["fileName"] = Path.GetFileName(path),
                            ["title"] = Path.GetFileNameWithoutExtension(path),
                            ["mode"] = path.EndsWith(".txt") ? "txt" : "md",
                            ["updatedAt"] = info.LastWriteTime.ToString("o")
                        });
                    }
                }
                return result;
            }
        }
        catch { }
        return new List<object>();
    }

    private void AddRecentNote(JsonElement args)
    {
        var path = GetArg(args, "path");
        var recentPath = Path.Combine(_workDir, ".recent.json");

        var paths = new List<string>();
        if (File.Exists(recentPath))
        {
            try
            {
                var existing = File.ReadAllText(recentPath, Encoding.UTF8);
                using var doc = JsonDocument.Parse(existing);
                if (doc.RootElement.TryGetProperty("paths", out var arr))
                    foreach (var item in arr.EnumerateArray())
                        paths.Add(item.GetString() ?? "");
            }
            catch { }
        }
        paths.RemoveAll(p => p == path);
        paths.Insert(0, path);
        if (paths.Count > 20) paths = paths.Take(20).ToList();

        File.WriteAllText(recentPath,
            JsonSerializer.Serialize(new { paths }, _jsonOptions), Encoding.UTF8);
    }

    // ========================================================================
    // 元信息
    // ========================================================================

    private async Task<List<object>> GetAllNoteMetasAsync()
    {
        var result = new List<object>();
        foreach (var file in Directory.GetFiles(_workDir, "*.*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(file).ToLower();
            if (ext != ".md" && ext != ".txt") continue;
            var relPath = Path.GetRelativePath(_workDir, file).Replace('\\', '/');
            var info = new FileInfo(file);
            // 使用稳定 ID（基于文件路径哈希），与 GetFilePathFromNoteId 保持一致
            var stableId = Math.Abs(relPath.GetHashCode()) % 1000000 + 1;
            result.Add(new Dictionary<string, object>
            {
                ["id"] = stableId,
                ["filePath"] = relPath,
                ["fileName"] = Path.GetFileName(file),
                ["title"] = Path.GetFileNameWithoutExtension(file),
                ["mode"] = ext == ".txt" ? "txt" : "md",
                ["isPinned"] = false,
                ["sortOrder"] = 0,
                ["parentPath"] = (string?)null,
                ["fileSize"] = info.Length,
                ["createdAt"] = info.CreationTime.ToString("o"),
                ["updatedAt"] = info.LastWriteTime.ToString("o"),
                ["accessedAt"] = (string?)null,
                ["contentHash"] = (string?)null
            });
        }
        await Task.CompletedTask;
        return result;
    }

    // ========================================================================
    // 图片
    // ========================================================================

    /// <summary>
    /// 保存图片。imgData 是 number[]（字节数组），noteDir 是笔记所在目录。
    /// 图片保存到笔记同级 assets/ 目录，返回相对于工作目录的路径。
    /// </summary>
    private async Task<string> SaveImageAsync(JsonElement args)
    {
        // AINote sends: { imgData: number[], noteDir: string, compress: bool, quality: number }
        byte[] bytes;
        if (args.TryGetProperty("imgData", out var imgDataEl) && imgDataEl.ValueKind == JsonValueKind.Array)
        {
            bytes = new byte[imgDataEl.GetArrayLength()];
            var i = 0;
            foreach (var item in imgDataEl.EnumerateArray())
                bytes[i++] = item.GetByte();
        }
        else
        {
            // fallback: try base64
            var data = GetArg(args, "imgData");
            if (string.IsNullOrEmpty(data)) throw new InvalidOperationException("No image data provided");
            var base64 = data.Contains(',') ? data[(data.IndexOf(',') + 1)..] : data;
            bytes = Convert.FromBase64String(base64);
        }

        // Determine save location relative to noteDir
        var noteDir = GetArg(args, "noteDir");
        var assetsDir = !string.IsNullOrEmpty(noteDir)
            ? Path.Combine(ResolvePath(noteDir), "assets")
            : _assetsDir;
        Directory.CreateDirectory(assetsDir);

        var fileName = $"img_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid().ToString()[..6]}.png";
        var filePath = Path.Combine(assetsDir, fileName);
        await File.WriteAllBytesAsync(filePath, bytes);

        return Path.GetRelativePath(_workDir, filePath).Replace('\\', '/');
    }

    // ========================================================================
    // 搜索
    // ========================================================================

    private async Task<List<object>> SearchNotesAsync(JsonElement args)
    {
        var query = GetArg(args, "query") ?? "";
        var results = new List<object>();
        if (string.IsNullOrWhiteSpace(query)) return results;

        foreach (var file in Directory.GetFiles(_workDir, "*.*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(file).ToLower();
            if (ext != ".md" && ext != ".txt") continue;
            try
            {
                var content = await File.ReadAllTextAsync(file, Encoding.UTF8);
                var title = Path.GetFileName(file);
                if (title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    content.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    var relPath = Path.GetRelativePath(_workDir, file).Replace('\\', '/');
                    results.Add(new Dictionary<string, object>
                    {
                        ["notePath"] = relPath,
                        ["filePath"] = relPath,
                        ["title"] = title,
                        ["fileName"] = title,
                        ["snippet"] = GetSnippet(content, query, 80),
                        ["parentPath"] = Path.GetDirectoryName(relPath)?.Replace('\\', '/') ?? "",
                        ["matchCount"] = CountMatches(content, query),
                        ["matchType"] = title.Contains(query, StringComparison.OrdinalIgnoreCase) ? "title" : "content"
                    });
                }
            }
            catch { }
        }
        return results;
    }

    /// <summary>
    /// 直接文件系统搜索（SearchPanel 在数据库搜索无结果时的兜底方案）。
    /// 返回与 search_notes 相同格式的结果。
    /// </summary>
    private async Task<List<object>> SearchFilesDirectAsync(JsonElement args)
    {
        var query = GetArg(args, "query") ?? "";
        var workDir = GetArg(args, "workDir");
        var dir = !string.IsNullOrEmpty(workDir) ? Path.GetFullPath(workDir) : _workDir;
        var results = new List<object>();

        if (string.IsNullOrWhiteSpace(query) || !Directory.Exists(dir))
            return results;

        foreach (var file in Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(file).ToLower();
            if (ext != ".md" && ext != ".txt") continue;
            try
            {
                var content = await File.ReadAllTextAsync(file, Encoding.UTF8);
                var fileName = Path.GetFileName(file);
                var titleMatch = fileName.Contains(query, StringComparison.OrdinalIgnoreCase);
                var contentMatch = content.Contains(query, StringComparison.OrdinalIgnoreCase);

                if (titleMatch || contentMatch)
                {
                    var relPath = Path.GetRelativePath(dir, file).Replace('\\', '/');
                    var parentPath = Path.GetDirectoryName(relPath)?.Replace('\\', '/') ?? "";
                    results.Add(new Dictionary<string, object>
                    {
                        ["filePath"] = relPath,
                        ["notePath"] = relPath,
                        ["title"] = fileName,
                        ["fileName"] = fileName,
                        ["snippet"] = GetSnippet(content, query, 80),
                        ["parentPath"] = parentPath,
                        ["matchCount"] = CountMatches(content, query)
                    });
                }
            }
            catch { }
        }
        return results;
    }

    // ========================================================================
    // 配置
    // ========================================================================

    private Dictionary<string, object> GetConfig()
    {
        var configPath = Path.Combine(_workDir, ".config.json");
        if (File.Exists(configPath))
        {
            try
            {
                var json = File.ReadAllText(configPath, Encoding.UTF8);
                var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json, _jsonOptions);
                if (dict != null)
                {
                    // 确保必要字段存在
                    if (!dict.ContainsKey("storageDir")) dict["storageDir"] = _workDir;
                    if (!dict.ContainsKey("theme")) dict["theme"] = "system";
                    if (!dict.ContainsKey("fontFamily")) dict["fontFamily"] = "JetBrains Mono, Consolas, monospace";
                    if (!dict.ContainsKey("fontSize")) dict["fontSize"] = 14;
                    if (!dict.ContainsKey("codeFontFamily")) dict["codeFontFamily"] = "JetBrains Mono, Consolas, monospace";
                    return dict;
                }
            }
            catch { }
        }

        // 默认配置
        return new Dictionary<string, object>
        {
            ["storageDir"] = _workDir,
            ["theme"] = "system",
            ["fontFamily"] = "JetBrains Mono, Consolas, monospace",
            ["fontSize"] = 14,
            ["codeFontFamily"] = "JetBrains Mono, Consolas, monospace"
        };
    }

    private void SaveConfig(JsonElement args)
    {
        var configPath = Path.Combine(_workDir, ".config.json");
        // AINote sends { config: AppConfig }, extract the inner config object
        if (args.TryGetProperty("config", out var configEl))
            File.WriteAllText(configPath, configEl.GetRawText(), Encoding.UTF8);
        else
            File.WriteAllText(configPath, args.GetRawText(), Encoding.UTF8);
    }

    // ========================================================================
    // 标签系统（基于 JSON 文件）
    // ========================================================================

    private string TagsFilePath => Path.Combine(_workDir, ".kite-tags.json");

    private class TagEntry
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? Color { get; set; }
        public string CreatedAt { get; set; } = DateTime.Now.ToString("o");
    }

    private class TagStore
    {
        public List<TagEntry> Tags { get; set; } = new();
        /// <summary>notePath (relative) → tagIds</summary>
        public Dictionary<string, List<int>> NoteTags { get; set; } = new();
    }

    private TagStore LoadTagStore()
    {
        if (File.Exists(TagsFilePath))
        {
            try
            {
                var json = File.ReadAllText(TagsFilePath, Encoding.UTF8);
                return JsonSerializer.Deserialize<TagStore>(json, _jsonOptions) ?? new TagStore();
            }
            catch { }
        }
        return new TagStore();
    }

    private void SaveTagStore(TagStore store)
    {
        File.WriteAllText(TagsFilePath, JsonSerializer.Serialize(store, _jsonOptions), Encoding.UTF8);
    }

    private Task<List<object>> GetAllTagsAsync()
    {
        var store = LoadTagStore();
        var result = store.Tags.Select(t => (object)new Dictionary<string, object>
        {
            ["id"] = t.Id,
            ["name"] = t.Name,
            ["color"] = t.Color,
            ["createdAt"] = t.CreatedAt,
            ["noteCount"] = store.NoteTags.Count(kv => kv.Value.Contains(t.Id))
        }).ToList();
        return Task.FromResult(result);
    }

    private Task<object> CreateTagAsync(JsonElement args)
    {
        var name = GetArg(args, "name");
        var color = GetArg(args, "color");
        var store = LoadTagStore();

        var maxId = store.Tags.Count > 0 ? store.Tags.Max(t => t.Id) : 0;
        var tag = new TagEntry
        {
            Id = maxId + 1,
            Name = name,
            Color = string.IsNullOrEmpty(color) ? null : color,
            CreatedAt = DateTime.Now.ToString("o")
        };
        store.Tags.Add(tag);
        SaveTagStore(store);

        return Task.FromResult<object>(new Dictionary<string, object>
        {
            ["id"] = tag.Id,
            ["name"] = tag.Name,
            ["color"] = tag.Color,
            ["createdAt"] = tag.CreatedAt,
            ["noteCount"] = 0
        });
    }

    private Task RenameTagAsync(JsonElement args)
    {
        var tagId = int.Parse(GetArg(args, "tagId"));
        var newName = GetArg(args, "newName");
        var store = LoadTagStore();
        var tag = store.Tags.FirstOrDefault(t => t.Id == tagId);
        if (tag != null) { tag.Name = newName; SaveTagStore(store); }
        return Task.CompletedTask;
    }

    private Task DeleteTagAsync(JsonElement args)
    {
        var tagId = int.Parse(GetArg(args, "tagId"));
        var store = LoadTagStore();
        store.Tags.RemoveAll(t => t.Id == tagId);
        // 移除所有笔记中对该标签的引用
        foreach (var key in store.NoteTags.Keys.ToList())
            store.NoteTags[key] = store.NoteTags[key].Where(id => id != tagId).ToList();
        SaveTagStore(store);
        return Task.CompletedTask;
    }

    private Task<List<object>> GetNoteTagsAsync(JsonElement args)
    {
        var noteId = int.Parse(GetArg(args, "noteId"));
        // noteId is the id from get_all_note_metas — we need to map back to filePath
        var store = LoadTagStore();
        // Find the filePath from noteId mapping
        var filePath = GetFilePathFromNoteId(noteId);
        if (filePath == null || !store.NoteTags.TryGetValue(filePath, out var tagIds))
            return Task.FromResult(new List<object>());

        var result = store.Tags
            .Where(t => tagIds.Contains(t.Id))
            .Select(t => (object)new Dictionary<string, object>
            {
                ["id"] = t.Id,
                ["name"] = t.Name,
                ["color"] = t.Color,
                ["createdAt"] = t.CreatedAt,
                ["noteCount"] = store.NoteTags.Count(kv => kv.Value.Contains(t.Id))
            }).ToList();
        return Task.FromResult(result);
    }

    private Task SetNoteTagsAsync(JsonElement args)
    {
        var noteId = int.Parse(GetArg(args, "noteId"));
        var tagIds = new List<int>();
        if (args.TryGetProperty("tagIds", out var arr))
        {
            foreach (var item in arr.EnumerateArray())
                tagIds.Add(item.GetInt32());
        }

        var filePath = GetFilePathFromNoteId(noteId);
        if (filePath != null)
        {
            var store = LoadTagStore();
            store.NoteTags[filePath] = tagIds;
            SaveTagStore(store);
        }
        return Task.CompletedTask;
    }

    private Task<List<object>> GetNotesByTagAsync(JsonElement args)
    {
        var tagId = int.Parse(GetArg(args, "tagId"));
        var store = LoadTagStore();
        var matchingPaths = store.NoteTags
            .Where(kv => kv.Value.Contains(tagId))
            .Select(kv => kv.Key)
            .ToList();

        // Build NoteMeta-like objects
        var result = matchingPaths.Select(path =>
        {
            var fullPath = ResolvePath(path);
            var info = File.Exists(fullPath) ? new FileInfo(fullPath) : null;
            return (object)new Dictionary<string, object>
            {
                ["filePath"] = path,
                ["fileName"] = Path.GetFileName(path),
                ["title"] = Path.GetFileNameWithoutExtension(path),
                ["mode"] = path.EndsWith(".txt") ? "txt" : "md",
                ["updatedAt"] = info?.LastWriteTime.ToString("o") ?? ""
            };
        }).ToList();

        return Task.FromResult(result);
    }

    /// <summary>
    /// 将 noteId（来自 get_all_note_metas 分配的临时 ID）映射回 filePath。
    /// 使用文件的相对路径的稳定哈希作为 ID 基础。
    /// </summary>
    private string? GetFilePathFromNoteId(int noteId)
    {
        // 扫描工作目录中的所有文件，计算它们的稳定 ID
        foreach (var file in Directory.GetFiles(_workDir, "*.*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(file).ToLower();
            if (ext != ".md" && ext != ".txt") continue;
            var relPath = Path.GetRelativePath(_workDir, file).Replace('\\', '/');
            var stableId = Math.Abs(relPath.GetHashCode()) % 1000000 + 1;
            if (stableId == noteId) return relPath;
        }
        return null;
    }

    // ========================================================================
    // AI 请求处理
    // ========================================================================

    private static readonly HttpClient _aiHttpClient = new() { Timeout = TimeSpan.FromSeconds(60) };

    private async Task<object> AiRequestAsync(JsonElement args)
    {
        var request = args.TryGetProperty("request", out var req) ? req : args;
        var action = request.TryGetProperty("action", out var a) ? a.GetString() ?? "chat" : "chat";
        var text = request.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
        var context = request.TryGetProperty("context", out var ctx) ? ctx.GetString() : null;

        // 读取配置
        var config = GetConfig();
        var configPath = Path.Combine(_workDir, ".config.json");
        var configExists = File.Exists(configPath);
        var configPreview = configExists ? Truncate(File.ReadAllText(configPath, Encoding.UTF8), 300) : "(文件不存在)";

        // 检查是否启用
        var isEnabled = config.TryGetValue("aiEnabled", out var enabled)
            && enabled is JsonElement je && je.ValueKind == JsonValueKind.True;

        if (!isEnabled)
        {
            return AiErrorResponse($"AI 功能未启用。\n配置文件：{configPath}\n内容：{configPreview}");
        }

        var provider = GetConfigString(config, "aiProvider", "openai");

        // 写诊断日志确认代码执行
        var logPath = Path.Combine(_workDir, ".ai-debug.log");
        File.AppendAllText(logPath,
            $"[{DateTime.Now:HH:mm:ss}] req: action={action} provider={provider} " +
            $"url={GetConfigString(config, "aiApiUrl")} model={GetConfigString(config, "aiModel", "?")} " +
            $"textLen={(text ?? "").Length}\n", Encoding.UTF8);

        try
        {
            var response = provider switch
            {
                "ollama" => await CallOllamaAsync(config, action, text, context),
                _ => await CallOpenAiCompatibleAsync(config, action, text, context)
            };

            return new Dictionary<string, object>
            {
                ["content"] = response,
                ["debug"] = $"[{DateTime.Now:HH:mm:ss}] ok: provider={provider}, action={action}, url={GetConfigString(config, "aiApiUrl")}, model={GetConfigString(config, "aiModel", "?")}"
            };
        }
        catch (Exception ex)
        {
            var innerMsg = ex.InnerException?.Message ?? "";
            var msg = $"❌ AI 请求失败\n时间：{DateTime.Now:HH:mm:ss}\n错误：{ex.Message}";
            if (!string.IsNullOrEmpty(innerMsg)) msg += $"\n详情：{innerMsg}";
            File.AppendAllText(logPath,
                $"[{DateTime.Now:HH:mm:ss}] FAIL: {ex.Message}\n", Encoding.UTF8);
            return AiErrorResponse(msg);
        }
    }

    private static Dictionary<string, object> AiErrorResponse(string msg)
    {
        return new Dictionary<string, object>
        {
            ["content"] = msg,
            ["error"] = msg,
            ["debug"] = $"[{DateTime.Now:HH:mm:ss}] error"
        };
    }

    private static string BuildSystemPrompt(string action)
    {
        return action switch
        {
            "polish" => "你是一个文本润色助手。请对用户提供的文本进行润色，使其更加流畅、专业、易读。保持原意不变，只输出润色后的文本，不要加任何解释。",
            "summarize" => "你是一个文本摘要助手。请对用户提供的内容进行简洁的摘要总结，提取关键要点。只输出摘要内容，不要加任何解释。",
            "organize" => "你是一个内容整理助手。请将用户提供的内容进行结构化整理，使用清晰的标题、列表和分段。只输出整理后的内容。",
            "complete" => "你是一个内容补全助手。请根据用户提供的开头或上下文，补全缺失的内容。只输出补全后的文本。",
            "continue" => "你是一个内容续写助手。请根据用户提供的内容进行自然续写，保持风格和语气一致。只输出续写内容。",
            "correct" => "你是一个语法纠错助手。请纠正用户文本中的语法错误、拼写错误和标点问题。保持原意不变。如果原文正确，返回原文。",
            _ => "你是一个智能助手。请简洁准确地回答用户的问题。"
        };
    }

    private static string GetConfigString(Dictionary<string, object> config, string key, string defaultValue = "")
    {
        if (config.TryGetValue(key, out var val) && val is JsonElement je)
            return je.ValueKind == JsonValueKind.String ? je.GetString() ?? defaultValue : defaultValue;
        return defaultValue;
    }

    private async Task<string> CallOpenAiCompatibleAsync(Dictionary<string, object> config,
        string action, string text, string? context)
    {
        var apiUrl = GetConfigString(config, "aiApiUrl");
        var apiKey = GetConfigString(config, "aiApiKey");
        var model = GetConfigString(config, "aiModel", "gpt-4o-mini");

        if (string.IsNullOrWhiteSpace(apiUrl))
            throw new InvalidOperationException("未配置 API 地址");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("未配置 API 密钥");

        // 自动补全 API 路径：如果 URL 不包含 /chat/completions，追加它
        if (!apiUrl.Contains("/chat/completions"))
        {
            apiUrl = apiUrl.TrimEnd('/') + "/v1/chat/completions";
        }

        var systemPrompt = BuildSystemPrompt(action);
        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt }
        };

        if (!string.IsNullOrWhiteSpace(context))
            messages.Add(new { role = "assistant", content = context });

        messages.Add(new { role = "user", content = text });

        var payload = new
        {
            model,
            messages,
            temperature = 0.7,
            max_tokens = 2048
        };

        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
        _aiHttpClient.DefaultRequestHeaders.Clear();
        _aiHttpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        var httpResponse = await _aiHttpClient.PostAsync(apiUrl, httpContent);
        var responseBody = await httpResponse.Content.ReadAsStringAsync();

        if (!httpResponse.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"API {apiUrl}\n状态码: {httpResponse.StatusCode}\n响应: {Truncate(responseBody, 300)}");

        using var doc = JsonDocument.Parse(responseBody);
        var choices = doc.RootElement.GetProperty("choices");
        var first = choices[0];
        var message = first.GetProperty("message");
        return message.GetProperty("content").GetString() ?? "";
    }

    private async Task<string> CallOllamaAsync(Dictionary<string, object> config,
        string action, string text, string? context)
    {
        var ollamaUrl = GetConfigString(config, "aiOllamaUrl", "http://localhost:11434").TrimEnd('/');
        var model = GetConfigString(config, "aiOllamaModel", "llama3");

        var apiUrl = $"{ollamaUrl}/api/chat";
        var systemPrompt = BuildSystemPrompt(action);

        var messages = new List<object> { new { role = "system", content = systemPrompt } };
        if (!string.IsNullOrWhiteSpace(context))
            messages.Add(new { role = "assistant", content = context });
        messages.Add(new { role = "user", content = text });

        var payload = new { model, messages, stream = false };
        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        _aiHttpClient.DefaultRequestHeaders.Clear();
        var httpResponse = await _aiHttpClient.PostAsync(apiUrl, httpContent);
        var responseBody = await httpResponse.Content.ReadAsStringAsync();

        if (!httpResponse.IsSuccessStatusCode)
            throw new InvalidOperationException($"Ollama 返回错误 ({httpResponse.StatusCode}): {Truncate(responseBody, 200)}");

        using var doc = JsonDocument.Parse(responseBody);
        var msg = doc.RootElement.GetProperty("message");
        return msg.GetProperty("content").GetString() ?? "";
    }

    private static string Truncate(string text, int maxLen) =>
        text.Length <= maxLen ? text : text[..maxLen] + "...";

    // ========================================================================
    // 辅助方法
    // ========================================================================

    private string GetArg(JsonElement args, string key)
    {
        if (args.ValueKind == JsonValueKind.Object && args.TryGetProperty(key, out var val))
        {
            if (val.ValueKind == JsonValueKind.String) return val.GetString() ?? "";
            if (val.ValueKind == JsonValueKind.Null) return "";
            return val.GetRawText();
        }
        return "";
    }

    private string ResolvePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return _workDir;
        if (Path.IsPathRooted(path)) return path;
        return Path.GetFullPath(Path.Combine(_workDir, path));
    }

    private static string GetSnippet(string content, string query, int maxLen)
    {
        var idx = content.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return content.Length > maxLen ? content[..maxLen] + "..." : content;
        var start = Math.Max(0, idx - maxLen / 2);
        var end = Math.Min(content.Length, idx + query.Length + maxLen / 2);
        return (start > 0 ? "..." : "") + content[start..end] + (end < content.Length ? "..." : "");
    }

    private static int CountMatches(string content, string query)
    {
        var count = 0;
        var idx = 0;
        while ((idx = content.IndexOf(query, idx, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            idx += query.Length;
        }
        return count;
    }

    private static string EscapeJson(string text)
    {
        return text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    }
}
