using System.Windows;

namespace KiteTodo.Views;

public partial class BacklogTransferDialog : Window
{
    public enum TransferChoice
    {
        Cancel,
        Move,
        Copy
    }

    public TransferChoice SelectedChoice { get; private set; } = TransferChoice.Cancel;

    public BacklogTransferDialog(string? noteTitle)
    {
        InitializeComponent();
        NoteTitleText.Text = string.IsNullOrWhiteSpace(noteTitle) ? "未命名笔记" : noteTitle.Trim();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        SelectedChoice = TransferChoice.Cancel;
        DialogResult = false;
        Close();
    }

    private void OnCopy(object sender, RoutedEventArgs e)
    {
        SelectedChoice = TransferChoice.Copy;
        DialogResult = true;
        Close();
    }

    private void OnMove(object sender, RoutedEventArgs e)
    {
        SelectedChoice = TransferChoice.Move;
        DialogResult = true;
        Close();
    }
}