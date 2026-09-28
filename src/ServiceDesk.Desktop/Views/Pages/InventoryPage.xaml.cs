using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using ServiceDesk.Contracts;
using ServiceDesk.Desktop.Services;

namespace ServiceDesk.Desktop.Views.Pages;

// Ввод фактических остатков и проведение инвентаризации
public partial class InventoryPage : Page
{
    public class Line : INotifyPropertyChanged
    {
        private int? _actual;
        public int PartId { get; init; }
        public string Sku { get; init; }
        public string Name { get; init; }
        public string Location { get; init; }
        public int Expected { get; init; }
        public int Reserved { get; init; }

        public int? Actual
        {
            get => _actual;
            set { _actual = value; Changed(); Changed(nameof(DifferenceText)); }
        }

        public string DifferenceText => Actual == null ? "" : (Actual - Expected) switch { 0 => "0", > 0 and var d => "+" + d, var d => d.ToString() };

        public event PropertyChangedEventHandler PropertyChanged;
        private void Changed([CallerMemberName] string p = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    private List<Line> _lines = new();

    public InventoryPage()
    {
        InitializeComponent();
        CommentBox.TextChanged += (_, _) => CommentHint.Visibility = CommentBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var parts = await ApiClient.GetAsync<List<PartDto>>("parts");
            _lines = parts.Select(p => new Line { PartId = p.Id, Sku = p.Sku, Name = p.Name, Location = p.Location, Expected = p.OnHand, Reserved = p.Reserved })
                .OrderBy(l => l.Location).ThenBy(l => l.Name).ToList();
            Grid.ItemsSource = _lines;
            HistoryList.ItemsSource = await ApiClient.GetAsync<List<InventoryDto>>("inventories");
        }
        catch (ApiException ex)
        {
            Dialogs.Error(ex.Message);
        }
    }

    private void Grid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditingElement is TextBox tb && tb.Text.Trim() != "" && (!int.TryParse(tb.Text.Trim(), out var v) || v < 0))
        {
            Dialogs.Error("Фактический остаток — целое неотрицательное число");
            tb.Text = "";
        }
    }

    private void FillExpected_Click(object sender, RoutedEventArgs e)
    {
        foreach (var l in _lines.Where(l => l.Actual == null)) l.Actual = l.Expected;
    }

    private async void Post_Click(object sender, RoutedEventArgs e)
    {
        Grid.CommitEdit(DataGridEditingUnit.Row, true);
        var filled = _lines.Where(l => l.Actual != null).ToList();
        if (filled.Count == 0)
        {
            Dialogs.Info("Введите фактический остаток хотя бы по одной позиции");
            return;
        }
        var diffs = filled.Count(l => l.Actual != l.Expected);
        if (!Dialogs.Confirm($"Провести инвентаризацию по {filled.Count} поз.?\nПозиций с расхождением: {diffs}. Учётные остатки будут скорректированы.")) return;
        try
        {
            var inv = await ApiClient.PostAsync<InventoryDto>("inventories", new InventoryRequest
            {
                Comment = CommentBox.Text.Trim(),
                Items = filled.Select(l => new InventoryLine { PartId = l.PartId, ActualQuantity = l.Actual.Value }).ToList(),
            });
            CommentBox.Text = "";
            Dialogs.Info($"Инвентаризация № {inv.Id} проведена. Позиций с расхождением: {inv.Discrepancies}.");
            await LoadAsync();
        }
        catch (ApiException ex)
        {
            Dialogs.Error(ex.Message);
        }
    }

    private async void History_Selected(object sender, SelectionChangedEventArgs e)
    {
        if (HistoryList.SelectedItem is not InventoryDto i) return;
        try
        {
            var full = await ApiClient.GetAsync<InventoryDto>($"inventories/{i.Id}");
            var diff = full.Items.Where(x => x.Difference != 0).ToList();
            DetailsText.Text = $"Ведомость № {full.Id}: {full.Items.Count} поз.\n" +
                               (diff.Count == 0 ? "Расхождений нет" : string.Join("\n", diff.Select(x => $"{x.Sku}: учёт {x.Expected}, факт {x.Actual} ({(x.Difference > 0 ? "+" : "")}{x.Difference})")));
        }
        catch (ApiException ex)
        {
            DetailsText.Text = ex.Message;
        }
    }
}
