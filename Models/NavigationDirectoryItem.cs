namespace KiteTodo.Models;

public class NavigationDirectoryItem
{
    public required string Id { get; set; }

    public required string Name { get; set; }

    public required string Url { get; set; }

    public required string Category { get; set; }

    public required string Description { get; set; }

    public string[] Tags { get; set; } = [];

    public int SortOrder { get; set; }

    public bool IsBuiltIn { get; set; } = true;

    public bool IsFavorite { get; set; }

    public int RecentOrder { get; set; } = int.MaxValue;

    public bool IsRecent => RecentOrder < int.MaxValue;

    public bool CanDelete => !IsBuiltIn;

    public string TagText => Tags.Length == 0 ? string.Empty : string.Join(" · ", Tags);

    public string BadgeText
    {
        get
        {
            var parts = Name.Split([' ', '-', '.', '_'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return string.Concat(parts[0][0], parts[1][0]).ToUpperInvariant();

            var letters = new string(Name.Where(char.IsLetterOrDigit).Take(2).ToArray());
            return string.IsNullOrWhiteSpace(letters) ? "NA" : letters.ToUpperInvariant();
        }
    }

    public string BadgeBackground => Category switch
    {
        "综合工具" => "#EAF4FF",
        "开发调试" => "#EEFCEF",
        "编码转换" => "#FFF3E8",
        "时间日期" => "#F3E8FF",
        "数据格式" => "#FFF7D6",
        "图片处理" => "#FFE8F0",
        _ => "#EEF4FB"
    };

    public string BadgeForeground => Category switch
    {
        "综合工具" => "#2563EB",
        "开发调试" => "#15803D",
        "编码转换" => "#C2410C",
        "时间日期" => "#7C3AED",
        "数据格式" => "#A16207",
        "图片处理" => "#BE185D",
        _ => "#52667A"
    };
}