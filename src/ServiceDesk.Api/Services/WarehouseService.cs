using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Infrastructure;
using ServiceDesk.Contracts;

namespace ServiceDesk.Api.Services;

// Складской учёт: резервирование, выдача, оприходование, инвентаризация, журнал движения.
// Методы, меняющие остатки, вызываются внутри транзакции (см. Db.InTransactionAsync).
public class WarehouseService
{
    private readonly AppDbContext _db;

    public WarehouseService(AppDbContext db) => _db = db;

    // Строка запчасти с блокировкой (SELECT … FOR UPDATE) — защита от одновременного резервирования
    public async Task<Part> LockPartAsync(int partId)
    {
        var part = _db.Database.IsRelational()
            ? await _db.Parts.FromSqlInterpolated($"SELECT * FROM parts WHERE id = {partId} FOR UPDATE").FirstOrDefaultAsync()
            : await _db.Parts.FirstOrDefaultAsync(p => p.Id == partId);
        return part ?? throw BusinessException.NotFound("Запчасть не найдена");
    }

    // Резерв создаётся, если доступный остаток не меньше запрошенного; иначе запрос «ожидает поступления»
    public async Task<PartReservation> ReserveAsync(int orderId, int partId, int quantity, int userId)
    {
        if (quantity <= 0) throw BusinessException.BadRequest("Количество должно быть больше нуля");
        var part = await LockPartAsync(partId);
        if (!part.IsActive) throw BusinessException.Conflict("Позиция выведена из использования");

        var res = new PartReservation
        {
            OrderId = orderId, PartId = part.Id, Part = part, Quantity = quantity, Price = part.Price,
            RequestedById = userId, Status = ReservationStatuses.Requested,
        };
        _db.PartReservations.Add(res);

        if (part.Available >= quantity)
        {
            res.Status = ReservationStatuses.Reserved;
            part.QuantityReserved += quantity;
            AddMovement(part, orderId, MovementTypes.Reserve, 0, userId, $"Резерв {quantity} {part.Unit} под заявку № {orderId}");
        }
        await _db.SaveChangesAsync();
        return res;
    }

    public async Task CancelReservationAsync(PartReservation res, int userId)
    {
        if (res.Status == ReservationStatuses.Issued)
            throw BusinessException.Conflict("Запчасть уже выдана мастером — отменить резерв нельзя");
        if (res.Status == ReservationStatuses.Cancelled) return;
        if (res.Status == ReservationStatuses.Reserved)
        {
            var part = await LockPartAsync(res.PartId);
            part.QuantityReserved -= res.Quantity;
            AddMovement(part, res.OrderId, MovementTypes.Unreserve, 0, userId, $"Снятие резерва {res.Quantity} {part.Unit}, заявка № {res.OrderId}");
        }
        res.Status = ReservationStatuses.Cancelled;
        await _db.SaveChangesAsync();
    }

    // Выдача зарезервированной детали мастеру: списание со склада под заявку
    public async Task<PartReservation> IssueAsync(int reservationId, int userId)
    {
        var res = await _db.PartReservations.FirstOrDefaultAsync(r => r.Id == reservationId)
                  ?? throw BusinessException.NotFound("Запрос на запчасть не найден");
        if (res.Status == ReservationStatuses.Requested)
            throw BusinessException.Conflict("Запчасть ожидает поступления на склад — выдача невозможна");
        if (res.Status != ReservationStatuses.Reserved)
            throw BusinessException.Conflict("Запчасть уже выдана или резерв отменён");

        var part = await LockPartAsync(res.PartId);
        part.QuantityOnHand -= res.Quantity;
        part.QuantityReserved -= res.Quantity;
        res.Status = ReservationStatuses.Issued;
        res.IssuedById = userId;
        res.IssuedAt = DateTime.Now;
        AddMovement(part, res.OrderId, MovementTypes.Issue, -res.Quantity, userId, $"Выдано мастеру под заявку № {res.OrderId}");
        await _db.SaveChangesAsync();
        return res;
    }

    // Оприходование; поступившие детали сразу резервируются под ожидающие запросы (по очереди).
    // Возвращает номера заявок, у которых запросы перешли в резерв.
    public async Task<List<int>> ReceiptAsync(int partId, int quantity, string comment, int userId)
    {
        if (quantity <= 0) throw BusinessException.BadRequest("Количество должно быть больше нуля");
        var part = await LockPartAsync(partId);
        part.QuantityOnHand += quantity;
        AddMovement(part, null, MovementTypes.Receipt, quantity, userId, string.IsNullOrWhiteSpace(comment) ? "Оприходование" : comment.Trim());

        var waiting = await _db.PartReservations
            .Where(r => r.PartId == partId && r.Status == ReservationStatuses.Requested)
            .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id).ToListAsync();
        var orders = new List<int>();
        foreach (var r in waiting)
        {
            if (part.Available < r.Quantity) break;
            r.Status = ReservationStatuses.Reserved;
            part.QuantityReserved += r.Quantity;
            AddMovement(part, r.OrderId, MovementTypes.Reserve, 0, userId, $"Резерв {r.Quantity} {part.Unit} под заявку № {r.OrderId} (после поступления)");
            orders.Add(r.OrderId);
        }
        await _db.SaveChangesAsync();
        return orders.Distinct().ToList();
    }

    // Инвентаризация: Δ = факт − учёт; при Δ ≠ 0 учётный остаток корректируется
    public async Task<Inventory> InventoryAsync(InventoryRequest req, int userId)
    {
        if (req.Items == null || req.Items.Count == 0) throw BusinessException.BadRequest("Добавьте хотя бы одну позицию");
        if (req.Items.Select(i => i.PartId).Distinct().Count() != req.Items.Count)
            throw BusinessException.BadRequest("Позиция указана в ведомости несколько раз");

        var inv = new Inventory { CreatedById = userId, Comment = req.Comment?.Trim() };
        _db.Inventories.Add(inv);
        await _db.SaveChangesAsync();

        foreach (var line in req.Items.OrderBy(i => i.PartId))
        {
            if (line.ActualQuantity < 0) throw BusinessException.BadRequest("Фактический остаток не может быть отрицательным");
            var part = await LockPartAsync(line.PartId);
            var diff = line.ActualQuantity - part.QuantityOnHand;
            if (line.ActualQuantity < part.QuantityReserved)
                throw BusinessException.Conflict($"«{part.Name}»: фактический остаток меньше зарезервированного ({part.QuantityReserved}). Сначала снимите резервы.");
            inv.Items.Add(new InventoryItem { PartId = part.Id, Part = part, ExpectedQty = part.QuantityOnHand, ActualQty = line.ActualQuantity, Difference = diff });
            if (diff != 0)
            {
                inv.Discrepancies++;
                part.QuantityOnHand = line.ActualQuantity;
                AddMovement(part, null, MovementTypes.Inventory, diff, userId,
                    $"Инвентаризация № {inv.Id}: {(diff > 0 ? "излишек" : "недостача")}");
            }
        }
        await _db.SaveChangesAsync();
        return inv;
    }

    public async Task<Part> SavePartAsync(int? id, PartEditRequest req)
    {
        var sku = req.Sku.Trim().ToUpper();
        if (await _db.Parts.AnyAsync(p => p.Sku == sku && p.Id != (id ?? 0)))
            throw BusinessException.Conflict("Позиция с таким артикулом уже существует");
        Part part;
        if (id == null)
        {
            part = new Part();
            _db.Parts.Add(part);
        }
        else
        {
            part = await _db.Parts.FindAsync(id.Value) ?? throw BusinessException.NotFound("Запчасть не найдена");
        }
        part.Sku = sku;
        part.Name = req.Name.Trim();
        part.Unit = string.IsNullOrWhiteSpace(req.Unit) ? "шт" : req.Unit.Trim();
        part.Price = req.Price;
        part.MinQuantity = req.MinQuantity;
        part.Location = string.IsNullOrWhiteSpace(req.Location) ? null : req.Location.Trim().ToUpper();
        part.IsActive = req.IsActive;
        await _db.SaveChangesAsync();
        return part;
    }

    private void AddMovement(Part part, int? orderId, string type, int qty, int userId, string comment) =>
        _db.PartMovements.Add(new PartMovement
        {
            PartId = part.Id, OrderId = orderId, MovementType = type, Quantity = qty,
            BalanceAfter = part.QuantityOnHand, UserId = userId, Comment = comment,
        });

    public static PartDto ToDto(Part p) => new()
    {
        Id = p.Id, Sku = p.Sku, Name = p.Name, Unit = p.Unit, Price = p.Price,
        OnHand = p.QuantityOnHand, Reserved = p.QuantityReserved, Available = p.Available,
        MinQuantity = p.MinQuantity, Location = p.Location, IsActive = p.IsActive,
        BelowMinimum = p.Available < p.MinQuantity,
    };
}
