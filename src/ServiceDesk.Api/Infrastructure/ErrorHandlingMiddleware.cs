using Microsoft.EntityFrameworkCore;
using ServiceDesk.Contracts;

namespace ServiceDesk.Api.Infrastructure;

// Единый формат ошибок API: { "message": "..." }
public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _log;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> log)
    {
        _next = next;
        _log = log;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (BusinessException ex)
        {
            await Write(context, ex.StatusCode, ex.Message);
        }
        catch (DbUpdateException ex)
        {
            // нарушение UNIQUE/CHECK/FK на уровне СУБД
            _log.LogWarning(ex, "Ошибка сохранения в БД");
            var text = ex.InnerException?.Message ?? ex.Message;
            var message = text.Contains("Duplicate", StringComparison.OrdinalIgnoreCase)
                ? "Запись с такими данными уже существует"
                : text.Contains("ck_parts", StringComparison.OrdinalIgnoreCase)
                    ? "Недостаточно запчастей на складе"
                    : "Операция нарушает ограничения целостности данных";
            await Write(context, StatusCodes.Status409Conflict, message);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Необработанная ошибка");
            await Write(context, StatusCodes.Status500InternalServerError, "Внутренняя ошибка сервера");
        }
    }

    private static Task Write(HttpContext context, int status, string message)
    {
        if (context.Response.HasStarted) return Task.CompletedTask;
        context.Response.Clear();
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new ApiError { Message = message });
    }
}
