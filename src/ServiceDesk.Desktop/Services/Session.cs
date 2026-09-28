using System.Diagnostics;
using System.IO;
using ServiceDesk.Contracts;

namespace ServiceDesk.Desktop.Services;

// Токен и данные текущего пользователя; справочники загружаются один раз после входа
public static class Session
{
    public static string Token { get; private set; }
    public static UserDto User { get; private set; }
    public static DictionariesDto Dictionaries { get; private set; }

    public static bool IsReceptionist => User?.Role == Roles.Receptionist;
    public static bool IsMaster => User?.Role == Roles.Master;
    public static bool IsStorekeeper => User?.Role == Roles.Storekeeper;

    public static async Task StartAsync(AuthResponse auth)
    {
        Token = auth.Token;
        User = auth.User;
        Dictionaries = await ApiClient.GetAsync<DictionariesDto>("dictionaries");
    }

    public static void Clear()
    {
        Token = null;
        User = null;
        Dictionaries = null;
    }

    public static StatusDto Status(string code) => Dictionaries?.Statuses.FirstOrDefault(s => s.Code == code);
}

// Сохранение PDF во временную папку и открытие в программе просмотра (для печати)
public static class DocumentOpener
{
    public static async Task OpenAsync(int orderId, string type)
    {
        var bytes = await ApiClient.GetBytesAsync($"orders/{orderId}/documents/{type}");
        var dir = Path.Combine(Path.GetTempPath(), "ServiceDesk");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, $"{DocumentTypes.Prefix(type)}-{orderId}_{DateTime.Now:HHmmss}.pdf");
        await File.WriteAllBytesAsync(file, bytes);
        Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
    }
}
