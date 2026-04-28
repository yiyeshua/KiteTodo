using System.IO;
using System.Text.Json;
using KiteTodo.Models;

namespace KiteTodo.Services;

public class NavigationDirectoryService
{
    private sealed class NavigationDirectorySettings
    {
        public List<string> FavoriteIds { get; set; } = [];

        public List<string> RecentIds { get; set; } = [];

        public List<NavigationDirectoryItem> CustomItems { get; set; } = [];
    }

    private readonly string _settingsFilePath;
    private NavigationDirectorySettings _settings;

    public NavigationDirectoryService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KiteTodo");
        Directory.CreateDirectory(appDataPath);
        _settingsFilePath = Path.Combine(appDataPath, "navigation-directory.json");
        _settings = LoadSettings();
    }

    public List<NavigationDirectoryItem> GetItems()
    {
        var builtIn = GetBuiltInItems().Select(item => Clone(item)).ToList();
        var custom = _settings.CustomItems.Select(item =>
        {
            var clone = Clone(item);
            clone.IsBuiltIn = false;
            return clone;
        }).ToList();

        var items = builtIn.Concat(custom)
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        var favoriteSet = _settings.FavoriteIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < _settings.RecentIds.Count; index++)
        {
            var recentId = _settings.RecentIds[index];
            var item = items.FirstOrDefault(candidate => string.Equals(candidate.Id, recentId, StringComparison.OrdinalIgnoreCase));
            if (item != null)
                item.RecentOrder = index;
        }

        foreach (var item in items)
            item.IsFavorite = favoriteSet.Contains(item.Id);

        return items;
    }

    public NavigationDirectoryItem AddCustomItem(NavigationDirectoryItem item)
    {
        item.Id = BuildCustomId(item.Name, item.Url);
        item.IsBuiltIn = false;
        _settings.CustomItems.RemoveAll(existing => string.Equals(existing.Id, item.Id, StringComparison.OrdinalIgnoreCase));
        _settings.CustomItems.Add(Clone(item));
        SaveSettings();
        return item;
    }

    public NavigationDirectoryItem UpdateCustomItem(NavigationDirectoryItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Id))
            throw new ArgumentException("更新自定义站点时必须提供 Id。", nameof(item));

        item.IsBuiltIn = false;
        _settings.CustomItems.RemoveAll(existing => string.Equals(existing.Id, item.Id, StringComparison.OrdinalIgnoreCase));
        _settings.CustomItems.Add(Clone(item));
        SaveSettings();
        return item;
    }

    public void DeleteCustomItem(string id)
    {
        _settings.CustomItems.RemoveAll(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
        _settings.FavoriteIds.RemoveAll(itemId => string.Equals(itemId, id, StringComparison.OrdinalIgnoreCase));
        _settings.RecentIds.RemoveAll(itemId => string.Equals(itemId, id, StringComparison.OrdinalIgnoreCase));
        SaveSettings();
    }

    public int DeleteCustomItemsByCategory(string category)
    {
        var removedIds = _settings.CustomItems
            .Where(item => string.Equals(item.Category, category, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (removedIds.Count == 0)
            return 0;

        _settings.CustomItems.RemoveAll(item => removedIds.Contains(item.Id));
        _settings.FavoriteIds.RemoveAll(itemId => removedIds.Contains(itemId));
        _settings.RecentIds.RemoveAll(itemId => removedIds.Contains(itemId));
        SaveSettings();
        return removedIds.Count;
    }

    public void SetFavorite(string id, bool isFavorite)
    {
        _settings.FavoriteIds.RemoveAll(itemId => string.Equals(itemId, id, StringComparison.OrdinalIgnoreCase));
        if (isFavorite)
            _settings.FavoriteIds.Insert(0, id);

        SaveSettings();
    }

    public void RecordVisit(string id)
    {
        _settings.RecentIds.RemoveAll(itemId => string.Equals(itemId, id, StringComparison.OrdinalIgnoreCase));
        _settings.RecentIds.Insert(0, id);
        if (_settings.RecentIds.Count > 20)
            _settings.RecentIds = _settings.RecentIds.Take(20).ToList();

        SaveSettings();
    }

    public IReadOnlyList<NavigationDirectoryItem> GetBuiltInItems()
    {
        return
        [
            new NavigationDirectoryItem
            {
                Id = "jyshare",
                Name = "JY Share",
                Url = "https://www.jyshare.com/",
                Category = "综合工具",
                Description = "综合型在线工具与教程站，覆盖编程、转换、校验、开发辅助等大量场景。",
                Tags = ["综合", "开发", "转换", "教程"],
                SortOrder = 10
            },
            new NavigationDirectoryItem
            {
                Id = "toolsdar",
                Name = "ToolsDar",
                Url = "https://toolsdar.cn/",
                Category = "综合工具",
                Description = "工具导航聚合站，适合快速发现各类在线工具和效率网站。",
                Tags = ["导航", "聚合", "效率"],
                SortOrder = 20
            },
            new NavigationDirectoryItem
            {
                Id = "jsoncn",
                Name = "JSON.cn",
                Url = "https://www.json.cn/",
                Category = "数据格式",
                Description = "JSON 解析、格式化、压缩、转义等常用数据处理入口。",
                Tags = ["JSON", "格式化", "压缩"],
                SortOrder = 30
            },
            new NavigationDirectoryItem
            {
                Id = "bejson",
                Name = "Be JSON",
                Url = "https://www.bejson.com/",
                Category = "数据格式",
                Description = "JSON、XML、正则、编码转换等常用开发工具集合。",
                Tags = ["JSON", "XML", "编码"],
                SortOrder = 40
            },
            new NavigationDirectoryItem
            {
                Id = "toollu",
                Name = "Tool.lu",
                Url = "https://tool.lu/",
                Category = "开发调试",
                Description = "开发者常用在线工具站，包含编码、压缩、网络、时间与格式转换。",
                Tags = ["开发", "调试", "格式转换"],
                SortOrder = 50
            },
            new NavigationDirectoryItem
            {
                Id = "regex101",
                Name = "Regex101",
                Url = "https://regex101.com/",
                Category = "开发调试",
                Description = "正则表达式测试、解释与调试工具，适合快速验证规则。",
                Tags = ["正则", "调试", "表达式"],
                SortOrder = 60
            },
            new NavigationDirectoryItem
            {
                Id = "timestamp",
                Name = "Unix Timestamp",
                Url = "https://www.unixtimestamp.com/",
                Category = "时间日期",
                Description = "时间戳与日期时间互转，适合接口调试和日志分析。",
                Tags = ["时间戳", "日期", "转换"],
                SortOrder = 70
            },
            new NavigationDirectoryItem
            {
                Id = "base64guru",
                Name = "Base64 Guru",
                Url = "https://base64.guru/",
                Category = "编码转换",
                Description = "Base64 编解码、校验与数据 URI 处理。",
                Tags = ["Base64", "编码", "解码"],
                SortOrder = 80
            },
            new NavigationDirectoryItem
            {
                Id = "cyberchef",
                Name = "CyberChef",
                Url = "https://gchq.github.io/CyberChef/",
                Category = "编码转换",
                Description = "强大的数据转换工具箱，适合编码、哈希、压缩、解析等复杂场景。",
                Tags = ["编码", "哈希", "转换"],
                SortOrder = 90
            },
            new NavigationDirectoryItem
            {
                Id = "tinypng",
                Name = "TinyPNG",
                Url = "https://tinypng.com/",
                Category = "图片处理",
                Description = "图片压缩工具，适合快速压缩 PNG 和 JPEG。",
                Tags = ["图片", "压缩", "PNG"],
                SortOrder = 100
            },
            new NavigationDirectoryItem
            {
                Id = "squoosh",
                Name = "Squoosh",
                Url = "https://squoosh.app/",
                Category = "图片处理",
                Description = "支持多格式转换与压缩对比的图片优化工具。",
                Tags = ["图片", "转换", "压缩"],
                SortOrder = 110
            },
            new NavigationDirectoryItem
            {
                Id = "it-tools",
                Name = "IT Tools",
                Url = "https://it-tools.tech/",
                Category = "开发调试",
                Description = "面向开发者的轻量工具合集，包含 UUID、哈希、编码、网络等常用能力。",
                Tags = ["开发", "哈希", "UUID"],
                SortOrder = 120
            }
        ];
    }

    private NavigationDirectorySettings LoadSettings()
    {
        if (!File.Exists(_settingsFilePath))
            return new NavigationDirectorySettings();

        try
        {
            var settings = JsonSerializer.Deserialize<NavigationDirectorySettings>(File.ReadAllText(_settingsFilePath)) ?? new NavigationDirectorySettings();
            settings.FavoriteIds ??= [];
            settings.RecentIds ??= [];
            settings.CustomItems ??= [];
            return settings;
        }
        catch
        {
            return new NavigationDirectorySettings();
        }
    }

    private void SaveSettings()
    {
        var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_settingsFilePath, json);
    }

    private static NavigationDirectoryItem Clone(NavigationDirectoryItem item)
    {
        return new NavigationDirectoryItem
        {
            Id = item.Id,
            Name = item.Name,
            Url = item.Url,
            Category = item.Category,
            Description = item.Description,
            Tags = item.Tags.ToArray(),
            SortOrder = item.SortOrder,
            IsBuiltIn = item.IsBuiltIn,
            IsFavorite = item.IsFavorite,
            RecentOrder = item.RecentOrder
        };
    }

    private static string BuildCustomId(string name, string url)
    {
        var normalizedName = new string(name.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit).Take(18).ToArray());
        var fallback = Math.Abs(url.Trim().ToLowerInvariant().GetHashCode()).ToString();
        if (string.IsNullOrWhiteSpace(normalizedName))
            normalizedName = "custom";

        return $"custom-{normalizedName}-{fallback}";
    }
}