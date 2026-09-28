using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Hubs;
using ServiceDesk.Api.Infrastructure;
using ServiceDesk.Api.Services;
using ServiceDesk.Contracts;

namespace ServiceDesk.Tests;

// БД в памяти с минимальными справочниками для тестов сервисов
public class TestDb : IDisposable
{
    public const int Receptionist = 1, Master = 3, OtherMaster = 4, Storekeeper = 6, Client = 10, OtherClient = 11;
    public const int PartInStock = 1, PartOutOfStock = 2;

    public AppDbContext Db { get; }
    public List<OrderUpdatedEvent> Events { get; } = new();
    public WarehouseService Warehouse { get; }
    public OrderService Orders { get; }

    public TestDb()
    {
        var opt = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        Db = new AppDbContext(opt);
        Seed();
        Warehouse = new WarehouseService(Db);
        Orders = new OrderService(Db, Warehouse, new FakeNotifier(Events), new DocumentRegistry(Db));
    }

    public static CurrentUser As(int id, string role) => new(id, role);
    public static CurrentUser AsReceptionist => As(Receptionist, Roles.Receptionist);
    public static CurrentUser AsMaster => As(Master, Roles.Master);
    public static CurrentUser AsClient => As(Client, Roles.Client);
    public static CurrentUser AsStorekeeper => As(Storekeeper, Roles.Storekeeper);

    private void Seed()
    {
        Db.Roles.AddRange(new Role { Id = 1, Code = Roles.Client, Name = "Клиент" }, new Role { Id = 2, Code = Roles.Receptionist, Name = "Приёмщик" },
            new Role { Id = 3, Code = Roles.Master, Name = "Мастер" }, new Role { Id = 4, Code = Roles.Storekeeper, Name = "Кладовщик" });
        var codes = new[] { OrderStatuses.Accepted, OrderStatuses.Diagnostics, OrderStatuses.Approval, OrderStatuses.WaitingParts,
            OrderStatuses.InWork, OrderStatuses.Ready, OrderStatuses.Issued, OrderStatuses.Cancelled };
        for (var i = 0; i < codes.Length; i++)
            Db.OrderStatuses.Add(new OrderStatus { Id = i + 1, Code = codes[i], Name = codes[i], Color = "#000000", SortOrder = i + 1, IsFinal = codes[i] == OrderStatuses.Issued });
        User U(int id, int role, string last, string phone, string password = "Test2026") => new()
        {
            Id = id, RoleId = role, LastName = last, FirstName = "Тест", Phone = phone,
            PasswordHash = password == null ? null : BCrypt.Net.BCrypt.HashPassword(password, 4),
        };
        Db.Users.AddRange(U(Receptionist, 2, "Соколова", "+79000000001"), U(Master, 3, "Орлов", "+79000000003"), U(OtherMaster, 3, "Лебедев", "+79000000004"),
            U(Storekeeper, 4, "Морозова", "+79000000006"), U(Client, 1, "Иванов", "+79001234567"), U(OtherClient, 1, "Смирнов", "+79005551122"));
        Db.DeviceTypes.Add(new DeviceType { Id = 1, Name = "Ноутбук" });
        Db.CompletenessItems.Add(new CompletenessItem { Id = 1, Name = "Зарядное устройство", Kind = "accessory" });
        Db.Services.Add(new RepairService { Id = 1, Name = "Замена блока питания", Price = 1200 });
        Db.Parts.AddRange(
            new Part { Id = PartInStock, Sku = "PWR-65W", Name = "Блок питания 65W", Price = 2000, QuantityOnHand = 5, MinQuantity = 2 },
            new Part { Id = PartOutOfStock, Sku = "LED-55", Name = "Подсветка 55\"", Price = 3800, QuantityOnHand = 0, MinQuantity = 1 });
        Db.SaveChanges();
    }

    // Новая заявка клиента Client, назначенная мастеру Master
    public async Task<int> NewOrderAsync(bool warranty = false) =>
        await Orders.CreateAsync(new CreateOrderRequest
        {
            ClientPhone = "8 900 123-45-67", DeviceTypeId = 1, Brand = "ASUS", Model = "X515", DeclaredFault = "Не включается",
            CompletenessIds = new() { 1 }, MasterId = Master, IsWarranty = warranty,
        }, AsReceptionist);

    public async Task<string> StatusOf(int orderId) =>
        await Db.Orders.Where(o => o.Id == orderId).Select(o => o.Status.Code).FirstAsync();

    public Task SetStatus(int orderId, string status, CurrentUser user) =>
        Orders.ChangeStatusAsync(orderId, new ChangeStatusRequest { Status = status }, user);

    public void Dispose() => Db.Dispose();

    private class FakeNotifier : INotifier
    {
        private readonly List<OrderUpdatedEvent> _events;
        public FakeNotifier(List<OrderUpdatedEvent> events) => _events = events;
        public Task OrderUpdatedAsync(int clientId, OrderUpdatedEvent e)
        {
            _events.Add(e);
            return Task.CompletedTask;
        }
    }
}
