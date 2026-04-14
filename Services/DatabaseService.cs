// ============================================================================
// DatabaseService.cs - 数据库服务（单例）
// 负责管理 LiteDB 数据库连接，提供各集合的访问入口
// 数据库文件位于: %LOCALAPPDATA%\KiteTodo\kitetodo.db
// ============================================================================

using System.IO;
using LiteDB;
using KiteTodo.Models;

namespace KiteTodo.Services;

/// <summary>
/// 数据库服务，使用懒加载单例模式（Lazy Singleton）。
/// 整个应用共享同一个 LiteDB 连接实例。
/// 
/// 使用方式：DatabaseService.Instance.Todos / .Pomodoros / .Settings
/// </summary>
public class DatabaseService
{
    // 懒加载单例：首次访问 Instance 时才创建实例
    private static readonly Lazy<DatabaseService> _instance = new(() => new DatabaseService());
    public static DatabaseService Instance => _instance.Value;

    // LiteDB 数据库实例（轻量级嵌入式 NoSQL 数据库，类似 SQLite）
    private readonly LiteDatabase _db;

    /// <summary>待办事项集合（对应 TodoItem 类）</summary>
    public ILiteCollection<TodoItem> Todos => _db.GetCollection<TodoItem>("todos");

    /// <summary>番茄钟记录集合（对应 PomodoroRecord 类）</summary>
    public ILiteCollection<PomodoroRecord> Pomodoros => _db.GetCollection<PomodoroRecord>("pomodoros");

    /// <summary>应用设置集合（对应 AppSettings 类，只有一条记录）</summary>
    public ILiteCollection<AppSettings> Settings => _db.GetCollection<AppSettings>("settings");

    /// <summary>笔记集合（对应 Note 类）</summary>
    public ILiteCollection<Note> Notes => _db.GetCollection<Note>("notes");

    private DatabaseService()
    {
        // 数据库文件存放在用户本地应用数据目录下
        // 例如: C:\Users\<用户名>\AppData\Local\KiteTodo\kitetodo.db
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KiteTodo");
        Directory.CreateDirectory(appDataPath); // 目录不存在则自动创建

        var dbPath = Path.Combine(appDataPath, "kitetodo.db");
        // Connection=shared 允许多个读写器并发访问同一个数据库文件
        _db = new LiteDatabase($"Filename={dbPath};Connection=shared");

        EnsureIndexes();
        EnsureDefaultSettings();
    }

    /// <summary>为常用查询字段创建索引，提升查询性能</summary>
    private void EnsureIndexes()
    {
        Todos.EnsureIndex(x => x.ScheduledDate);
        Todos.EnsureIndex(x => x.CompletedAt);
        Todos.EnsureIndex(x => x.Category);
        Pomodoros.EnsureIndex(x => x.StartTime);
    }

    /// <summary>确保设置集合中有默认记录（首次运行时插入）</summary>
    private void EnsureDefaultSettings()
    {
        if (Settings.Count() == 0)
        {
            Settings.Insert(new AppSettings());
        }
    }

    /// <summary>获取应用设置（始终返回 Id=1 的记录）</summary>
    public AppSettings GetSettings()
    {
        return Settings.FindById(1) ?? new AppSettings();
    }

    /// <summary>保存应用设置（Upsert = 存在则更新，不存在则插入）</summary>
    public void SaveSettings(AppSettings settings)
    {
        settings.Id = 1;
        Settings.Upsert(settings);
    }
}
