using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KiteTodo.Views;

public partial class FocusCompleteDialog : Window
{
    public string? FocusNote => string.IsNullOrWhiteSpace(NoteBox.Text) ? null : NoteBox.Text.Trim();
    public int SelectedMood { get; private set; }

    private Button? _selectedMoodBtn;

    public FocusCompleteDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => NoteBox.Focus();
    }

    private void OnMoodClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string s && int.TryParse(s, out int mood))
        {
            SelectedMood = mood;

            // 重置之前选中按钮的样式
            if (_selectedMoodBtn != null)
            {
                _selectedMoodBtn.BorderThickness = new Thickness(1);
                _selectedMoodBtn.BorderBrush = SystemColors.ControlDarkBrush;
            }

            // 高亮当前选中按钮
            btn.BorderThickness = new Thickness(2);
            btn.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 120, 212));
            _selectedMoodBtn = btn;
        }
    }

    private void OnConfirm(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnSkip(object sender, RoutedEventArgs e)
    {
        SelectedMood = 0;
        DialogResult = false;
    }
}
