using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Infrastructure;
using ServiceDesk.Contracts;

namespace ServiceDesk.Api.Services;

public class JwtOptions
{
    public string Key { get; set; }
    public string Issuer { get; set; } = "ServiceDesk";
    public string Audience { get; set; } = "ServiceDesk";
    public int LifetimeHours { get; set; } = 12;
}

// Формирование JWT с идентификатором и ролью пользователя
public class TokenService
{
    private readonly JwtOptions _opt;

    public TokenService(JwtOptions opt) => _opt = opt;

    public (string Token, DateTime ExpiresAt) Create(User user)
    {
        var expires = DateTime.UtcNow.AddHours(_opt.LifetimeHours);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Role, user.Role.Code),
            new Claim(ClaimTypes.Name, user.ShortName),
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Key));
        var token = new JwtSecurityToken(_opt.Issuer, _opt.Audience, claims, expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}

// Правило блокировки: 5 неудачных попыток подряд → вход запрещён на 15 минут
public static class LoginLock
{
    public const int MaxAttempts = 5;
    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(15);

    public static bool IsLocked(User u, DateTime now) => u.LockedUntil.HasValue && u.LockedUntil.Value > now;

    public static void RegisterFailure(User u, DateTime now)
    {
        // истёкшая блокировка — счёт попыток начинается заново
        if (u.LockedUntil.HasValue && u.LockedUntil.Value <= now)
        {
            u.LockedUntil = null;
            u.FailedLoginCount = 0;
        }
        u.FailedLoginCount++;
        if (u.FailedLoginCount >= MaxAttempts)
        {
            u.LockedUntil = now.Add(Duration);
            u.FailedLoginCount = 0;
        }
    }

    public static void RegisterSuccess(User u)
    {
        u.FailedLoginCount = 0;
        u.LockedUntil = null;
    }
}

public class AuthService
{
    private readonly AppDbContext _db;
    private readonly TokenService _tokens;

    public AuthService(AppDbContext db, TokenService tokens)
    {
        _db = db;
        _tokens = tokens;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest req)
    {
        var login = (req.Login ?? "").Trim();
        var phone = Phone.Normalize(login);
        var user = await _db.Users.Include(u => u.Role)
            .FirstOrDefaultAsync(u => (phone != null && u.Phone == phone) || u.Email == login.ToLower());

        var now = DateTime.Now;
        if (user == null || user.PasswordHash == null || !user.IsActive)
            throw BusinessException.Unauthorized("Неверный логин или пароль");

        if (LoginLock.IsLocked(user, now))
            throw BusinessException.Locked(LockMessage(user.LockedUntil.Value, now));

        if (!BCrypt.Net.BCrypt.Verify(req.Password ?? "", user.PasswordHash))
        {
            LoginLock.RegisterFailure(user, now);
            await _db.SaveChangesAsync();
            if (LoginLock.IsLocked(user, now))
                throw BusinessException.Locked(LockMessage(user.LockedUntil.Value, now));
            var left = LoginLock.MaxAttempts - user.FailedLoginCount;
            throw BusinessException.Unauthorized($"Неверный логин или пароль. Осталось попыток: {left}");
        }

        LoginLock.RegisterSuccess(user);
        await _db.SaveChangesAsync();
        return Response(user);
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest req)
    {
        var phone = Phone.Normalize(req.Phone) ?? throw BusinessException.BadRequest("Телефон должен быть в формате +7XXXXXXXXXX");
        if (!PasswordPolicy.IsValid(req.Password)) throw BusinessException.BadRequest(PasswordPolicy.Requirements);
        var email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim().ToLower();

        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Phone == phone);
        if (user != null && (user.PasswordHash != null || user.Role.Code != Roles.Client))
            throw BusinessException.Conflict("Пользователь с таким телефоном уже зарегистрирован");
        if (email != null && await _db.Users.AnyAsync(u => u.Email == email && u.Phone != phone))
            throw BusinessException.Conflict("Этот email уже используется");

        if (user == null)
        {
            // новый клиент
            user = new User { RoleId = await _db.Roles.Where(r => r.Code == Roles.Client).Select(r => r.Id).FirstAsync(), Phone = phone };
            _db.Users.Add(user);
        }
        // клиент, ранее заведённый приёмщиком, завершает регистрацию и видит свои заявки
        user.LastName = req.LastName.Trim();
        user.FirstName = req.FirstName.Trim();
        user.Email = email;
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password, 11);
        await _db.SaveChangesAsync();
        await _db.Entry(user).Reference(u => u.Role).LoadAsync();
        return Response(user);
    }

    public AuthResponse Response(User user)
    {
        var (token, expires) = _tokens.Create(user);
        return new AuthResponse { Token = token, ExpiresAt = expires, User = ToDto(user) };
    }

    public static UserDto ToDto(User u) => new()
    {
        Id = u.Id,
        Role = u.Role.Code,
        RoleName = u.Role.Name,
        LastName = u.LastName,
        FirstName = u.FirstName,
        MiddleName = u.MiddleName,
        Phone = u.Phone,
        Email = u.Email,
        FullName = u.FullName,
        ShortName = u.ShortName,
    };

    private static string LockMessage(DateTime until, DateTime now)
    {
        var min = Math.Max(1, (int)Math.Ceiling((until - now).TotalMinutes));
        return $"Учётная запись временно заблокирована после 5 неудачных попыток. Повторите через {min} мин.";
    }
}
