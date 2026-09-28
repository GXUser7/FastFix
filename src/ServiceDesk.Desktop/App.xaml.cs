using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using ServiceDesk.Desktop.Services;
using ServiceDesk.Desktop.Views;

namespace ServiceDesk.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnError;
        ApiClient.Init(ReadApiUrl());
        ShowLogin();
    }

    // Адрес сервера из appsettings.json рядом с exe (по умолчанию http://localhost:5080)
    private static string ReadApiUrl()
    {
        try
        {
            var file = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            return doc.RootElement.GetProperty("ApiUrl").GetString();
        }
        catch
        {
            return "http://localhost:5080";
        }
    }

    public static void ShowLogin()
    {
        var login = new LoginWindow();
        login.Show();
    }

    private void OnError(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        Dialogs.Error(e.Exception is ApiException ? e.Exception.Message : "Непредвиденная ошибка: " + e.Exception.Message);
    }
}
