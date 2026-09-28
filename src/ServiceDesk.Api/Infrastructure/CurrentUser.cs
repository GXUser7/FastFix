using System.Security.Claims;

namespace ServiceDesk.Api.Infrastructure;

// Текущий пользователь из JWT
public record CurrentUser(int Id, string Role)
{
    public static CurrentUser From(ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var role = principal.FindFirstValue(ClaimTypes.Role);
        if (id == null || role == null) throw BusinessException.Unauthorized("Требуется вход в систему");
        return new CurrentUser(int.Parse(id), role);
    }
}
