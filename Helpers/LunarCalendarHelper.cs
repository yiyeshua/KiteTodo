using System.Globalization;

namespace KiteTodo.Helpers;

/// <summary>
/// 农历日期转换辅助类，基于 .NET 内置 ChineseLunisolarCalendar
/// </summary>
public static class LunarCalendarHelper
{
    private static readonly ChineseLunisolarCalendar _lunar = new();

    private static readonly string[] MonthNames =
        ["正月", "二月", "三月", "四月", "五月", "六月", "七月", "八月", "九月", "十月", "冬月", "腊月"];

    private static readonly string[] DayNames =
    [
        "初一", "初二", "初三", "初四", "初五", "初六", "初七", "初八", "初九", "初十",
        "十一", "十二", "十三", "十四", "十五", "十六", "十七", "十八", "十九", "二十",
        "廿一", "廿二", "廿三", "廿四", "廿五", "廿六", "廿七", "廿八", "廿九", "三十"
    ];

    // 常见农历节日 (月, 日) → 名称
    private static readonly Dictionary<(int, int), string> LunarFestivals = new()
    {
        [(1, 1)] = "春节",
        [(1, 15)] = "元宵",
        [(5, 5)] = "端午",
        [(7, 7)] = "七夕",
        [(7, 15)] = "中元",
        [(8, 15)] = "中秋",
        [(9, 9)] = "重阳",
        [(12, 30)] = "除夕",
    };

    // 公历节日 (月, 日) → 名称
    private static readonly Dictionary<(int, int), string> SolarFestivals = new()
    {
        [(1, 1)] = "元旦",
        [(2, 14)] = "情人节",
        [(3, 8)] = "妇女节",
        [(4, 5)] = "清明",
        [(5, 1)] = "劳动节",
        [(6, 1)] = "儿童节",
        [(10, 1)] = "国庆节",
        [(12, 25)] = "圣诞节",
    };

    /// <summary>
    /// 获取农历日期简短显示文本。
    /// 优先级：节日 > 月初显示月名 > 日名
    /// </summary>
    public static string GetLunarDateText(DateTime date)
    {
        try
        {
            // 检查公历节日
            if (SolarFestivals.TryGetValue((date.Month, date.Day), out var solarFestival))
                return solarFestival;

            int lunarYear = _lunar.GetYear(date);
            int lunarMonth = _lunar.GetMonth(date);
            int lunarDay = _lunar.GetDayOfMonth(date);

            // 处理闰月：GetLeapMonth 返回闰月在该年的序号（0=无闰月）
            int leapMonth = _lunar.GetLeapMonth(lunarYear);
            int actualMonth = lunarMonth;
            bool isLeapMonth = false;

            if (leapMonth > 0)
            {
                if (lunarMonth == leapMonth)
                {
                    isLeapMonth = true;
                    actualMonth = lunarMonth - 1;
                }
                else if (lunarMonth > leapMonth)
                {
                    actualMonth = lunarMonth - 1;
                }
            }

            // 检查农历节日
            if (!isLeapMonth && LunarFestivals.TryGetValue((actualMonth, lunarDay), out var lunarFestival))
                return lunarFestival;

            // 除夕特殊处理：腊月最后一天（可能是廿九）
            if (!isLeapMonth && actualMonth == 12)
            {
                int daysInLunarMonth = _lunar.GetDaysInMonth(lunarYear, lunarMonth);
                if (lunarDay == daysInLunarMonth)
                    return "除夕";
            }

            // 初一显示月名
            if (lunarDay == 1)
            {
                string monthName = MonthNames[actualMonth - 1];
                return isLeapMonth ? $"闰{monthName}" : monthName;
            }

            // 其他日期显示日名
            return DayNames[lunarDay - 1];
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// 获取完整的农历日期描述，如 "农历三月初五" 或 "农历闰四月十二"
    /// </summary>
    public static string GetFullLunarDateText(DateTime date)
    {
        try
        {
            int lunarYear = _lunar.GetYear(date);
            int lunarMonth = _lunar.GetMonth(date);
            int lunarDay = _lunar.GetDayOfMonth(date);

            int leapMonth = _lunar.GetLeapMonth(lunarYear);
            int actualMonth = lunarMonth;
            bool isLeapMonth = false;

            if (leapMonth > 0)
            {
                if (lunarMonth == leapMonth)
                {
                    isLeapMonth = true;
                    actualMonth = lunarMonth - 1;
                }
                else if (lunarMonth > leapMonth)
                {
                    actualMonth = lunarMonth - 1;
                }
            }

            string monthName = MonthNames[actualMonth - 1];
            string dayName = DayNames[lunarDay - 1];
            string prefix = isLeapMonth ? "闰" : "";

            return $"农历{prefix}{monthName}{dayName}";
        }
        catch
        {
            return string.Empty;
        }
    }
}
