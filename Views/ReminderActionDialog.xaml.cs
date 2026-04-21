using System.Windows;
using KiteTodo.Models;

namespace KiteTodo.Views;

public partial class ReminderActionDialog : Window
{
    public enum ReminderActionType
    {
        None,
        Complete,
        Snooze10Minutes,
        Snooze1Hour,
        MoveToTomorrow,
        OpenTodo
    }

    public ReminderActionType SelectedAction { get; private set; }

    public ReminderActionDialog(TodoItem todo)
    {
        InitializeComponent();
        TodoTitleText.Text = todo.Title;
        TodoDescriptionText.Text = string.IsNullOrWhiteSpace(todo.Description)
            ? $"计划日期：{todo.ScheduledDate:yyyy-MM-dd}"
            : todo.Description;
    }

    private void SetAction(ReminderActionType action, bool dialogResult)
    {
        SelectedAction = action;
        DialogResult = dialogResult;
        Close();
    }

    private void OnComplete(object sender, RoutedEventArgs e) => SetAction(ReminderActionType.Complete, true);

    private void OnSnooze10(object sender, RoutedEventArgs e) => SetAction(ReminderActionType.Snooze10Minutes, true);

    private void OnSnooze1Hour(object sender, RoutedEventArgs e) => SetAction(ReminderActionType.Snooze1Hour, true);

    private void OnMoveToTomorrow(object sender, RoutedEventArgs e) => SetAction(ReminderActionType.MoveToTomorrow, true);

    private void OnOpenTodo(object sender, RoutedEventArgs e) => SetAction(ReminderActionType.OpenTodo, true);

    private void OnClose(object sender, RoutedEventArgs e) => SetAction(ReminderActionType.None, false);
}