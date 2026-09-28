using System.Windows;
using System.Windows.Controls;
using ServiceDesk.Contracts;
using ServiceDesk.Desktop.Services;

namespace ServiceDesk.Desktop.Views.Pages;

// Запросы мастеров и выдача деталей под заявку
public partial class PartRequestsPage : Page
{
    private record Filter(string Code, string Name);

    public PartRequestsPage()
    {
        InitializeComponent();
        var filters = new List<Filter>
        {
            new("active", "Ожидают выдачи и поступления"), new(ReservationStatuses.Reserved, "Зарезервированы (к выдаче)"),
            new(ReservationStatuses.Requested, "Ожидают поступления"), new(ReservationStatuses.Issued, "Выданные"), new("all", "Все запросы"),
        };
        FilterBox.ItemsSource = filters;
        FilterBox.SelectedIndex = 0;
        Loaded += async (_, _) =>
        {
            RealtimeClient.OrderUpdated += OnOrderUpdated;
            await LoadAsync();
        };
        Unloaded += (_, _) => RealtimeClient.OrderUpdated -= OnOrderUpdated;
    }

    private async void OnOrderUpdated(OrderUpdatedEvent e) => await LoadAsync();

    private async Task LoadAsync()
    {
        if (!IsLoaded) return;
        try
        {
            var code = (FilterBox.SelectedItem as Filter)?.Code ?? "active";
            var list = await ApiClient.GetAsync<List<ReservationDto>>("parts/requests?status=" + code);
            Grid.ItemsSource = list;
            Footer.Text = $"Запросов: {list.Count} · к выдаче: {list.Count(r => r.Status == ReservationStatuses.Reserved)} · ожидают поступления: {list.Count(r => r.Status == ReservationStatuses.Requested)}";
        }
        catch (ApiException ex)
        {
            Footer.Text = ex.Message;
        }
    }

    private async void Filter_Changed(object sender, SelectionChangedEventArgs e) => await LoadAsync();
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadAsync();

    private async void Issue_Click(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not ReservationDto r)
        {
            Dialogs.Info("Выберите запрос в таблице");
            return;
        }
        if (r.Status == ReservationStatuses.Requested)
        {
            Dialogs.Info("Запчасти нет на складе. Оприходуйте поступление на странице «Остатки» — резерв создастся автоматически.");
            return;
        }
        if (r.Status != ReservationStatuses.Reserved) return;
        if (!Dialogs.Confirm($"Выдать мастеру {r.RequestedBy}:\n{r.PartName} — {r.Quantity} шт. (ячейка {r.Location})\nпод заявку № {r.OrderId}?")) return;
        try
        {
            await ApiClient.PostAsync($"parts/requests/{r.Id}/issue");
            await LoadAsync();
        }
        catch (ApiException ex)
        {
            Dialogs.Error(ex.Message);
        }
    }
}
