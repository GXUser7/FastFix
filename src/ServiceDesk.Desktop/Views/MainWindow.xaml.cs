using System.Windows;
using System.Windows.Controls;
using ServiceDesk.Desktop.Services;
using ServiceDesk.Desktop.Views.Pages;

namespace ServiceDesk.Desktop.Views;

public partial class MainWindow : Window
{
    private bool _loggingOut;

    public MainWindow()
    {
        InitializeComponent();
        UserName.Text = Session.User.ShortName;
        UserRole.Text = Session.User.RoleName;
        Title = $"СервисДеск — {Session.User.RoleName}";
        BuildMenu();
        ApiClient.SessionExpired += OnSessionExpired;
        Loaded += async (_, _) => await RealtimeClient.StartAsync();
        Closed += async (_, _) =>
        {
            ApiClient.SessionExpired -= OnSessionExpired;
            await RealtimeClient.StopAsync();
            if (!_loggingOut) Application.Current.Shutdown();
        };
    }

    private void BuildMenu()
    {
        if (Session.IsReceptionist)
        {
            AddItem("Заявки", () => new OrdersPage(), true);
            AddItem("Новая заявка", () => new NewOrderPage());
            AddItem("Готовы к выдаче", () => new OrdersPage(Contracts.OrderStatuses.Ready));
        }
        else if (Session.IsMaster)
        {
            AddItem("Мои заявки", () => new OrdersPage(), true);
        }
        else if (Session.IsStorekeeper)
        {
            AddItem("Остатки", () => new PartsPage(), true);
            AddItem("Запросы мастеров", () => new PartRequestsPage());
            AddItem("Инвентаризация", () => new InventoryPage());
            AddItem("Движение запчастей", () => new MovementsPage());
        }
    }

    private void AddItem(string title, Func<Page> factory, bool open = false)
    {
        var rb = new RadioButton { Content = title, GroupName = "menu", Style = (Style)FindResource("NavButton") };
        rb.Checked += (_, _) => Navigate(factory());
        Menu.Children.Add(rb);
        if (open) Loaded += (_, _) => rb.IsChecked = true;
    }

    public void Navigate(Page page)
    {
        PageFrame.Navigate(page);
        // очищаем журнал навигации, чтобы страницы не накапливались
        while (PageFrame.CanGoBack) PageFrame.RemoveBackEntry();
    }

    public static MainWindow Current => (MainWindow)Application.Current.MainWindow;

    // Открыть карточку заявки из любой страницы
    public static void OpenOrder(int id) => Current.PageFrame.Navigate(new OrderCardPage(id));

    public static void GoBack()
    {
        if (Current.PageFrame.CanGoBack) Current.PageFrame.GoBack();
    }

    public static void SelectMenu(int index)
    {
        if (Current.Menu.Children[index] is RadioButton rb)
        {
            if (rb.IsChecked == true) rb.RaiseEvent(new RoutedEventArgs(RadioButton.CheckedEvent));
            else rb.IsChecked = true;
        }
    }

    private void OnSessionExpired() => Dispatcher.BeginInvoke(() =>
    {
        if (_loggingOut) return;
        Dialogs.Error("Сеанс истёк. Войдите снова.");
        DoLogout();
    });

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        if (Dialogs.Confirm("Выйти из учётной записи?")) DoLogout();
    }

    private void DoLogout()
    {
        _loggingOut = true;
        Session.Clear();
        App.ShowLogin();
        Close();
    }
}
