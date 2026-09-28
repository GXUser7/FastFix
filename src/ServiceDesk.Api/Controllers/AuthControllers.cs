using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Infrastructure;
using ServiceDesk.Api.Services;
using ServiceDesk.Contracts;

namespace ServiceDesk.Api.Controllers;

public abstract class ApiController : ControllerBase
{
    protected CurrentUser Me => CurrentUser.From(User);
}

/// <summary>Вход, регистрация клиента, текущий пользователь</summary>
[ApiController, Route("api/auth")]
public class AuthController : ApiController
{
    private readonly AuthService _auth;
    private readonly AppDbContext _db;

    public AuthController(AuthService auth, AppDbContext db)
    {
        _auth = auth;
        _db = db;
    }

    /// <summary>Вход по телефону или e-mail и паролю, выдача JWT (12 ч)</summary>
    [HttpPost("login")]
    public Task<AuthResponse> Login(LoginRequest req) => _auth.LoginAsync(req);

    /// <summary>Регистрация клиента</summary>
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest req) =>
        StatusCode(StatusCodes.Status201Created, await _auth.RegisterAsync(req));

    /// <summary>Текущий пользователь</summary>
    [Authorize, HttpGet("me")]
    public async Task<UserDto> GetMe()
    {
        var u = await _db.Users.Include(x => x.Role).FirstOrDefaultAsync(x => x.Id == Me.Id)
                ?? throw BusinessException.Unauthorized("Пользователь не найден");
        return AuthService.ToDto(u);
    }
}

/// <summary>Профиль пользователя</summary>
[ApiController, Authorize, Route("api/profile")]
public class ProfileController : ApiController
{
    private readonly AppDbContext _db;

    public ProfileController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<UserDto> Get() => AuthService.ToDto(await Load());

    /// <summary>Изменение фамилии, имени, телефона и e-mail</summary>
    [HttpPut]
    public async Task<UserDto> Update(ProfileUpdateRequest req)
    {
        var u = await Load();
        var phone = Phone.Normalize(req.Phone) ?? throw BusinessException.BadRequest("Телефон должен быть в формате +7XXXXXXXXXX");
        var email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim().ToLower();
        if (await _db.Users.AnyAsync(x => x.Id != u.Id && x.Phone == phone)) throw BusinessException.Conflict("Этот телефон уже используется");
        if (email != null && await _db.Users.AnyAsync(x => x.Id != u.Id && x.Email == email)) throw BusinessException.Conflict("Этот email уже используется");
        u.LastName = req.LastName.Trim();
        u.FirstName = req.FirstName.Trim();
        u.Phone = phone;
        u.Email = email;
        await _db.SaveChangesAsync();
        return AuthService.ToDto(u);
    }

    /// <summary>Смена пароля</summary>
    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest req)
    {
        var u = await Load();
        if (u.PasswordHash == null || !BCrypt.Net.BCrypt.Verify(req.CurrentPassword, u.PasswordHash))
            throw BusinessException.BadRequest("Текущий пароль указан неверно");
        if (!PasswordPolicy.IsValid(req.NewPassword)) throw BusinessException.BadRequest(PasswordPolicy.Requirements);
        u.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword, 11);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<User> Load() =>
        await _db.Users.Include(x => x.Role).FirstOrDefaultAsync(x => x.Id == Me.Id)
        ?? throw BusinessException.Unauthorized("Пользователь не найден");
}

/// <summary>Справочники</summary>
[ApiController, Authorize, Route("api/dictionaries")]
public class DictionariesController : ApiController
{
    private readonly AppDbContext _db;

    public DictionariesController(AppDbContext db) => _db = db;

    /// <summary>Статусы, типы устройств, комплектность, прайс-лист услуг, мастера</summary>
    [HttpGet]
    public async Task<DictionariesDto> Get()
    {
        var masters = await _db.Users.Where(u => u.Role.Code == Roles.Master && u.IsActive)
            .OrderBy(u => u.LastName).ToListAsync();
        return new DictionariesDto
        {
            Statuses = await _db.OrderStatuses.OrderBy(s => s.SortOrder).Select(s => new StatusDto
            {
                Id = s.Id, Code = s.Code, Name = s.Name, Color = s.Color, SortOrder = s.SortOrder, IsFinal = s.IsFinal,
            }).ToListAsync(),
            DeviceTypes = await _db.DeviceTypes.OrderBy(t => t.Id).Select(t => new IdName { Id = t.Id, Name = t.Name }).ToListAsync(),
            Completeness = await _db.CompletenessItems.OrderBy(c => c.Id).Select(c => new CompletenessItemDto { Id = c.Id, Name = c.Name, Kind = c.Kind }).ToListAsync(),
            Services = await _db.Services.Where(s => s.IsActive).OrderBy(s => s.Name).Select(s => new ServiceDto { Id = s.Id, Name = s.Name, Price = s.Price }).ToListAsync(),
            Masters = masters.Select(m => new IdName { Id = m.Id, Name = m.ShortName }).ToList(),
        };
    }
}

/// <summary>Поиск клиента приёмщиком</summary>
[ApiController, Authorize(Roles = Roles.Receptionist), Route("api/clients")]
public class ClientsController : ApiController
{
    private readonly AppDbContext _db;

    public ClientsController(AppDbContext db) => _db = db;

    /// <summary>Поиск клиента по номеру телефона (404 — клиент не найден)</summary>
    [HttpGet]
    public async Task<ClientDto> Find([FromQuery] string phone)
    {
        var normalized = Phone.Normalize(phone) ?? throw BusinessException.BadRequest("Телефон должен быть в формате +7XXXXXXXXXX");
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Phone == normalized && x.Role.Code == Roles.Client)
                ?? throw BusinessException.NotFound("Клиент не найден");
        return OrderService.ToClientDto(u);
    }
}
