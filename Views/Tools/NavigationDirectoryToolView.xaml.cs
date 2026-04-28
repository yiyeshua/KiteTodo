using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KiteTodo.Models;
using KiteTodo.Services;

namespace KiteTodo.Views.Tools;

public partial class NavigationDirectoryToolView : UserControl
{
    private static readonly HashSet<string> ReservedCategories =
    [
        "全部",
        "收藏",
        "最近访问",
        "自定义"
    ];

    private readonly NavigationDirectoryService _navigationDirectoryService = new();
    private List<NavigationDirectoryItem> _allItems;
    private readonly ObservableCollection<NavigationDirectoryCategoryItem> _categories = [];
    private readonly ObservableCollection<NavigationDirectoryItem> _visibleItems = [];
    private string _selectedCategory = "全部";
    private string? _editingItemId;
    private bool _suppressFilterRefresh;

    public NavigationDirectoryToolView()
    {
        InitializeComponent();

        _allItems = [];

        CategoryListBox.ItemsSource = _categories;
        SiteListBox.ItemsSource = _visibleItems;

        ReloadItems();
    }

    private void BuildCategories()
    {
        _categories.Clear();
        _categories.Add(new NavigationDirectoryCategoryItem { Name = "全部", ItemCount = _allItems.Count, CanDelete = false });
        _categories.Add(new NavigationDirectoryCategoryItem { Name = "收藏", ItemCount = _allItems.Count(item => item.IsFavorite), CanDelete = false });
        _categories.Add(new NavigationDirectoryCategoryItem { Name = "最近访问", ItemCount = _allItems.Count(item => item.IsRecent), CanDelete = false });

        if (_allItems.Any(item => !item.IsBuiltIn))
            _categories.Add(new NavigationDirectoryCategoryItem
            {
                Name = "自定义",
                ItemCount = _allItems.Count(item => !item.IsBuiltIn),
                CanDelete = false
            });

        foreach (var group in _allItems
                     .GroupBy(item => item.Category)
                     .OrderBy(group => group.Key, StringComparer.CurrentCulture))
        {
            if (_categories.Any(item => string.Equals(item.Name, group.Key, StringComparison.Ordinal)))
                continue;

            var allCustom = group.All(item => !item.IsBuiltIn);
            _categories.Add(new NavigationDirectoryCategoryItem
            {
                Name = group.Key,
                ItemCount = group.Count(),
                CanDelete = allCustom && !ReservedCategories.Contains(group.Key)
            });
        }

        if (!_categories.Any(item => string.Equals(item.Name, _selectedCategory, StringComparison.Ordinal)))
            _selectedCategory = "全部";

        CategoryListBox.SelectedItem = _categories.FirstOrDefault(item => string.Equals(item.Name, _selectedCategory, StringComparison.Ordinal));
    }

    private void OnCategorySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressFilterRefresh)
            return;

        _selectedCategory = (CategoryListBox.SelectedItem as NavigationDirectoryCategoryItem)?.Name ?? "全部";
        ApplyFilter();
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressFilterRefresh)
            return;

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var keyword = SearchTextBox.Text?.Trim() ?? string.Empty;

        var filtered = _allItems.Where(item =>
            MatchCategory(item, _selectedCategory)
            && (string.IsNullOrWhiteSpace(keyword)
                || item.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || item.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || item.Url.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || item.Tags.Any(tag => tag.Contains(keyword, StringComparison.OrdinalIgnoreCase))));

        var list = filtered
            .OrderBy(item => item.IsFavorite ? 0 : 1)
            .ThenBy(item => item.RecentOrder)
            .ThenBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .ToList();

        _visibleItems.Clear();
        foreach (var item in list)
            _visibleItems.Add(item);

        ResultSummaryTextBlock.Text = $"共 {list.Count} 个站点";
        SiteEmptyStateBorder.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnSiteDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SiteListBox.SelectedItem is NavigationDirectoryItem item)
            OpenItemInExternalBrowser(item);
    }

    private void OnOpenExternal(object sender, RoutedEventArgs e)
    {
        var item = GetNavigationItemFromSender(sender);
        if (item != null)
            OpenItemInExternalBrowser(item);
    }

    private void OnToggleFavoriteSite(object sender, RoutedEventArgs e)
    {
        var item = GetNavigationItemFromSender(sender);
        if (item == null)
            return;

        _navigationDirectoryService.SetFavorite(item.Id, !item.IsFavorite);
        ReloadItems(preserveCategory: true, preserveKeyword: true);
    }

    private void OnDeleteCustomSite(object sender, RoutedEventArgs e)
    {
        var item = GetNavigationItemFromSender(sender);
        if (item == null || !item.CanDelete)
            return;

        var result = MessageBox.Show($"确定删除自定义站点“{item.Name}”吗？", "导航大全", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes)
            return;

        _navigationDirectoryService.DeleteCustomItem(item.Id);
        ReloadItems(preserveCategory: true, preserveKeyword: true);
    }

    private void OnDeleteCategory(object sender, RoutedEventArgs e)
    {
        var category = GetCategoryItemFromSender(sender);
        if (category == null || !category.CanDelete)
            return;

        var result = MessageBox.Show(
            $"确定删除分类“{category.Name}”下的全部自定义站点吗？",
            "导航大全",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        var removedCount = _navigationDirectoryService.DeleteCustomItemsByCategory(category.Name);
        if (string.Equals(_selectedCategory, category.Name, StringComparison.Ordinal))
            _selectedCategory = "全部";

        ReloadItems(preserveCategory: true, preserveKeyword: true);
        AlertService.ShowCornerToast($"已删除 {removedCount} 个站点", 3);
    }

    private void OnToggleAddSiteForm(object sender, RoutedEventArgs e)
    {
        AddSiteFormBorder.Visibility = AddSiteFormBorder.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void OnCancelAddSite(object sender, RoutedEventArgs e)
    {
        ClearAddSiteForm();
        AddSiteFormBorder.Visibility = Visibility.Collapsed;
    }

    private void OnEditCustomSite(object sender, RoutedEventArgs e)
    {
        var item = GetNavigationItemFromSender(sender);
        if (item == null || !item.CanDelete)
            return;

        _editingItemId = item.Id;
        AddSiteFormTitleTextBlock.Text = "编辑自定义站点";
        AddSiteNameTextBox.Text = item.Name;
        AddSiteUrlTextBox.Text = item.Url;
        AddSiteCategoryTextBox.Text = item.Category;
        AddSiteTagsTextBox.Text = string.Join(" ", item.Tags);
        AddSiteDescriptionTextBox.Text = item.Description;
        AddSiteFormBorder.Visibility = Visibility.Visible;
        AddSiteNameTextBox.Focus();
        AddSiteNameTextBox.SelectAll();
    }

    private void OpenItemInExternalBrowser(NavigationDirectoryItem item, string? toastMessage = null, bool refreshAfterOpen = true)
    {
        try
        {
            _navigationDirectoryService.RecordVisit(item.Id);
            Process.Start(new ProcessStartInfo
            {
                FileName = item.Url,
                UseShellExecute = true
            });

            if (refreshAfterOpen)
                ReloadItems(preserveCategory: true, preserveKeyword: true);

            if (!string.IsNullOrWhiteSpace(toastMessage))
                AlertService.ShowCornerToast(toastMessage, 3);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法打开浏览器：{ex.Message}", "导航大全", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnSaveCustomSite(object sender, RoutedEventArgs e)
    {
        var name = AddSiteNameTextBox.Text.Trim();
        var url = AddSiteUrlTextBox.Text.Trim();
        var category = string.IsNullOrWhiteSpace(AddSiteCategoryTextBox.Text) ? "自定义" : AddSiteCategoryTextBox.Text.Trim();
        var description = string.IsNullOrWhiteSpace(AddSiteDescriptionTextBox.Text) ? "用户手动添加的导航站点。" : AddSiteDescriptionTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
        {
            MessageBox.Show("站点名称和 URL 不能为空。", "导航大全", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            MessageBox.Show("请输入有效的 http 或 https 地址。", "导航大全", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var item = new NavigationDirectoryItem
        {
            Id = _editingItemId ?? string.Empty,
            Name = name,
            Url = uri.ToString(),
            Category = category,
            Description = description,
            Tags = SplitTags(AddSiteTagsTextBox.Text),
            SortOrder = 9990,
            IsBuiltIn = false
        };

        var isEditing = !string.IsNullOrWhiteSpace(_editingItemId);
        var saved = isEditing
            ? _navigationDirectoryService.UpdateCustomItem(item)
            : _navigationDirectoryService.AddCustomItem(item);
        ClearAddSiteForm();
        AddSiteFormBorder.Visibility = Visibility.Collapsed;
        _selectedCategory = saved.Category;
        ReloadItems(preserveCategory: true, preserveKeyword: false);
        AlertService.ShowCornerToast(
            isEditing ? $"已更新站点：{saved.Name}" : $"已添加站点：{saved.Name}",
            3);
    }

    private void OnCopyUrl(object sender, RoutedEventArgs e)
    {
        var item = GetNavigationItemFromSender(sender);
        if (item == null)
            return;

        Clipboard.SetText(item.Url);
        AlertService.ShowCornerToast("链接已复制", 2);
    }

    private static NavigationDirectoryItem? GetNavigationItemFromSender(object sender)
    {
        return sender switch
        {
            FrameworkElement { Tag: NavigationDirectoryItem item } => item,
            MenuItem { CommandParameter: NavigationDirectoryItem item } => item,
            _ => null
        };
    }

    private static NavigationDirectoryCategoryItem? GetCategoryItemFromSender(object sender)
    {
        return sender switch
        {
            FrameworkElement { Tag: NavigationDirectoryCategoryItem item } => item,
            MenuItem { CommandParameter: NavigationDirectoryCategoryItem item } => item,
            _ => null
        };
    }

    private void ReloadItems(bool preserveCategory = true, bool preserveKeyword = true)
    {
        var keyword = preserveKeyword ? SearchTextBox.Text : string.Empty;
        var category = preserveCategory ? _selectedCategory : "全部";

        _suppressFilterRefresh = true;

        _allItems = _navigationDirectoryService.GetItems()
            .OrderBy(item => item.Category)
            .ThenBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .ToList();

        _selectedCategory = category;
        BuildCategories();
        if (!string.Equals(SearchTextBox.Text, keyword ?? string.Empty, StringComparison.Ordinal))
            SearchTextBox.Text = keyword ?? string.Empty;

        _suppressFilterRefresh = false;
        ApplyFilter();
    }

    private static bool MatchCategory(NavigationDirectoryItem item, string category)
    {
        return category switch
        {
            "全部" => true,
            "收藏" => item.IsFavorite,
            "最近访问" => item.IsRecent,
            "自定义" => !item.IsBuiltIn,
            _ => item.Category == category
        };
    }

    private static string[] SplitTags(string raw)
    {
        return raw.Split([' ', ',', '，', ';', '；', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private void ClearAddSiteForm()
    {
        _editingItemId = null;
        AddSiteFormTitleTextBlock.Text = "新增自定义站点";
        AddSiteNameTextBox.Clear();
        AddSiteUrlTextBox.Clear();
        AddSiteCategoryTextBox.Clear();
        AddSiteTagsTextBox.Clear();
        AddSiteDescriptionTextBox.Clear();
    }
}