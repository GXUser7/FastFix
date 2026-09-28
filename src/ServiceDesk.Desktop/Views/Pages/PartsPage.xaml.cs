using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ServiceDesk.Contracts;
using ServiceDesk.Desktop.Services;

namespace ServiceDesk.Desktop.Views.Pages;

// Остатки запчастей, новая позиция, оприходование
public partial class PartsPage : Page
{
    private readonly DispatcherTimer _delay = new() { Interval = TimeSpan.FromMilliseconds(300) };

    public PartsPage()
    {
        InitializeComponent();
        _delay.Tick += async (_, _) => { _delay.Stop(); await LoadAsync(); };
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var q = new List<string>();
        if (!string.IsNullOrWhiteSpace(SearchBox.Text)) q.Add("search=" + Uri.EscapeDataString(SearchBox.Text.Trim()));
        if (BelowMinBox.IsChecked == true) q.Add("belowMinimum=true");
        if (InactiveBox.IsChecked == true) q.Add("includeInactive=true");
        try
        {
            var selected = (Grid.SelectedItem as PartDto)?.Id;
            var list = await ApiClient.GetAsync<List<PartDto>>("parts" + (q.Count > 0 ? "?" + string.Join("&", q) : ""));
            Grid.ItemsSource = list;
            Grid.SelectedItem = list.FirstOrDefault(p => p.Id == selected);
            var low = list.Count(p => p.BelowMinimum && p.IsActive);
            Footer.Text = $"Позиций: {list.Count}" + (low > 0 ? $" · ниже минимального остатка: {low} (выделены)" : "");
        }
        catch (ApiException ex)
        {
            Footer.Text = ex.Message;
        }
    }

    private void Search_Changed(object sender, TextChangedEventArgs e)
    {
        SearchHint.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        _delay.Stop();
        _delay.Start();
    }

    private async void Filter_Changed(object sender, RoutedEventArgs e) => await LoadAsync();

    private async void New_Click(object sender, RoutedEventArgs e) => await EditAsync(null);

    private async void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is PartDto p) await EditAsync(p);
        else if (e.RoutedEvent != null && sender is Button) Dialogs.Info("Выберите позицию в таблице");
    }

    private async Task EditAsync(PartDto p)
    {
        var dlg = new FormDialog(p == null ? "Новая позиция" : "Карточка запчасти")
            .Text("sku", "Артикул *", p?.Sku)
            .Text("name", "Наименование *", p?.Name)
            .Text("unit", "Единица измерения", p?.Unit ?? "шт")
            .Number("price", "Цена для клиента, ₽ *", p?.Price)
            .Number("min", "Неснижаемый остаток", p?.MinQuantity ?? 0)
            .Text("location", "Ячейка хранения", p?.Location);
        if (p != null) dlg.Check("active", "Позиция используется", p.IsActive);
        dlg.Validate = f =>
        {
            if (f.GetText("sku") == "" || f.GetText("name") == "") return "Укажите артикул и наименование";
            if (f.GetDecimal("price") is null or < 0) return "Некорректная цена";
            if (f.GetInt("min") is null or < 0) return "Неснижаемый остаток — целое число не меньше 0";
            return null;
        };
        if (dlg.ShowDialog() != true) return;
        var req = new PartEditRequest
        {
            Sku = dlg.GetText("sku"), Name = dlg.GetText("name"), Unit = dlg.GetText("unit"), Price = dlg.GetDecimal("price").Value,
            MinQuantity = dlg.GetInt("min").Value, Location = dlg.GetText("location"), IsActive = p == null || dlg.GetCheck("active"),
        };
        try
        {
            if (p == null) await ApiClient.PostAsync<PartDto>("parts", req);
            else await ApiClient.PutAsync<PartDto>($"parts/{p.Id}", req);
            await LoadAsync();
        }
        catch (ApiException ex)
        {
            Dialogs.Error(ex.Message);
        }
    }

    private async void Receipt_Click(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not PartDto p)
        {
            Dialogs.Info("Выберите позицию в таблице");
            return;
        }
        var dlg = new FormDialog("Оприходование", "Оприходовать")
            .Note($"{p.Sku} · {p.Name}\nСейчас на складе: {p.OnHand} {p.Unit}, в резерве: {p.Reserved}")
            .Number("qty", "Количество поступившее *")
            .Text("comment", "Документ поставки (накладная, поставщик)");
        dlg.Validate = f => f.GetInt("qty") is null or < 1 ? "Количество — целое число больше нуля" : null;
        if (dlg.ShowDialog() != true) return;
        try
        {
            var updated = await ApiClient.PostAsync<PartDto>($"parts/{p.Id}/receipt", new ReceiptRequest { Quantity = dlg.GetInt("qty").Value, Comment = dlg.GetText("comment") });
            await LoadAsync();
            if (updated.Reserved > p.Reserved)
                Dialogs.Info($"Поступившие детали автоматически зарезервированы под ожидающие запросы мастеров ({updated.Reserved - p.Reserved} {p.Unit}).");
        }
        catch (ApiException ex)
        {
            Dialogs.Error(ex.Message);
        }
    }
}
