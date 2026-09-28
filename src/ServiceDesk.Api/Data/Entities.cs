namespace ServiceDesk.Api.Data;

// Классы сущностей соответствуют таблицам БД servicedesk (db/01_schema.sql)

public class Role
{
    public int Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
}

public class User
{
    public int Id { get; set; }
    public int RoleId { get; set; }
    public Role Role { get; set; }
    public string LastName { get; set; }
    public string FirstName { get; set; }
    public string MiddleName { get; set; }
    public string Phone { get; set; }
    public string Email { get; set; }
    public string PasswordHash { get; set; }
    public bool IsActive { get; set; } = true;
    public int FailedLoginCount { get; set; }
    public DateTime? LockedUntil { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public string FullName => string.Join(" ", new[] { LastName, FirstName, MiddleName }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public string ShortName => $"{LastName} {Initial(FirstName)}{Initial(MiddleName)}".Trim();

    private static string Initial(string s) => string.IsNullOrWhiteSpace(s) ? "" : s[0] + ".";
}

public class DeviceType
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public class Device
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public User Client { get; set; }
    public int DeviceTypeId { get; set; }
    public DeviceType DeviceType { get; set; }
    public string Brand { get; set; }
    public string Model { get; set; }
    public string SerialNumber { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public string Title => $"{DeviceType?.Name} {Brand} {Model}".Trim();
}

public class OrderStatus
{
    public int Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string Color { get; set; }
    public int SortOrder { get; set; }
    public bool IsFinal { get; set; }
}

public class Order
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public User Client { get; set; }
    public int DeviceId { get; set; }
    public Device Device { get; set; }
    public int ReceptionistId { get; set; }
    public User Receptionist { get; set; }
    public int? MasterId { get; set; }
    public User Master { get; set; }
    public int StatusId { get; set; }
    public OrderStatus Status { get; set; }
    public string DeclaredFault { get; set; }
    public string AppearanceNote { get; set; }
    public string Diagnosis { get; set; }
    public bool IsWarranty { get; set; }
    public int? WarrantyMonths { get; set; }
    public string ClientDecision { get; set; }
    public DateTime? DecisionAt { get; set; }
    public decimal TotalCost { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? ReadyAt { get; set; }
    public DateTime? IssuedAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public List<OrderCompleteness> Completeness { get; set; } = new();
    public List<OrderStatusHistory> History { get; set; } = new();
    public List<OrderWork> Works { get; set; } = new();
    public List<PartReservation> Reservations { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();
    public List<Document> Documents { get; set; } = new();
}

public class CompletenessItem
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Kind { get; set; }
}

public class OrderCompleteness
{
    public int OrderId { get; set; }
    public Order Order { get; set; }
    public int ItemId { get; set; }
    public CompletenessItem Item { get; set; }
}

public class OrderStatusHistory
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int StatusId { get; set; }
    public OrderStatus Status { get; set; }
    public int ChangedById { get; set; }
    public User ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.Now;
    public string Comment { get; set; }
}

public class RepairService
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
}

public class OrderWork
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int? ServiceId { get; set; }
    public RepairService Service { get; set; }
    public int MasterId { get; set; }
    public User Master { get; set; }
    public string Description { get; set; }
    public decimal Price { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class Part
{
    public int Id { get; set; }
    public string Sku { get; set; }
    public string Name { get; set; }
    public string Unit { get; set; } = "шт";
    public decimal Price { get; set; }
    public int QuantityOnHand { get; set; }
    public int QuantityReserved { get; set; }
    public int MinQuantity { get; set; }
    public string Location { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public int Available => QuantityOnHand - QuantityReserved;
}

public class PartReservation
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; }
    public int PartId { get; set; }
    public Part Part { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public string Status { get; set; }
    public int RequestedById { get; set; }
    public User RequestedBy { get; set; }
    public int? IssuedById { get; set; }
    public User IssuedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? IssuedAt { get; set; }
}

public class PartMovement
{
    public int Id { get; set; }
    public int PartId { get; set; }
    public Part Part { get; set; }
    public int? OrderId { get; set; }
    public string MovementType { get; set; }
    public int Quantity { get; set; }
    public int BalanceAfter { get; set; }
    public int UserId { get; set; }
    public User User { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string Comment { get; set; }
}

public class Inventory
{
    public int Id { get; set; }
    public int CreatedById { get; set; }
    public User CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string Comment { get; set; }
    public int Discrepancies { get; set; }
    public List<InventoryItem> Items { get; set; } = new();
}

public class InventoryItem
{
    public int Id { get; set; }
    public int InventoryId { get; set; }
    public int PartId { get; set; }
    public Part Part { get; set; }
    public int ExpectedQty { get; set; }
    public int ActualQty { get; set; }
    public int Difference { get; set; }
}

public class Payment
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; }
    public int ReceivedById { get; set; }
    public User ReceivedBy { get; set; }
    public DateTime PaidAt { get; set; } = DateTime.Now;
}

public class Document
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string DocType { get; set; }
    public string Number { get; set; }
    public int CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
