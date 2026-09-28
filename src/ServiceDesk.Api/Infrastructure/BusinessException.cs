namespace ServiceDesk.Api.Infrastructure;

// Нарушение бизнес-правила: превращается в HTTP-ответ с понятным сообщением
public class BusinessException : Exception
{
    public int StatusCode { get; }

    public BusinessException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }

    public static BusinessException BadRequest(string message) => new(StatusCodes.Status400BadRequest, message);
    public static BusinessException Unauthorized(string message) => new(StatusCodes.Status401Unauthorized, message);
    public static BusinessException Forbidden(string message = "Недостаточно прав для выполнения операции") => new(StatusCodes.Status403Forbidden, message);
    public static BusinessException NotFound(string message) => new(StatusCodes.Status404NotFound, message);
    public static BusinessException Conflict(string message) => new(StatusCodes.Status409Conflict, message);
    public static BusinessException Locked(string message) => new(StatusCodes.Status423Locked, message);
}
