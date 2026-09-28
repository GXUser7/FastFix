using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Hubs;
using ServiceDesk.Api.Infrastructure;
using ServiceDesk.Api.Services;
using ServiceDesk.Contracts;

// настройки читаются из каталога сборки — независимо от того, откуда запущен сервер
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory });
// локальные настройки разработчика (строка подключения к своему MySQL) — не попадают в репозиторий
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
builder.Configuration.AddEnvironmentVariables();
var cfg = builder.Configuration;

// ---------- база данных ----------
var connection = cfg.GetConnectionString("Default")
                 ?? throw new InvalidOperationException("Не задана строка подключения ConnectionStrings:Default");
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseMySql(connection, new MySqlServerVersion(new Version(8, 0, 36))));

// ---------- аутентификация (JWT) ----------
var jwt = cfg.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
    throw new InvalidOperationException("Jwt:Key должен содержать не менее 32 символов");
builder.Services.AddSingleton(jwt);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = jwt.Issuer,
        ValidAudience = jwt.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
        ClockSkew = TimeSpan.FromMinutes(1),
        NameClaimType = System.Security.Claims.ClaimTypes.Name,
        RoleClaimType = System.Security.Claims.ClaimTypes.Role,
    };
    // SignalR передаёт токен в строке запроса WebSocket-подключения
    o.Events = new JwtBearerEvents
    {
        OnMessageReceived = ctx =>
        {
            var token = ctx.Request.Query["access_token"];
            if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                ctx.Token = token;
            return Task.CompletedTask;
        },
        OnChallenge = async ctx =>
        {
            ctx.HandleResponse();
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await ctx.Response.WriteAsJsonAsync(new ApiError { Message = "Требуется вход в систему" });
        },
        OnForbidden = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return ctx.Response.WriteAsJsonAsync(new ApiError { Message = "Недостаточно прав для выполнения операции" });
        },
    };
});
builder.Services.AddAuthorization();

// ---------- сервисы ----------
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<WarehouseService>();
builder.Services.AddScoped<DocumentRegistry>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<DocumentService>();
builder.Services.AddScoped<INotifier, NotificationService>();
builder.Services.AddSignalR();

builder.Services.AddControllers().ConfigureApiBehaviorOptions(o =>
{
    // ошибки валидации — в едином формате { message }
    o.InvalidModelStateResponseFactory = ctx =>
    {
        var message = ctx.ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
            .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m)) ?? "Некорректные данные запроса";
        if (message.Contains("JSON", StringComparison.OrdinalIgnoreCase) || message.Contains("field is required"))
            message = "Некорректные данные запроса";
        return new BadRequestObjectResult(new ApiError { Message = message });
    };
});

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(cfg.GetSection("Cors:Origins").Get<string[]>() ?? new[] { "http://localhost:5173" })
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials().WithExposedHeaders("Content-Disposition")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "СервисДеск API", Version = "v1", Description = "REST API информационной системы управления заявками в сервисный центр" });
    var xml = Path.Combine(AppContext.BaseDirectory, "ServiceDesk.Api.xml");
    if (File.Exists(xml)) o.IncludeXmlComments(xml);
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
        Description = "JWT из ответа POST /api/auth/login",
    });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() },
    });
});

DocumentService.Init();

var app = builder.Build();

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseSwagger();
app.UseSwaggerUI(o => o.DocumentTitle = "СервисДеск API");
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapGet("/api/health", async (AppDbContext db) =>
    await db.Database.CanConnectAsync() ? Results.Ok(new { status = "ok" }) : Results.StatusCode(503)).ExcludeFromDescription();
app.MapControllers();
app.MapHub<OrdersHub>("/hubs/orders");

app.Run();

public partial class Program { }
