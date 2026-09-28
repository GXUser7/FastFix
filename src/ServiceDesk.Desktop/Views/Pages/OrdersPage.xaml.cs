using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ServiceDesk.Contracts;
using ServiceDesk.Desktop.Controls;
using ServiceDesk.Desktop.Services;

namespace ServiceDesk.Desktop.Views.Pages;

// Список заявок: поиск по номеру и телефону, фильтр по статусу, выделение просрочки
public partial class OrdersPage : Page
{
    public class Row
    {
        public OrderListItemDto Dto { get; init; }
        public int Id => Dto.Id;
        public string ClientName => Dto.ClientName;
        public string ClientPhone => Dto.ClientPhone;
        public string DeviceTitle => $"{Dto.DeviceType} {Dto.Device}";
        public string MasterName => Dto.MasterName ?? "не назначен";
        public DateTime CreatedAt => Dto.CreatedAt;
        public DateTime DueDate => Dto.DueDate;
        public bool IsOverdue => Dto.IsOverdue;
        public string StatusName => Dto.StatusName;
        public string StatusColor => Dto.StatusColor;
        public string Sum => Dto.IsWarranty ? "гарантия" : Dto.TotalCost > 0 ? Fmt.Money(Dto.TotalCost) : "—";
    }

    private record StatusItem(string Code, string Name);

    private readonly DispatcherTimer _searchDelay = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private bool _ready;

    public OrdersPage(string status = null)
    {
        InitializeComponent();
        var items = new List<StatusItem> { new(null, "Статус: все"), new("active", "Все активные") };
        items.AddRange(Session.Dictionaries.Statuses.Select(s => new StatusItem(s.Code, s.Name)));
        StatusBox.ItemsSource = items;
        StatusBox.SelectedItem = items.FirstOrDefault(i => i.Code == (status ?? (Session.IsMaster ? "active" : null)));

        NewButton.Visibility = Session.IsReceptionist ? Visibility.Visible : Visibility.Collapsed;
        Header.Text = Session.IsMaster ? "Мои заявки" : status == OrderStatuses.Ready ? "Готовы к выдаче" : "Заявки";

        _searchDelay.Tick += async (_, _) => { _searchDelay.Stop(); await LoadAsync(); };
        Loaded += async (_, _) =>
        {
            RealtimeClient.OrderUpdated += OnOrderUpdated;
            _ready = true;
            await LoadAsync();
            SearchBox.Focus();
        };
        Unloaded += (_, _) => RealtimeClient.OrderUpdated -= OnOrderUpdated;
    }

    private async void OnOrderUpdated(OrderUpdatedEvent e) => await LoadAsync();

    private async Task LoadAsync()
    {
        if (!_ready) return;
        var q = new List<string>();
        var status = (StatusBox.SelectedItem as StatusItem)?.Code;
        if (status == "active") q.Add("active=true");
        else if (status != null) q.Add("status=" + status);
        if (OverdueBox.IsChecked == true) q.Add("overdue=true");
        if (!string.IsNullOrWhiteSpace(SearchBox.Text)) q.Add("search=" + Uri.EscapeDataString(SearchBox.Text.Trim()));
        try
        {
            var selected = (Grid.SelectedItem as Row)?.Id;
            var list = await ApiClient.GetAsync<List<OrderListItemDto>>("orders" + (q.Count > 0 ? "?" + string.Join("&", q) : ""));
            var rows = list.Select(d => new Row { Dto = d }).ToList();
            Grid.ItemsSource = rows;
            Grid.SelectedItem = rows.FirstOrDefault(r => r.Id == selected);
            var overdue = rows.Count(r => r.IsOverdue);
            Footer.Text = $"Найдено заявок: {rows.Count}" + (overdue > 0 ? $" · просрочено: {overdue}" : "") + " · двойной щелчок или Enter — открыть карточку";
        }
        catch (ApiException ex)
        {
            Footer.Text = ex.Message;
        }
    }

    private void Search_Changed(object sender, TextChangedEventArgs e)
    {
        SearchHint.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        _searchDelay.Stop();
        _searchDelay.Start();
    }

    private async void Filter_Changed(object sender, RoutedEventArgs e) => await LoadAsync();
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadAsync();
    private void New_Click(object sender, RoutedEventArgs e) => MainWindow.SelectMenu(1);

    private void Grid_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Grid.SelectedItem is Row r) MainWindow.OpenOrder(r.Id);
    }

    private void Grid_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Grid.SelectedItem is Row r)
        {
            e.Handled = true;
            MainWindow.OpenOrder(r.Id);
        }
    }
}
