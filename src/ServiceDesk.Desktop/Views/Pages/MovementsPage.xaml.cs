using System.Windows;
using System.Windows.Controls;
using ServiceDesk.Contracts;
using ServiceDesk.Desktop.Services;

namespace ServiceDesk.Desktop.Views.Pages;

// Журнал движения запчастей с отбором по позиции, типу операции и периоду
public partial class MovementsPage : Page
{
    private record Item(string Key, string Name);

    private bool _ready;

    public MovementsPage()
    {
        InitializeComponent();
        var types = new List<Item> { new(null, "Все операции") };
        types.AddRange(new[] { MovementTypes.Receipt, MovementTypes.Reserve, MovementTypes.Unreserve, MovementTypes.Issue, MovementTypes.Inventory }
            .Select(t => new Item(t, MovementTypes.Title(t))));
        TypeBox.ItemsSource = types;
        TypeBox.SelectedIndex = 0;
        Loaded += async (_, _) =>
        {
            try
            {
                var parts = await ApiClient.GetAsync<List<PartDto>>("parts?includeInactive=true");
                var items = new List<Item> { new(null, "Все позиции") };
                items.AddRange(parts.Select(p => new Item(p.Id.ToString(), $"{p.Sku} · {p.Name}")));
                PartBox.ItemsSource = items;
                PartBox.SelectedIndex = 0;
            }
            catch (ApiException ex)
            {
                Footer.Text = ex.Message;
            }
            _ready = true;
            await LoadAsync();
        };
    }

    private async Task LoadAsync()
    {
        if (!_ready) return;
        var q = new List<string>();
        if ((PartBox.SelectedItem as Item)?.Key is { } part) q.Add("partId=" + part);
        if ((TypeBox.SelectedItem as Item)?.Key is { } type) q.Add("type=" + type);
        if (FromBox.SelectedDate is { } from) q.Add("from=" + from.ToString("yyyy-MM-dd"));
        if (ToBox.SelectedDate is { } to) q.Add("to=" + to.ToString("yyyy-MM-dd"));
        try
        {
            var list = await ApiClient.GetAsync<List<MovementDto>>("parts/movements" + (q.Count > 0 ? "?" + string.Join("&", q) : ""));
            Grid.ItemsSource = list;
            Footer.Text = $"Операций: {list.Count} · приход: {list.Where(m => m.Quantity > 0).Sum(m => m.Quantity)} · расход: {-list.Where(m => m.Quantity < 0).Sum(m => m.Quantity)}";
        }
        catch (ApiException ex)
        {
            Footer.Text = ex.Message;
        }
    }

    private async void Filter_Changed(object sender, EventArgs e) => await LoadAsync();

    private async void Reset_Click(object sender, RoutedEventArgs e)
    {
        _ready = false;
        PartBox.SelectedIndex = 0;
        TypeBox.SelectedIndex = 0;
        FromBox.SelectedDate = ToBox.SelectedDate = null;
        _ready = true;
        await LoadAsync();
    }
}
