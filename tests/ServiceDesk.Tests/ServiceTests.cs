using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Infrastructure;
using ServiceDesk.Api.Services;
using ServiceDesk.Contracts;
using Xunit;
using S = ServiceDesk.Contracts.OrderStatuses;

namespace ServiceDesk.Tests;

// Сценарии заявки на уровне сервиса (БД в памяти)
public class OrderServiceTests : IDisposable
{
    private readonly TestDb _t = new();

    public void Dispose() => _t.Dispose();

    [Fact]
    public async Task CreateOrderWritesHistoryAndReceipt()
    {
        var id = await _t.NewOrderAsync();
        var o = await _t.Orders.GetAsync(id, TestDb.AsReceptionist);
        Assert.Equal(S.Accepted, o.Status);
        Assert.Equal(TestDb.Client, o.Client.Id);           // клиент найден по телефону в формате 8 900 …
        Assert.Single(o.History);
        Assert.Contains(o.Documents, d => d.Type == DocumentTypes.Receipt && d.CreatedAt != null);
        Assert.Single(_t.Events);
    }

    [Fact]
    public async Task CreateOrderRegistersNewClient()
    {
        var id = await _t.Orders.CreateAsync(new CreateOrderRequest
        {
            ClientPhone = "+7 916 000-11-22", ClientLastName = "Новиков", ClientFirstName = "Пётр",
            DeviceTypeId = 1, Brand = "Apple", Model = "iPhone 13", DeclaredFault = "Разбит экран",
        }, TestDb.AsReceptionist);
        var client = await _t.Db.Users.SingleAsync(u => u.Phone == "+79160001122");
        Assert.Null(client.PasswordHash);
        Assert.Equal(client.Id, (await _t.Db.Orders.FindAsync(id)).ClientId);
    }

    [Fact]
    public async Task CreateOrderWithoutNameForUnknownPhoneFails()
    {
        var ex = await Assert.ThrowsAsync<BusinessException>(() => _t.Orders.CreateAsync(new CreateOrderRequest
        {
            ClientPhone = "+79160009999", DeviceTypeId = 1, Brand = "A", Model = "B", DeclaredFault = "C",
        }, TestDb.AsReceptionist));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task FullRepairCycle()
    {
        var id = await _t.NewOrderAsync();
        await _t.SetStatus(id, S.Diagnostics, TestDb.AsMaster);
        await _t.Orders.SaveDiagnosisAsync(id, new DiagnosisRequest { Diagnosis = "Неисправен блок питания", WarrantyMonths = 6 }, TestDb.AsMaster);
        await _t.Orders.AddWorkAsync(id, new AddWorkRequest { ServiceId = 1 }, TestDb.AsMaster);
        var part = await _t.Orders.AddPartAsync(id, new AddPartRequest { PartId = TestDb.PartInStock, Quantity = 1 }, TestDb.AsMaster);
        Assert.Equal(ReservationStatuses.Reserved, part.Status);
        Assert.Equal(3200m, (await _t.Db.Orders.FindAsync(id)).TotalCost);

        await _t.SetStatus(id, S.Approval, TestDb.AsMaster);
        var forClient = await _t.Orders.GetAsync(id, TestDb.AsClient);
        Assert.True(forClient.CanDecide);
        await _t.Orders.DecideAsync(id, new DecisionRequest { Approve = true }, TestDb.AsClient);
        Assert.Equal(S.InWork, await _t.StatusOf(id));

        // мастер не может закрыть ремонт, пока деталь не выдана со склада
        var ex = await Assert.ThrowsAsync<BusinessException>(() => _t.SetStatus(id, S.Ready, TestDb.AsMaster));
        Assert.Equal(409, ex.StatusCode);
        await _t.Warehouse.IssueAsync(part.Id, TestDb.Storekeeper);
        await _t.SetStatus(id, S.Ready, TestDb.AsMaster);

        // выдача без оплаты запрещена
        ex = await Assert.ThrowsAsync<BusinessException>(() => _t.Orders.IssueAsync(id, TestDb.AsReceptionist));
        Assert.Equal(409, ex.StatusCode);
        await _t.Orders.PayAsync(id, new PaymentRequest { Amount = 2000, Method = PaymentMethods.Cash }, TestDb.AsReceptionist);
        await _t.Orders.PayAsync(id, new PaymentRequest { Amount = 1200, Method = PaymentMethods.Card }, TestDb.AsReceptionist);
        await _t.Orders.IssueAsync(id, TestDb.AsReceptionist);

        var o = await _t.Orders.GetAsync(id, TestDb.AsReceptionist);
        Assert.Equal(S.Issued, o.Status);
        Assert.Equal(0m, o.Due);
        Assert.Equal(new[] { S.Accepted, S.Diagnostics, S.Approval, S.InWork, S.Ready, S.Issued }, o.History.Select(h => h.Status));
        Assert.All(o.Documents, d => Assert.True(d.Available));
        var stock = await _t.Db.Parts.FindAsync(TestDb.PartInStock);
        Assert.Equal(4, stock.QuantityOnHand);
        Assert.Equal(0, stock.QuantityReserved);
    }

    [Fact]
    public async Task OverpaymentRejected()
    {
        var id = await _t.NewOrderAsync();
        await _t.SetStatus(id, S.Diagnostics, TestDb.AsMaster);
        await _t.Orders.AddWorkAsync(id, new AddWorkRequest { Description = "Диагностика", Price = 500 }, TestDb.AsMaster);
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _t.Orders.PayAsync(id, new PaymentRequest { Amount = 600, Method = PaymentMethods.Sbp }, TestDb.AsReceptionist));
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task ApprovalRequiresDiagnosisAndWorks()
    {
        var id = await _t.NewOrderAsync();
        await _t.SetStatus(id, S.Diagnostics, TestDb.AsMaster);
        await Assert.ThrowsAsync<BusinessException>(() => _t.SetStatus(id, S.Approval, TestDb.AsMaster));
    }

    [Fact]
    public async Task InvalidTransitionIs409()
    {
        var id = await _t.NewOrderAsync();
        var ex = await Assert.ThrowsAsync<BusinessException>(() => _t.SetStatus(id, S.Ready, TestDb.AsMaster));
        Assert.Equal(409, ex.StatusCode);
        Assert.Equal(S.Accepted, await _t.StatusOf(id));
    }

    [Fact]
    public async Task ClientRejectCancelsAndReleasesReserve()
    {
        var id = await _t.NewOrderAsync();
        await _t.SetStatus(id, S.Diagnostics, TestDb.AsMaster);
        await _t.Orders.SaveDiagnosisAsync(id, new DiagnosisRequest { Diagnosis = "Замена БП" }, TestDb.AsMaster);
        await _t.Orders.AddWorkAsync(id, new AddWorkRequest { ServiceId = 1 }, TestDb.AsMaster);
        await _t.Orders.AddPartAsync(id, new AddPartRequest { PartId = TestDb.PartInStock, Quantity = 2 }, TestDb.AsMaster);
        await _t.SetStatus(id, S.Approval, TestDb.AsMaster);
        await _t.Orders.DecideAsync(id, new DecisionRequest { Approve = false }, TestDb.AsClient);

        Assert.Equal(S.Cancelled, await _t.StatusOf(id));
        Assert.Equal(0, (await _t.Db.Parts.FindAsync(TestDb.PartInStock)).QuantityReserved);
        // возврат устройства по отменённой заявке выполняется без оплаты
        await _t.Orders.IssueAsync(id, TestDb.AsReceptionist);
        Assert.Equal(S.Issued, await _t.StatusOf(id));
    }

    [Fact]
    public async Task ShortageMovesToWaitingPartsAndReceiptReturnsToWork()
    {
        var id = await _t.NewOrderAsync();
        await _t.SetStatus(id, S.Diagnostics, TestDb.AsMaster);
        await _t.Orders.SaveDiagnosisAsync(id, new DiagnosisRequest { Diagnosis = "Нет подсветки" }, TestDb.AsMaster);
        await _t.Orders.AddWorkAsync(id, new AddWorkRequest { ServiceId = 1 }, TestDb.AsMaster);
        var res = await _t.Orders.AddPartAsync(id, new AddPartRequest { PartId = TestDb.PartOutOfStock, Quantity = 1 }, TestDb.AsMaster);
        Assert.Equal(ReservationStatuses.Requested, res.Status);

        await _t.SetStatus(id, S.Approval, TestDb.AsMaster);
        await _t.Orders.DecideAsync(id, new DecisionRequest { Approve = true }, TestDb.AsClient);
        Assert.Equal(S.WaitingParts, await _t.StatusOf(id));

        var orders = await _t.Warehouse.ReceiptAsync(TestDb.PartOutOfStock, 2, "Накладная № 1", TestDb.Storekeeper);
        await _t.Orders.OnPartsArrivedAsync(orders, TestDb.Storekeeper);
        Assert.Equal(S.InWork, await _t.StatusOf(id));
        var part = await _t.Db.Parts.FindAsync(TestDb.PartOutOfStock);
        Assert.Equal(2, part.QuantityOnHand);
        Assert.Equal(1, part.QuantityReserved);
    }

    [Fact]
    public async Task AccessByRole()
    {
        var id = await _t.NewOrderAsync();
        var other = TestDb.As(TestDb.OtherClient, Roles.Client);
        Assert.Equal(404, (await Assert.ThrowsAsync<BusinessException>(() => _t.Orders.GetAsync(id, other))).StatusCode);
        var otherMaster = TestDb.As(TestDb.OtherMaster, Roles.Master);
        Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => _t.Orders.GetAsync(id, otherMaster))).StatusCode);
        Assert.Empty(await _t.Orders.ListAsync(other, new OrderQuery()));
        Assert.Single(await _t.Orders.ListAsync(TestDb.AsClient, new OrderQuery()));
        Assert.Equal(403, (await Assert.ThrowsAsync<BusinessException>(() => _t.Orders.ListAsync(TestDb.AsStorekeeper, new OrderQuery()))).StatusCode);
    }

    [Fact]
    public async Task WarrantyOrderIsFree()
    {
        var id = await _t.NewOrderAsync(warranty: true);
        await _t.SetStatus(id, S.Diagnostics, TestDb.AsMaster);
        await _t.Orders.SaveDiagnosisAsync(id, new DiagnosisRequest { Diagnosis = "Гарантийный случай" }, TestDb.AsMaster);
        await _t.Orders.AddWorkAsync(id, new AddWorkRequest { ServiceId = 1 }, TestDb.AsMaster);
        await _t.SetStatus(id, S.InWork, TestDb.AsMaster);
        await _t.SetStatus(id, S.Ready, TestDb.AsMaster);
        Assert.Equal(0m, (await _t.Db.Orders.FindAsync(id)).TotalCost);
        await _t.Orders.IssueAsync(id, TestDb.AsReceptionist);
        Assert.Equal(S.Issued, await _t.StatusOf(id));
    }
}

// Складские операции
public class WarehouseServiceTests : IDisposable
{
    private readonly TestDb _t = new();

    public void Dispose() => _t.Dispose();

    [Fact]
    public async Task ReserveWhenEnoughStock()
    {
        var id = await _t.NewOrderAsync();
        var r = await _t.Warehouse.ReserveAsync(id, TestDb.PartInStock, 5, TestDb.Master);
        Assert.Equal(ReservationStatuses.Reserved, r.Status);
        // доступный остаток исчерпан — следующий запрос ожидает поступления
        var r2 = await _t.Warehouse.ReserveAsync(id, TestDb.PartInStock, 1, TestDb.Master);
        Assert.Equal(ReservationStatuses.Requested, r2.Status);
        Assert.Equal(5, (await _t.Db.Parts.FindAsync(TestDb.PartInStock)).QuantityReserved);
    }

    [Fact]
    public async Task IssueWritesOffStockAndJournal()
    {
        var id = await _t.NewOrderAsync();
        var r = await _t.Warehouse.ReserveAsync(id, TestDb.PartInStock, 2, TestDb.Master);
        await _t.Warehouse.IssueAsync(r.Id, TestDb.Storekeeper);
        var p = await _t.Db.Parts.FindAsync(TestDb.PartInStock);
        Assert.Equal(3, p.QuantityOnHand);
        Assert.Equal(0, p.QuantityReserved);
        var m = await _t.Db.PartMovements.Where(x => x.MovementType == MovementTypes.Issue).SingleAsync();
        Assert.Equal(-2, m.Quantity);
        Assert.Equal(3, m.BalanceAfter);
        // повторная выдача невозможна
        await Assert.ThrowsAsync<BusinessException>(() => _t.Warehouse.IssueAsync(r.Id, TestDb.Storekeeper));
    }

    [Fact]
    public async Task InventoryCorrectsStock()
    {
        var inv = await _t.Warehouse.InventoryAsync(new InventoryRequest
        {
            Items = new() { new InventoryLine { PartId = TestDb.PartInStock, ActualQuantity = 4 }, new InventoryLine { PartId = TestDb.PartOutOfStock, ActualQuantity = 0 } },
        }, TestDb.Storekeeper);
        Assert.Equal(1, inv.Discrepancies);
        Assert.Equal(4, (await _t.Db.Parts.FindAsync(TestDb.PartInStock)).QuantityOnHand);
        var m = await _t.Db.PartMovements.SingleAsync(x => x.MovementType == MovementTypes.Inventory);
        Assert.Equal(-1, m.Quantity);
    }

    [Fact]
    public async Task InventoryBelowReservedRejected()
    {
        var id = await _t.NewOrderAsync();
        await _t.Warehouse.ReserveAsync(id, TestDb.PartInStock, 3, TestDb.Master);
        var ex = await Assert.ThrowsAsync<BusinessException>(() => _t.Warehouse.InventoryAsync(new InventoryRequest
        {
            Items = new() { new InventoryLine { PartId = TestDb.PartInStock, ActualQuantity = 2 } },
        }, TestDb.Storekeeper));
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task BelowMinimumFlag()
    {
        var p = await _t.Db.Parts.FindAsync(TestDb.PartOutOfStock);
        Assert.True(WarehouseService.ToDto(p).BelowMinimum);
        Assert.False(WarehouseService.ToDto(await _t.Db.Parts.FindAsync(TestDb.PartInStock)).BelowMinimum);
    }
}

// Блокировка входа (ТЗ, п. 6)
public class LoginLockTests
{
    [Fact]
    public void LocksAfterFiveFailures()
    {
        var u = new User();
        var now = new DateTime(2026, 10, 1, 12, 0, 0);
        for (var i = 0; i < 4; i++) LoginLock.RegisterFailure(u, now);
        Assert.False(LoginLock.IsLocked(u, now));
        LoginLock.RegisterFailure(u, now);
        Assert.True(LoginLock.IsLocked(u, now));
        Assert.True(LoginLock.IsLocked(u, now.AddMinutes(14)));
        Assert.False(LoginLock.IsLocked(u, now.AddMinutes(15)));
    }

    [Fact]
    public void SuccessResetsCounter()
    {
        var u = new User();
        var now = DateTime.Now;
        for (var i = 0; i < 4; i++) LoginLock.RegisterFailure(u, now);
        LoginLock.RegisterSuccess(u);
        LoginLock.RegisterFailure(u, now);
        Assert.False(LoginLock.IsLocked(u, now));
        Assert.Equal(1, u.FailedLoginCount);
    }

    [Theory]
    [InlineData("Client2026", true)]
    [InlineData("short1", false)]
    [InlineData("onlyletters", false)]
    [InlineData("12345678", false)]
    public void PasswordPolicy(string password, bool ok) => Assert.Equal(ok, ServiceDesk.Api.Infrastructure.PasswordPolicy.IsValid(password));

    [Theory]
    [InlineData("8 (900) 123-45-67", "+79001234567")]
    [InlineData("+7 900 123 45 67", "+79001234567")]
    [InlineData("9001234567", "+79001234567")]
    [InlineData("12345", null)]
    public void PhoneNormalize(string input, string expected) => Assert.Equal(expected, Phone.Normalize(input));
}
