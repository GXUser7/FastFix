using Microsoft.EntityFrameworkCore;

namespace ServiceDesk.Api.Data;

// Контекст БД: отображение классов на таблицы и связи (Fluent API)
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<DeviceType> DeviceTypes => Set<DeviceType>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<OrderStatus> OrderStatuses => Set<OrderStatus>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<CompletenessItem> CompletenessItems => Set<CompletenessItem>();
    public DbSet<OrderCompleteness> OrderCompleteness => Set<OrderCompleteness>();
    public DbSet<OrderStatusHistory> OrderStatusHistory => Set<OrderStatusHistory>();
    public DbSet<RepairService> Services => Set<RepairService>();
    public DbSet<OrderWork> OrderWorks => Set<OrderWork>();
    public DbSet<Part> Parts => Set<Part>();
    public DbSet<PartReservation> PartReservations => Set<PartReservation>();
    public DbSet<PartMovement> PartMovements => Set<PartMovement>();
    public DbSet<Inventory> Inventories => Set<Inventory>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Document> Documents => Set<Document>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Role>(e =>
        {
            e.ToTable("roles");
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.Code).HasColumnName("code");
            e.Property(x => x.Name).HasColumnName("name");
        });

        b.Entity<User>(e =>
        {
            e.ToTable("users");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.RoleId).HasColumnName("role_id");
            e.Property(x => x.LastName).HasColumnName("last_name");
            e.Property(x => x.FirstName).HasColumnName("first_name");
            e.Property(x => x.MiddleName).HasColumnName("middle_name");
            e.Property(x => x.Phone).HasColumnName("phone");
            e.Property(x => x.Email).HasColumnName("email");
            e.Property(x => x.PasswordHash).HasColumnName("password_hash");
            e.Property(x => x.IsActive).HasColumnName("is_active");
            e.Property(x => x.FailedLoginCount).HasColumnName("failed_login_count");
            e.Property(x => x.LockedUntil).HasColumnName("locked_until");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId);
            e.HasIndex(x => x.Phone).IsUnique();
            e.HasIndex(x => x.Email).IsUnique();
            e.Ignore(x => x.FullName);
            e.Ignore(x => x.ShortName);
        });

        b.Entity<DeviceType>(e =>
        {
            e.ToTable("device_types");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name");
        });

        b.Entity<Device>(e =>
        {
            e.ToTable("devices");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ClientId).HasColumnName("client_id");
            e.Property(x => x.DeviceTypeId).HasColumnName("device_type_id");
            e.Property(x => x.Brand).HasColumnName("brand");
            e.Property(x => x.Model).HasColumnName("model");
            e.Property(x => x.SerialNumber).HasColumnName("serial_number");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.DeviceType).WithMany().HasForeignKey(x => x.DeviceTypeId);
            e.Ignore(x => x.Title);
        });

        b.Entity<OrderStatus>(e =>
        {
            e.ToTable("order_statuses");
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.Code).HasColumnName("code");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.Color).HasColumnName("color");
            e.Property(x => x.SortOrder).HasColumnName("sort_order");
            e.Property(x => x.IsFinal).HasColumnName("is_final");
        });

        b.Entity<Order>(e =>
        {
            e.ToTable("orders");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ClientId).HasColumnName("client_id");
            e.Property(x => x.DeviceId).HasColumnName("device_id");
            e.Property(x => x.ReceptionistId).HasColumnName("receptionist_id");
            e.Property(x => x.MasterId).HasColumnName("master_id");
            e.Property(x => x.StatusId).HasColumnName("status_id");
            e.Property(x => x.DeclaredFault).HasColumnName("declared_fault");
            e.Property(x => x.AppearanceNote).HasColumnName("appearance_note");
            e.Property(x => x.Diagnosis).HasColumnName("diagnosis");
            e.Property(x => x.IsWarranty).HasColumnName("is_warranty");
            e.Property(x => x.WarrantyMonths).HasColumnName("warranty_months");
            e.Property(x => x.ClientDecision).HasColumnName("client_decision");
            e.Property(x => x.DecisionAt).HasColumnName("decision_at");
            e.Property(x => x.TotalCost).HasColumnName("total_cost").HasPrecision(10, 2);
            e.Property(x => x.DueDate).HasColumnName("due_date").HasColumnType("date");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.ReadyAt).HasColumnName("ready_at");
            e.Property(x => x.IssuedAt).HasColumnName("issued_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Receptionist).WithMany().HasForeignKey(x => x.ReceptionistId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Master).WithMany().HasForeignKey(x => x.MasterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Status).WithMany().HasForeignKey(x => x.StatusId);
            e.HasMany(x => x.Completeness).WithOne(x => x.Order).HasForeignKey(x => x.OrderId);
            e.HasMany(x => x.History).WithOne().HasForeignKey(x => x.OrderId);
            e.HasMany(x => x.Works).WithOne().HasForeignKey(x => x.OrderId);
            e.HasMany(x => x.Reservations).WithOne(x => x.Order).HasForeignKey(x => x.OrderId);
            e.HasMany(x => x.Payments).WithOne().HasForeignKey(x => x.OrderId);
            e.HasMany(x => x.Documents).WithOne().HasForeignKey(x => x.OrderId);
        });

        b.Entity<CompletenessItem>(e =>
        {
            e.ToTable("completeness_items");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.Kind).HasColumnName("kind");
        });

        b.Entity<OrderCompleteness>(e =>
        {
            e.ToTable("order_completeness");
            e.HasKey(x => new { x.OrderId, x.ItemId });
            e.Property(x => x.OrderId).HasColumnName("order_id");
            e.Property(x => x.ItemId).HasColumnName("item_id");
            e.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId);
        });

        b.Entity<OrderStatusHistory>(e =>
        {
            e.ToTable("order_status_history");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.OrderId).HasColumnName("order_id");
            e.Property(x => x.StatusId).HasColumnName("status_id");
            e.Property(x => x.ChangedById).HasColumnName("changed_by");
            e.Property(x => x.ChangedAt).HasColumnName("changed_at");
            e.Property(x => x.Comment).HasColumnName("comment");
            e.HasOne(x => x.Status).WithMany().HasForeignKey(x => x.StatusId);
            e.HasOne(x => x.ChangedBy).WithMany().HasForeignKey(x => x.ChangedById).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<RepairService>(e =>
        {
            e.ToTable("services");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.Price).HasColumnName("price").HasPrecision(10, 2);
            e.Property(x => x.IsActive).HasColumnName("is_active");
        });

        b.Entity<OrderWork>(e =>
        {
            e.ToTable("order_works");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.OrderId).HasColumnName("order_id");
            e.Property(x => x.ServiceId).HasColumnName("service_id");
            e.Property(x => x.MasterId).HasColumnName("master_id");
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.Price).HasColumnName("price").HasPrecision(10, 2);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasOne(x => x.Service).WithMany().HasForeignKey(x => x.ServiceId);
            e.HasOne(x => x.Master).WithMany().HasForeignKey(x => x.MasterId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Part>(e =>
        {
            e.ToTable("parts");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Sku).HasColumnName("sku");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.Unit).HasColumnName("unit");
            e.Property(x => x.Price).HasColumnName("price").HasPrecision(10, 2);
            e.Property(x => x.QuantityOnHand).HasColumnName("quantity_on_hand");
            e.Property(x => x.QuantityReserved).HasColumnName("quantity_reserved");
            e.Property(x => x.MinQuantity).HasColumnName("min_quantity");
            e.Property(x => x.Location).HasColumnName("location");
            e.Property(x => x.IsActive).HasColumnName("is_active");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => x.Sku).IsUnique();
            e.Ignore(x => x.Available);
        });

        b.Entity<PartReservation>(e =>
        {
            e.ToTable("part_reservations");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.OrderId).HasColumnName("order_id");
            e.Property(x => x.PartId).HasColumnName("part_id");
            e.Property(x => x.Quantity).HasColumnName("quantity");
            e.Property(x => x.Price).HasColumnName("price").HasPrecision(10, 2);
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.RequestedById).HasColumnName("requested_by");
            e.Property(x => x.IssuedById).HasColumnName("issued_by");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.IssuedAt).HasColumnName("issued_at");
            e.HasOne(x => x.Part).WithMany().HasForeignKey(x => x.PartId);
            e.HasOne(x => x.RequestedBy).WithMany().HasForeignKey(x => x.RequestedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.IssuedBy).WithMany().HasForeignKey(x => x.IssuedById).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<PartMovement>(e =>
        {
            e.ToTable("part_movements");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.PartId).HasColumnName("part_id");
            e.Property(x => x.OrderId).HasColumnName("order_id");
            e.Property(x => x.MovementType).HasColumnName("movement_type");
            e.Property(x => x.Quantity).HasColumnName("quantity");
            e.Property(x => x.BalanceAfter).HasColumnName("balance_after");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.Comment).HasColumnName("comment");
            e.HasOne(x => x.Part).WithMany().HasForeignKey(x => x.PartId);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Inventory>(e =>
        {
            e.ToTable("inventories");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.CreatedById).HasColumnName("created_by");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.Comment).HasColumnName("comment");
            e.Property(x => x.Discrepancies).HasColumnName("discrepancies");
            e.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.InventoryId);
        });

        b.Entity<InventoryItem>(e =>
        {
            e.ToTable("inventory_items");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.InventoryId).HasColumnName("inventory_id");
            e.Property(x => x.PartId).HasColumnName("part_id");
            e.Property(x => x.ExpectedQty).HasColumnName("expected_qty");
            e.Property(x => x.ActualQty).HasColumnName("actual_qty");
            // вычисляемый столбец MySQL (GENERATED STORED) — EF его не записывает
            e.Property(x => x.Difference).HasColumnName("difference").ValueGeneratedOnAddOrUpdate();
            e.HasOne(x => x.Part).WithMany().HasForeignKey(x => x.PartId);
        });

        b.Entity<Payment>(e =>
        {
            e.ToTable("payments");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.OrderId).HasColumnName("order_id");
            e.Property(x => x.Amount).HasColumnName("amount").HasPrecision(10, 2);
            e.Property(x => x.Method).HasColumnName("method");
            e.Property(x => x.ReceivedById).HasColumnName("received_by");
            e.Property(x => x.PaidAt).HasColumnName("paid_at");
            e.HasOne(x => x.ReceivedBy).WithMany().HasForeignKey(x => x.ReceivedById).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Document>(e =>
        {
            e.ToTable("documents");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.OrderId).HasColumnName("order_id");
            e.Property(x => x.DocType).HasColumnName("doc_type");
            e.Property(x => x.Number).HasColumnName("number");
            e.Property(x => x.CreatedById).HasColumnName("created_by");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });
    }
}
