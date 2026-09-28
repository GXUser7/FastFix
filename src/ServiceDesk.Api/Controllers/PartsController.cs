using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Infrastructure;
using ServiceDesk.Api.Services;
using ServiceDesk.Contracts;

namespace ServiceDesk.Api.Controllers;

/// <summary>Склад: номенклатура, оприходование, запросы мастеров, выдача, журнал движения</summary>
[ApiController, Authorize(Roles = Roles.Storekeeper + "," + Roles.Master), Route("api/parts")]
public class PartsController : ApiController
{
    private readonly AppDbContext _db;
    private readonly WarehouseService _warehouse;
    private readonly OrderService _orders;

    public PartsController(AppDbContext db, WarehouseService warehouse, OrderService orders)
    {
        _db = db;
        _warehouse = warehouse;
        _orders = orders;
    }

    /// <summary>Номенклатура и остатки (мастеру — только чтение для выбора запчасти)</summary>
    [HttpGet]
    public async Task<List<PartDto>> List([FromQuery] string search, [FromQuery] bool? belowMinimum, [FromQuery] bool includeInactive = false)
    {
        var q = _db.Parts.AsNoTracking();
        if (!includeInactive || Me.Role != Roles.Storekeeper) q = q.Where(p => p.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(p => p.Sku.Contains(s) || p.Name.Contains(s) || (p.Location != null && p.Location.Contains(s)));
        }
        if (belowMinimum == true) q = q.Where(p => p.QuantityOnHand - p.QuantityReserved < p.MinQuantity);
        return (await q.OrderBy(p => p.Name).ToListAsync()).Select(WarehouseService.ToDto).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<PartDto> Get(int id) =>
        WarehouseService.ToDto(await _db.Parts.FindAsync(id) ?? throw BusinessException.NotFound("Запчасть не найдена"));

    /// <summary>Новая позиция номенклатуры</summary>
    [HttpPost, Authorize(Roles = Roles.Storekeeper)]
    public async Task<ActionResult<PartDto>> Create(PartEditRequest req) =>
        StatusCode(StatusCodes.Status201Created, WarehouseService.ToDto(await _warehouse.SavePartAsync(null, req)));

    /// <summary>Изменение карточки позиции</summary>
    [HttpPut("{id:int}"), Authorize(Roles = Roles.Storekeeper)]
    public async Task<PartDto> Update(int id, PartEditRequest req) => WarehouseService.ToDto(await _warehouse.SavePartAsync(id, req));

    /// <summary>Оприходование поступивших запчастей</summary>
    [HttpPost("{id:int}/receipt"), Authorize(Roles = Roles.Storekeeper)]
    public async Task<PartDto> Receipt(int id, ReceiptRequest req)
    {
        var orders = await _db.InTransactionAsync(() => _warehouse.ReceiptAsync(id, req.Quantity, req.Comment, Me.Id));
        await _orders.OnPartsArrivedAsync(orders, Me.Id);
        return WarehouseService.ToDto(await _db.Parts.AsNoTracking().FirstAsync(p => p.Id == id));
    }

    /// <summary>Запросы мастеров: active (по умолчанию), all, requested, reserved, issued, cancelled</summary>
    [HttpGet("requests"), Authorize(Roles = Roles.Storekeeper)]
    public async Task<List<ReservationDto>> Requests([FromQuery] string status)
    {
        var q = _db.PartReservations.AsNoTracking()
            .Include(r => r.Part).Include(r => r.RequestedBy).Include(r => r.IssuedBy)
            .Include(r => r.Order).ThenInclude(o => o.Device).ThenInclude(d => d.DeviceType)
            .Include(r => r.Order).ThenInclude(o => o.Status)
            .AsQueryable();
        q = string.IsNullOrWhiteSpace(status) || status == "active"
            ? q.Where(r => r.Status == ReservationStatuses.Reserved || r.Status == ReservationStatuses.Requested)
            : status == "all" ? q : q.Where(r => r.Status == status);
        var list = await q.OrderByDescending(r => r.CreatedAt).Take(500).ToListAsync();
        return list.OrderBy(r => r.Status == ReservationStatuses.Reserved ? 0 : 1).ThenBy(r => r.CreatedAt)
            .Select(r => OrderService.ToReservationDto(r, r.Order)).ToList();
    }

    /// <summary>Выдача зарезервированной детали мастеру (списание под заявку)</summary>
    [HttpPost("requests/{id:int}/issue"), Authorize(Roles = Roles.Storekeeper)]
    public async Task<IActionResult> Issue(int id)
    {
        await _db.InTransactionAsync(() => _warehouse.IssueAsync(id, Me.Id));
        return NoContent();
    }

    /// <summary>Журнал движения запчастей с отбором по позиции, типу операции и периоду</summary>
    [HttpGet("movements"), Authorize(Roles = Roles.Storekeeper)]
    public async Task<List<MovementDto>> Movements([FromQuery] int? partId, [FromQuery] string type, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var q = _db.PartMovements.AsNoTracking().Include(m => m.Part).Include(m => m.User).AsQueryable();
        if (partId != null) q = q.Where(m => m.PartId == partId);
        if (!string.IsNullOrWhiteSpace(type)) q = q.Where(m => m.MovementType == type);
        if (from != null) q = q.Where(m => m.CreatedAt >= from.Value.Date);
        if (to != null) q = q.Where(m => m.CreatedAt < to.Value.Date.AddDays(1));
        var list = await q.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id).Take(1000).ToListAsync();
        return list.Select(m => new MovementDto
        {
            Id = m.Id, PartId = m.PartId, Sku = m.Part.Sku, PartName = m.Part.Name, OrderId = m.OrderId,
            Type = m.MovementType, TypeName = MovementTypes.Title(m.MovementType), Quantity = m.Quantity,
            BalanceAfter = m.BalanceAfter, UserName = m.User.ShortName, CreatedAt = m.CreatedAt, Comment = m.Comment,
        }).ToList();
    }
}

/// <summary>Инвентаризация склада</summary>
[ApiController, Authorize(Roles = Roles.Storekeeper), Route("api/inventories")]
public class InventoriesController : ApiController
{
    private readonly AppDbContext _db;
    private readonly WarehouseService _warehouse;

    public InventoriesController(AppDbContext db, WarehouseService warehouse)
    {
        _db = db;
        _warehouse = warehouse;
    }

    [HttpGet]
    public async Task<List<InventoryDto>> List()
    {
        var list = await _db.Inventories.AsNoTracking().Include(i => i.CreatedBy).OrderByDescending(i => i.CreatedAt).ToListAsync();
        return list.Select(i => new InventoryDto
        {
            Id = i.Id, CreatedAt = i.CreatedAt, CreatedBy = i.CreatedBy.ShortName, Comment = i.Comment, Discrepancies = i.Discrepancies,
        }).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<InventoryDto> Get(int id)
    {
        var i = await _db.Inventories.AsNoTracking().Include(x => x.CreatedBy).Include(x => x.Items).ThenInclude(x => x.Part)
                    .FirstOrDefaultAsync(x => x.Id == id) ?? throw BusinessException.NotFound("Инвентаризация не найдена");
        return new InventoryDto
        {
            Id = i.Id, CreatedAt = i.CreatedAt, CreatedBy = i.CreatedBy?.ShortName, Comment = i.Comment, Discrepancies = i.Discrepancies,
            Items = i.Items.OrderBy(x => x.Part.Name).Select(x => new InventoryItemDto
            {
                PartId = x.PartId, Sku = x.Part.Sku, PartName = x.Part.Name, Expected = x.ExpectedQty, Actual = x.ActualQty,
                Difference = x.ActualQty - x.ExpectedQty,
            }).ToList(),
        };
    }

    /// <summary>Проведение инвентаризации: расчёт расхождений и корректировка остатков</summary>
    [HttpPost]
    public async Task<ActionResult<InventoryDto>> Create(InventoryRequest req)
    {
        var inv = await _db.InTransactionAsync(() => _warehouse.InventoryAsync(req, Me.Id));
        return StatusCode(StatusCodes.Status201Created, await Get(inv.Id));
    }
}
