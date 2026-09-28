using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ServiceDesk.Desktop.Views;

// Сообщения и подтверждения
public static class Dialogs
{
    private static Window Owner => Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current?.MainWindow;

    public static void Error(string message) =>
        MessageBox.Show(Owner, message, "СервисДеск — ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);

    public static void Info(string message) =>
        MessageBox.Show(Owner, message, "СервисДеск", MessageBoxButton.OK, MessageBoxImage.Information);

    public static bool Confirm(string message) =>
        MessageBox.Show(Owner, message, "СервисДеск — подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public static BitmapImage Logo => new(new Uri("pack://application:,,,/Assets/logo.png"));
}

// Универсальная форма ввода: набор полей с подписями и кнопки «Сохранить» / «Отмена»
public class FormDialog : Window
{
    private readonly StackPanel _fields = new();
    private readonly TextBlock _error = new() { Foreground = (Brush)new BrushConverter().ConvertFromString("#C0392B"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8), Visibility = Visibility.Collapsed };
    private readonly Dictionary<string, FrameworkElement> _controls = new();
    private readonly Button _ok;

    public Func<FormDialog, string> Validate { get; set; }

    public FormDialog(string title, string okText = "Сохранить", double width = 440)
    {
        Title = title;
        Width = width;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
        Style = (Style)Application.Current.FindResource("AppWindow");
        Icon = Dialogs.Logo;
        ShowInTaskbar = false;

        _ok = new Button { Content = okText, IsDefault = true, MinWidth = 120 };
        _ok.Click += (_, _) => Accept();
        var cancel = new Button { Content = "Отмена", IsCancel = true, MinWidth = 100, Margin = new Thickness(0, 0, 10, 0), Style = (Style)Application.Current.FindResource("GhostButton") };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        buttons.Children.Add(cancel);
        buttons.Children.Add(_ok);

        var root = new StackPanel { Margin = new Thickness(22) };
        root.Children.Add(new TextBlock { Text = title, Style = (Style)Application.Current.FindResource("H1"), Margin = new Thickness(0, 0, 0, 14) });
        root.Children.Add(_error);
        root.Children.Add(_fields);
        root.Children.Add(buttons);
        Content = root;
    }

    public FormDialog Note(string text)
    {
        _fields.Children.Add(new TextBlock { Text = text, Style = (Style)Application.Current.FindResource("MutedText"), Margin = new Thickness(0, 0, 0, 12) });
        return this;
    }

    public FormDialog Text(string key, string label, string value = "", bool multiline = false)
    {
        var tb = new TextBox { Text = value ?? "" };
        if (multiline) tb.Style = (Style)Application.Current.FindResource("MultiLine");
        return Add(key, label, tb);
    }

    public FormDialog Number(string key, string label, decimal? value = null)
    {
        var tb = new TextBox { Text = value?.ToString(CultureInfo.GetCultureInfo("ru-RU")) ?? "" };
        return Add(key, label, tb);
    }

    public FormDialog Combo(string key, string label, IEnumerable<object> items, string display, object selected = null)
    {
        var cb = new ComboBox { ItemsSource = items.ToList(), DisplayMemberPath = display, SelectedItem = selected, IsEditable = false };
        if (cb.SelectedItem == null && cb.Items.Count > 0) cb.SelectedIndex = 0;
        return Add(key, label, cb);
    }

    public FormDialog Check(string key, string label, bool value = false)
    {
        var cb = new CheckBox { Content = label, IsChecked = value, Margin = new Thickness(0, 0, 0, 12) };
        _controls[key] = cb;
        _fields.Children.Add(cb);
        return this;
    }

    private FormDialog Add(string key, string label, FrameworkElement control)
    {
        _fields.Children.Add(new TextBlock { Text = label, Style = (Style)Application.Current.FindResource("Label") });
        control.Margin = new Thickness(0, 0, 0, 12);
        _controls[key] = control;
        _fields.Children.Add(control);
        if (_controls.Count == 1) Loaded += (_, _) => control.Focus();
        return this;
    }

    public string GetText(string key) => ((TextBox)_controls[key]).Text.Trim();
    public T GetItem<T>(string key) => (T)((ComboBox)_controls[key]).SelectedItem;
    public bool GetCheck(string key) => ((CheckBox)_controls[key]).IsChecked == true;

    public ComboBox GetCombo(string key) => (ComboBox)_controls[key];
    public TextBox GetTextBox(string key) => (TextBox)_controls[key];

    public decimal? GetDecimal(string key)
    {
        var s = GetText(key).Replace(" ", "").Replace(",", ".");
        return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    public int? GetInt(string key) => int.TryParse(GetText(key).Replace(" ", ""), out var v) ? v : null;

    public void ShowError(string message)
    {
        _error.Text = message;
        _error.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Accept()
    {
        var err = Validate?.Invoke(this);
        if (!string.IsNullOrEmpty(err))
        {
            ShowError(err);
            return;
        }
        DialogResult = true;
    }
}
