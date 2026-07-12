using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KiteTodo.Helpers;
using KiteTodo.Models;
using KiteTodo.Services;

namespace KiteTodo.Views.Pages;

public partial class SearchPage : Page
{
    private readonly SearchService _searchService = new();

    public SearchPage()
    {
        InitializeComponent();
        ResultList.Visibility = Visibility.Collapsed;
    }

    private void OnSearch(object sender, RoutedEventArgs e)
    {
        PerformSearch();
    }

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            PerformSearch();
    }

    private void PerformSearch()
    {
        var keyword = SearchBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(keyword))
        {
            ResultList.ItemsSource = null;
            ResultList.Visibility = Visibility.Collapsed;
            EmptyStatePanel.Visibility = Visibility.Visible;
            EmptyStateText.Text = "输入关键词开始搜索";
            SummaryText.Text = string.Empty;
            return;
        }

        var results = _searchService.Search(keyword);
        ResultList.ItemsSource = results;
        ResultList.Visibility = results.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyStatePanel.Visibility = results.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyStateText.Text = "没有找到匹配结果";
        SummaryText.Text = $"共找到 {results.Count} 条结果";
    }

    private void OnOpenResult(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is SearchResultItem result)
            OpenResult(result);
    }

    private void OnOpenSelectedResult(object sender, MouseButtonEventArgs e)
    {
        if (ResultList.SelectedItem is SearchResultItem result)
            OpenResult(result);
    }

    private void OpenResult(SearchResultItem result)
    {
        if (Window.GetWindow(this) is not MainWindow mainWindow)
            return;

        switch (result.TargetType)
        {
            case SearchTargetType.Todo:
                SearchNavigationRequest.PendingTodoDate = result.ScheduledDate;
                mainWindow.NavigateTo(typeof(HomePage));
                break;
            case SearchTargetType.Backlog:
                mainWindow.NavigateTo(typeof(BacklogPage));
                break;
            case SearchTargetType.Note:
                mainWindow.NavigateTo(typeof(NotebookProPage));
                break;
        }
    }
}