using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ServiceDesk.Contracts;
using ServiceDesk.Desktop.Services;

namespace ServiceDesk.Desktop.Views.Pages;

// Оформление заявки: клиент, устройство, неисправность, комплектность, мастер, срок
public partial class NewOrderPage : Page
{
    private ClientDto _client;
    private string _checkedPhone;

    public NewOrderPage()
    {
        InitializeComponent();
        var d = Session.Dictionaries;
        TypeBox.ItemsSource = d.DeviceTypes;
        var masters = new List<IdName> { new() { Id = 0, Name = "Не назначать" } };
        masters.AddRange(d.Masters);
        MasterBox.ItemsSource = masters;
        foreach (var item in d.Completeness)
        {
            var cb = new CheckBox { Content = item.Name, Tag = item.Id };
            (item.Kind == "accessory" ? AccessoriesPanel : AppearancePanel).Children.Add(cb);
        }
        Reset();
        Loaded += (_, _) => PhoneBox.Focus();
    }

    private void Reset()
    {
        _client = null;
        _checkedPhone = null;
        PhoneBox.Text = LastNameBox.Text = FirstNameBox.Text = MiddleNameBox.Text = "";
        BrandBox.Text = ModelBox.Text = SerialBox.Text = FaultBox.Text = AppearanceBox.Text = "";
        TypeBox.SelectedIndex = -1;
        MasterBox.SelectedIndex = 0;
        DueDateBox.SelectedDate = DateTime.Today.AddDays(7);
        DueDateBox.DisplayDateStart = DateTime.Today;
        WarrantyBox.IsChecked = false;
        foreach (var cb in AccessoriesPanel.Children.OfType<CheckBox>().Concat(AppearancePanel.Children.OfType<CheckBox>())) cb.IsChecked = false;
        ClientFound.Visibility = NewClientPanel.Visibility = Visibility.Collapsed;
        ErrorText.Text = "";
    }

    private async Task FindClientAsync()
    {
        var phone = PhoneBox.Text.Trim();
        if (phone.Count(char.IsDigit) < 10 || phone == _checkedPhone) return;
        _checkedPhone = phone;
        ErrorText.Text = "";
        try
        {
            _client = await ApiClient.GetAsync<ClientDto>("clients?phone=" + Uri.EscapeDataString(phone));
            ClientName.Text = _client.FullName;
            ClientInfo.Text = (_client.Email ?? "email не указан") + (_client.IsRegistered ? " · зарегистрирован в личном кабинете" : " · личный кабинет не зарегистрирован");
            ClientFound.Visibility = Visibility.Visible;
            NewClientPanel.Visibility = Visibility.Collapsed;
            TypeBox.Focus();
        }
        catch (ApiException ex) when (ex.Status == HttpStatusCode.NotFound)
        {
            _client = null;
            ClientFound.Visibility = Visibility.Collapsed;
            NewClientPanel.Visibility = Visibility.Visible;
            LastNameBox.Focus();
        }
        catch (ApiException ex)
        {
            _checkedPhone = null;
            ErrorText.Text = ex.Message;
        }
    }

    private async void FindClient_Click(object sender, RoutedEventArgs e)
    {
        _checkedPhone = null;
        await FindClientAsync();
    }

    private async void Phone_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) await FindClientAsync();
    }

    // телефон изменён после поиска — найденный клиент больше не актуален
    private void Phone_Changed(object sender, TextChangedEventArgs e)
    {
        if (_checkedPhone == null || PhoneBox.Text.Trim() == _checkedPhone) return;
        _checkedPhone = null;
        _client = null;
        ClientFound.Visibility = NewClientPanel.Visibility = Visibility.Collapsed;
    }

    private async void Phone_LostFocus(object sender, RoutedEventArgs e) => await FindClientAsync();

    private string Validate()
    {
        if (PhoneBox.Text.Count(char.IsDigit) < 10) return "Укажите телефон клиента";
        if (_client == null && NewClientPanel.Visibility != Visibility.Visible) return "Нажмите «Найти», чтобы проверить клиента";
        if (_client == null && (string.IsNullOrWhiteSpace(LastNameBox.Text) || string.IsNullOrWhiteSpace(FirstNameBox.Text))) return "Укажите фамилию и имя нового клиента";
        if (TypeBox.SelectedItem == null) return "Выберите тип устройства";
        if (string.IsNullOrWhiteSpace(BrandBox.Text) || string.IsNullOrWhiteSpace(ModelBox.Text)) return "Укажите производителя и модель";
        if (string.IsNullOrWhiteSpace(FaultBox.Text)) return "Опишите заявленную неисправность";
        if (DueDateBox.SelectedDate == null || DueDateBox.SelectedDate < DateTime.Today) return "Укажите плановый срок готовности не ранее сегодняшнего дня";
        return null;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = Validate() ?? "";
        if (ErrorText.Text != "") return;

        var master = (IdName)MasterBox.SelectedItem;
        var req = new CreateOrderRequest
        {
            ClientId = _client?.Id,
            ClientPhone = PhoneBox.Text.Trim(),
            ClientLastName = LastNameBox.Text.Trim(),
            ClientFirstName = FirstNameBox.Text.Trim(),
            ClientMiddleName = MiddleNameBox.Text.Trim(),
            DeviceTypeId = ((IdName)TypeBox.SelectedItem).Id,
            Brand = BrandBox.Text.Trim(),
            Model = ModelBox.Text.Trim(),
            SerialNumber = SerialBox.Text.Trim(),
            DeclaredFault = FaultBox.Text.Trim(),
            AppearanceNote = AppearanceBox.Text.Trim(),
            CompletenessIds = AccessoriesPanel.Children.OfType<CheckBox>().Concat(AppearancePanel.Children.OfType<CheckBox>())
                .Where(c => c.IsChecked == true).Select(c => (int)c.Tag).ToList(),
            MasterId = master == null || master.Id == 0 ? null : master.Id,
            DueDate = DueDateBox.SelectedDate,
            IsWarranty = WarrantyBox.IsChecked == true,
        };

        SaveButton.IsEnabled = false;
        try
        {
            var created = await ApiClient.PostAsync<CreateOrderResponse>("orders", req);
            try
            {
                await DocumentOpener.OpenAsync(created.Id, DocumentTypes.Receipt);
            }
            catch (Exception ex)
            {
                Dialogs.Error("Заявка оформлена, но квитанцию открыть не удалось: " + ex.Message);
            }
            Reset();
            MainWindow.OpenOrder(created.Id);
        }
        catch (ApiException ex)
        {
            ErrorText.Text = ex.Message;
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        Reset();
        PhoneBox.Focus();
    }
}
