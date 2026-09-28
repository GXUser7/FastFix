using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ServiceDesk.Contracts;

namespace ServiceDesk.Desktop.Services;

public class ApiException : Exception
{
    public HttpStatusCode Status { get; }
    public ApiException(HttpStatusCode status, string message) : base(message) => Status = status;
}

// Обёртка над HttpClient: JSON, JWT, разбор ошибок API
public static class ApiClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static HttpClient _http;

    public static string BaseUrl { get; private set; }

    // Вызывается при ответе 401 — главное окно возвращает пользователя к входу
    public static event Action SessionExpired;

    public static void Init(string baseUrl)
    {
        BaseUrl = baseUrl.TrimEnd('/');
        _http = new HttpClient { BaseAddress = new Uri(BaseUrl + "/"), Timeout = TimeSpan.FromSeconds(30) };
    }

    public static Task<T> GetAsync<T>(string url) => SendAsync<T>(HttpMethod.Get, url, null);
    public static Task<T> PostAsync<T>(string url, object body) => SendAsync<T>(HttpMethod.Post, url, body);
    public static Task PostAsync(string url, object body = null) => SendAsync<object>(HttpMethod.Post, url, body ?? new { });
    public static Task<T> PutAsync<T>(string url, object body) => SendAsync<T>(HttpMethod.Put, url, body);
    public static Task PutAsync(string url, object body) => SendAsync<object>(HttpMethod.Put, url, body);
    public static Task DeleteAsync(string url) => SendAsync<object>(HttpMethod.Delete, url, null);

    public static async Task<byte[]> GetBytesAsync(string url)
    {
        using var res = await RawAsync(HttpMethod.Get, url, null);
        return await res.Content.ReadAsByteArrayAsync();
    }

    private static async Task<T> SendAsync<T>(HttpMethod method, string url, object body)
    {
        using var res = await RawAsync(method, url, body);
        if (res.StatusCode == HttpStatusCode.NoContent || typeof(T) == typeof(object) || res.Content.Headers.ContentLength == 0)
            return default;
        return await res.Content.ReadFromJsonAsync<T>(Json);
    }

    private static async Task<HttpResponseMessage> RawAsync(HttpMethod method, string url, object body)
    {
        var req = new HttpRequestMessage(method, "api/" + url.TrimStart('/'));
        if (Session.Token != null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Session.Token);
        if (body != null) req.Content = JsonContent.Create(body, body.GetType(), options: Json);

        HttpResponseMessage res;
        try
        {
            res = await _http.SendAsync(req);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            throw new ApiException(0, $"Сервер недоступен ({BaseUrl}). Проверьте подключение и настройку ApiUrl в appsettings.json");
        }

        if (res.IsSuccessStatusCode) return res;

        var message = $"Ошибка сервера ({(int)res.StatusCode})";
        try
        {
            var err = await res.Content.ReadFromJsonAsync<ApiError>(Json);
            if (!string.IsNullOrWhiteSpace(err?.Message)) message = err.Message;
        }
        catch { /* тело ответа не JSON */ }
        var status = res.StatusCode;
        res.Dispose();
        if (status == HttpStatusCode.Unauthorized && Session.Token != null) SessionExpired?.Invoke();
        throw new ApiException(status, message);
    }
}
