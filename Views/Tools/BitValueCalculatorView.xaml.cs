using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KiteTodo.Services;

namespace KiteTodo.Views.Tools;

public partial class BitValueCalculatorView : UserControl
{
    private const int BitCount = 30;
    private const ulong MaxValue = (1UL << BitCount) - 1;

    public sealed class BitSlot : INotifyPropertyChanged
    {
        private string _valueText = "0";

        public required int BoxIndex { get; init; }
        public required int BitIndex { get; init; }

        public string BoxLabel => $"框 {BoxIndex}";
        public string BitLabel => $"bit {BitIndex}";

        public string ValueText
        {
            get => _valueText;
            set
            {
                if (_valueText == value)
                    return;

                _valueText = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public ObservableCollection<BitSlot> BitSlots { get; } = [];

    private bool _isNormalizingText;
    private bool _isUpdatingResults;

    public BitValueCalculatorView()
    {
        InitializeComponent();
        DataContext = this;

        for (var index = 0; index < BitCount; index++)
        {
            BitSlots.Add(new BitSlot
            {
                BoxIndex = index + 1,
                BitIndex = index,
                ValueText = "0"
            });
        }

        UpdateResults();
    }

    private void OnSetAllZero(object sender, RoutedEventArgs e)
    {
        SetAllBits("0");
    }

    private void OnSetAllOne(object sender, RoutedEventArgs e)
    {
        SetAllBits("1");
    }

    private void OnInvertBits(object sender, RoutedEventArgs e)
    {
        foreach (var slot in BitSlots)
            slot.ValueText = slot.ValueText == "1" ? "0" : "1";

        UpdateResults();
    }

    private void SetAllBits(string value)
    {
        foreach (var slot in BitSlots)
            slot.ValueText = value;

        UpdateResults();
    }

    private void OnBitTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = e.Text.Any(ch => ch != '0' && ch != '1');
    }

    private void OnBitPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.Tag is not BitSlot slot)
            return;

        if (e.Key == Key.Space)
        {
            slot.ValueText = slot.ValueText == "1" ? "0" : "1";
            textBox.Text = slot.ValueText;
            textBox.SelectAll();
            UpdateResults();
            e.Handled = true;
        }
    }

    private void OnBitTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isNormalizingText || sender is not TextBox textBox || textBox.Tag is not BitSlot slot)
            return;

        var normalized = NormalizeBitText(textBox.Text);
        if (textBox.Text != normalized)
        {
            _isNormalizingText = true;
            textBox.Text = normalized;
            textBox.CaretIndex = textBox.Text.Length;
            _isNormalizingText = false;
        }

        slot.ValueText = normalized;
        UpdateResults();
    }

    private void OnBitTextBoxGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox textBox)
            textBox.SelectAll();
    }

    private void OnBitTextBoxPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.DataObject.GetDataPresent(DataFormats.Text))
        {
            e.CancelCommand();
            return;
        }

        var pastedText = e.DataObject.GetData(DataFormats.Text) as string ?? string.Empty;
        if (!pastedText.Any(ch => ch == '0' || ch == '1'))
            e.CancelCommand();
    }

    private void OnBitCardMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount < 2 || sender is not Border { Tag: BitSlot slot })
            return;

        slot.ValueText = slot.ValueText == "1" ? "0" : "1";
        UpdateResults();
        e.Handled = true;
    }

    private void UpdateResults()
    {
        UpdateResultsFromValue(GetValueFromBits());
    }

    private void UpdateResultsFromValue(ulong value)
    {
        UpdateResultsFromValue(value, null);
    }

    private void UpdateResultsFromValue(ulong value, TextBox? editingTextBox)
    {
        _isUpdatingResults = true;
        SetResultText(DecimalResultTextBox, value.ToString(CultureInfo.InvariantCulture), editingTextBox);
        SetResultText(HexResultTextBox, value.ToString("X", CultureInfo.InvariantCulture), editingTextBox);
        SetResultText(BinaryPreviewTextBox, BuildBinaryPreview(value), editingTextBox);
        FormattedHexTextBlock.Text = BuildFormattedHexDisplay(value);
        _isUpdatingResults = false;
    }

    private static void SetResultText(TextBox target, string text, TextBox? editingTextBox)
    {
        if (ReferenceEquals(target, editingTextBox) && target.IsKeyboardFocused)
            return;

        if (target.Text == text)
            return;

        target.Text = text;
    }

    private string BuildBinaryPreview(ulong? explicitValue = null)
    {
        var value = explicitValue ?? GetValueFromBits();
        var bits = new StringBuilder(BitSlots.Count + 8);
        for (var index = BitSlots.Count - 1; index >= 0; index--)
        {
            bits.Append(((value >> index) & 1UL) == 1UL ? '1' : '0');
            if (index > 0 && index % 4 == 0)
                bits.Append(' ');
        }

        return bits.ToString();
    }

    private static string NormalizeBitText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "0";

        return text.Contains('1') ? "1" : "0";
    }

    private ulong GetValueFromBits()
    {
        ulong value = 0;
        for (var index = 0; index < BitSlots.Count; index++)
        {
            if (BitSlots[index].ValueText == "1")
                value |= 1UL << index;
        }

        return value;
    }

    private void ApplyValueToBits(ulong value)
    {
        ApplyValueToBits(value, null);
    }

    private void ApplyValueToBits(ulong value, TextBox? editingTextBox)
    {
        for (var index = 0; index < BitSlots.Count; index++)
            BitSlots[index].ValueText = ((value >> index) & 1UL) == 1UL ? "1" : "0";

        UpdateResultsFromValue(value, editingTextBox);
    }

    private void OnDecimalResultTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingResults || sender is not TextBox textBox)
            return;

        var caretIndex = textBox.SelectionStart;
        var selectionLength = textBox.SelectionLength;

        if (!TryParseDecimalValue(textBox.Text, out var value))
            return;

        ApplyValueToBits(value, textBox);
        RestoreTextBoxSelection(textBox, caretIndex, selectionLength);
    }

    private void OnHexResultTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingResults || sender is not TextBox textBox)
            return;

        var caretIndex = textBox.SelectionStart;
        var selectionLength = textBox.SelectionLength;

        if (!TryParseHexValue(textBox.Text, out var value))
            return;

        ApplyValueToBits(value, textBox);
        RestoreTextBoxSelection(textBox, caretIndex, selectionLength);
    }

    private void OnBinaryPreviewTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingResults || sender is not TextBox textBox)
            return;

        var caretIndex = textBox.SelectionStart;
        var selectionLength = textBox.SelectionLength;

        if (!TryParseBinaryValue(textBox.Text, out var value))
            return;

        ApplyValueToBits(value, textBox);
        RestoreTextBoxSelection(textBox, caretIndex, selectionLength);
    }

    private void OnResultTextBoxGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox textBox)
            textBox.SelectAll();
    }

    private void OnResultTextBoxLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        UpdateResults();
    }

    private void OnDecimalPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = e.Text.Any(ch => !char.IsDigit(ch));
    }

    private void OnHexPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = e.Text.Any(ch => !Uri.IsHexDigit(ch));
    }

    private void OnBinaryPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = e.Text.Any(ch => ch != '0' && ch != '1' && !char.IsWhiteSpace(ch));
    }

    private void OnDecimalTextBoxPasting(object sender, DataObjectPastingEventArgs e)
    {
        CancelPasteIfInvalid(e, text => text.All(char.IsDigit));
    }

    private void OnHexTextBoxPasting(object sender, DataObjectPastingEventArgs e)
    {
        CancelPasteIfInvalid(e, text => text.All(Uri.IsHexDigit));
    }

    private void OnBinaryTextBoxPasting(object sender, DataObjectPastingEventArgs e)
    {
        CancelPasteIfInvalid(e, text => text.All(ch => ch == '0' || ch == '1' || char.IsWhiteSpace(ch)));
    }

    private void OnCopyResult(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string target })
            return;

        var text = target switch
        {
            "decimal" => DecimalResultTextBox.Text,
            "hex" => HexResultTextBox.Text,
            "binary" => BinaryPreviewTextBox.Text,
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(text))
            return;

        Clipboard.SetText(text);
        AlertService.ShowCornerToast("结果已复制", 2);
    }

    private void OnBulkImport(object sender, RoutedEventArgs e)
    {
        if (!TryShowBulkImportDialog(out var inputText))
            return;

        if (!TryParseImportedValue(inputText, out var value))
        {
            MessageBox.Show("无法识别导入内容。支持十六进制（如 1F）、十进制、或二进制（可带空格）。", "批量导入", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ApplyValueToBits(value);
    }

    private static void CancelPasteIfInvalid(DataObjectPastingEventArgs e, Func<string, bool> validator)
    {
        if (!e.DataObject.GetDataPresent(DataFormats.Text))
        {
            e.CancelCommand();
            return;
        }

        var text = e.DataObject.GetData(DataFormats.Text) as string ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text) || !validator(text))
            e.CancelCommand();
    }

    private static void RestoreTextBoxSelection(TextBox textBox, int selectionStart, int selectionLength)
    {
        if (!textBox.IsKeyboardFocused)
            return;

        var safeStart = Math.Clamp(selectionStart, 0, textBox.Text.Length);
        var safeLength = Math.Clamp(selectionLength, 0, textBox.Text.Length - safeStart);
        textBox.Select(safeStart, safeLength);
    }

    private static string BuildFormattedHexDisplay(ulong value)
    {
        var rawHex = value.ToString("X", CultureInfo.InvariantCulture);
        return $"0x{GroupHexDigits(rawHex)}";
    }

    private static string GroupHexDigits(string hex)
    {
        if (hex.Length <= 4)
            return hex;

        var firstGroupLength = hex.Length % 4;
        if (firstGroupLength == 0)
            firstGroupLength = 4;

        var builder = new StringBuilder(hex.Length + (hex.Length / 4));
        builder.Append(hex.AsSpan(0, firstGroupLength));

        for (var index = firstGroupLength; index < hex.Length; index += 4)
        {
            builder.Append(' ');
            builder.Append(hex.AsSpan(index, Math.Min(4, hex.Length - index)));
        }

        return builder.ToString();
    }

    private bool TryShowBulkImportDialog(out string inputText)
    {
        inputText = string.Empty;

        var owner = Window.GetWindow(this);
        var inputBox = new TextBox
        {
            MinWidth = 320,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var hintText = new TextBlock
        {
            Text = "支持直接粘贴十六进制、十进制或二进制，例如 1F、31、0001 1111",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = System.Windows.Media.Brushes.DimGray
        };

        var okButton = new Button { Content = "导入", Width = 76, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        var cancelButton = new Button { Content = "取消", Width = 76, IsCancel = true };
        var pasteButton = new Button { Content = "粘贴", Width = 76, Margin = new Thickness(0, 0, 8, 0) };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0)
        };
        buttonPanel.Children.Add(pasteButton);
        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);

        var root = new StackPanel { Margin = new Thickness(16) };
        root.Children.Add(hintText);
        root.Children.Add(inputBox);
        root.Children.Add(buttonPanel);

        var dialog = new Window
        {
            Title = "批量导入位值",
            Content = root,
            SizeToContent = SizeToContent.WidthAndHeight,
            MinWidth = 420,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            Owner = owner,
            ShowInTaskbar = false
        };

        pasteButton.Click += (_, _) =>
        {
            if (Clipboard.ContainsText())
            {
                inputBox.Text = Clipboard.GetText();
                inputBox.SelectAll();
                inputBox.Focus();
            }
        };
        okButton.Click += (_, _) => dialog.DialogResult = true;
        cancelButton.Click += (_, _) => dialog.DialogResult = false;
        dialog.Loaded += (_, _) => inputBox.Focus();

        if (dialog.ShowDialog() != true)
            return false;

        inputText = inputBox.Text;
        return true;
    }

    private static bool TryParseImportedValue(string? text, out ulong value)
    {
        return TryParseHexValue(text, out value)
            || TryParseDecimalValue(text, out value)
            || TryParseBinaryValue(text, out value);
    }

    private static bool TryParseDecimalValue(string? text, out ulong value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return ulong.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out value) && value <= MaxValue;
    }

    private static bool TryParseHexValue(string? text, out ulong value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var normalized = text.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        return ulong.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value) && value <= MaxValue;
    }

    private static bool TryParseBinaryValue(string? text, out ulong value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var normalized = Regex.Replace(text, "\\s+", string.Empty);
        if (normalized.Length == 0 || normalized.Length > BitCount || normalized.Any(ch => ch != '0' && ch != '1'))
            return false;

        foreach (var ch in normalized)
        {
            value <<= 1;
            if (ch == '1')
                value |= 1;
        }

        return value <= MaxValue;
    }
}