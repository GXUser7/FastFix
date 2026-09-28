using System.Windows;
using ServiceDesk.Contracts;
using ServiceDesk.Desktop.Services;

namespace ServiceDesk.Desktop.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        ServerText.Text = "Сервер: " + ApiClient.BaseUrl;
        Loaded += (_, _) => LoginBox.Focus();
        Closed += (_, _) =>
        {
            if (Application.Current.Windows.OfType<MainWindow>().Any() == false) Application.Current.Shutdown();
        };
    }

    private async void Login_Click(object sender, RoutedEventArgs e)
    {
        ErrorBox.Visibility = Visibility.Collapsed;
        if (string.IsNullOrWhiteSpace(LoginBox.Text) || string.IsNullOrEmpty(PasswordBox.Password))
        {
            ShowError("Введите телефон или email и пароль");
            return;
        }
        LoginButton.IsEnabled = false;
        LoginButton.Content = "Вход…";
        try
        {
            var auth = await ApiClient.PostAsync<AuthResponse>("auth/login", new LoginRequest { Login = LoginBox.Text.Trim(), Password = PasswordBox.Password });
            if (auth.User.Role == Roles.Client)
            {
                ShowError("Клиенты работают в веб-кабинете. Настольное приложение предназначено для сотрудников");
                return;
            }
            await Session.StartAsync(auth);
            var main = new MainWindow();
            Application.Current.MainWindow = main;
            main.Show();
            Close();
        }
        catch (ApiException ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            LoginButton.IsEnabled = true;
            LoginButton.Content = "Войти";
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorBox.Visibility = Visibility.Visible;
    }
}
