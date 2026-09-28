using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Hubs;
using ServiceDesk.Api.Infrastructure;
using ServiceDesk.Contracts;
using S = ServiceDesk.Contracts.OrderStatuses;

namespace ServiceDesk.Api.Services;

public class OrderQuery
{
    public string Search { get; set; }
    public string Status { get; set; }
    public bool? Active { get; set; }
    public bool? Overdue { get; set; }
}

// Сценарии работы с заявкой: оформление, жизненный цикл, работы, запчасти, согласование, оплата, выдача
public class OrderService
{
    // статусы, в которых мастер ведёт диагностику, журнал работ и запрашивает запчасти
    private static readonly string[] RepairStatuses = { S.Diagnostics, S.WaitingParts, S.InWork };

    private readonly AppDbContext _db;
    private readonly WarehouseService _warehouse;
    private readonly INotifier _notifier;
    private readonly DocumentRegistry _registry;

    public OrderService(AppDbContext db, WarehouseService warehouse, INotifier notifier, DocumentRegistry registry)
    {
        _db = db;
        _warehouse = warehouse;
        _notifier = notifier;
        _registry = registry;
    }

    // ------------------------------------------------------------------ чтение

    public async Task<List<OrderListItemDto>> ListAsync(CurrentUser user, OrderQuery q)
    {
        var orders = _db.Orders.AsNoTracking().AsQueryable();
        orders = user.Role switch
        {
            Roles.Client => orders.Where(o => o.ClientId == user.Id),
            Roles.Master => orders.Where(o => o.MasterId == user.Id),
            Roles.Receptionist => orders,
            _ => throw BusinessException.Forbidden(),
        };

        if (!string.IsNullOrWhiteSpace(q.Status)) orders = orders.Where(o => o.Status.Code == q.Status);
        if (q.Active == true) orders = orders.Where(o => o.Status.Code != S.Issued && o.Status.Code != S.Cancelled);
        if (q.Active == false) orders = orders.Where(o => o.Status.Code == S.Issued || o.Status.Code == S.Cancelled);
        var today = DateTime.Today;
        if (q.Overdue == true) orders = orders.Where(o => o.DueDate < today && o.Status.Code != S.Issued && o.Status.Code != S.Cancelled);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            var digits = Phone.SearchDigits(s);
            int.TryParse(s.TrimStart('№', ' '), out var id);
            orders = orders.Where(o => o.Id == id
                                       || (digits.Length >= 4 && o.Client.Phone.Contains(digits))
                                       || o.Client.LastName.Contains(s)
                                       || o.Device.Brand.Contains(s)
                                       || o.Device.Model.Contains(s)
                                       || (o.Device.SerialNumber != null && o.Device.SerialNumber.Contains(s)));
        }

        var rows = await orders
            .OrderByDescending(o => o.CreatedAt)
            .Take(500)
            .Select(o => new
            {
                o.Id, o.ClientId, o.Client.LastName, o.Client.FirstName, o.Client.MiddleName, o.Client.Phone,
                DeviceType = o.Device.DeviceType.Name, o.Device.Brand, o.Device.Model,
                o.Status.Code, StatusName = o.Status.Name, o.Status.Color,
                MLast = o.Master.LastName, MFirst = o.Master.FirstName, MMiddle = o.Master.MiddleName,
                o.CreatedAt, o.DueDate, o.IsWarranty, o.TotalCost,
                Paid = o.Payments.Sum(p => (decimal?)p.Amount) ?? 0,
            })
            .ToListAsync();

        return rows.Select(r => new OrderListItemDto
        {
            Id = r.Id,
            ClientId = r.ClientId,
            ClientName = new User { LastName = r.LastName, FirstName = r.FirstName, MiddleName = r.MiddleName }.ShortName,
            ClientPhone = Phone.Format(r.Phone),
            DeviceType = r.DeviceType,
            Device = $"{r.Brand} {r.Model}",
            Status = r.Code,
            StatusName = r.StatusName,
            StatusColor = r.Color,
            MasterName = r.MLast == null ? null : new User { LastName = r.MLast, FirstName = r.MFirst, MiddleName = r.MMiddle }.ShortName,
            CreatedAt = r.CreatedAt,
            DueDate = r.DueDate,
            IsOverdue = OrderWorkflow.IsOverdue(r.Code, r.DueDate, today),
            IsWarranty = r.IsWarranty,
            TotalCost = r.TotalCost,
            Paid = r.Paid,
        }).ToList();
    }

    public async Task<Order> LoadAsync(int id, CurrentUser user)
    {
        var o = await _db.Orders
            .Include(x => x.Client)
            .Include(x => x.Device).ThenInclude(d => d.DeviceType)
            .Include(x => x.Receptionist)
            .Include(x => x.Master)
            .Include(x => x.Status)
            .Include(x => x.Completeness).ThenInclude(c => c.Item)
            .Include(x => x.History).ThenInclude(h => h.Status)
            .Include(x => x.History).ThenInclude(h => h.ChangedBy)
            .Include(x => x.Works).ThenInclude(w => w.Master)
            .Include(x => x.Reservations).ThenInclude(r => r.Part)
            .Include(x => x.Reservations).ThenInclude(r => r.RequestedBy)
            .Include(x => x.Reservations).ThenInclude(r => r.IssuedBy)
            .Include(x => x.Payments).ThenInclude(p => p.ReceivedBy)
            .Include(x => x.Documents)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id)
            ?? throw BusinessException.NotFound("Заявка не найдена");
        EnsureAccess(o, user);
        return o;
    }

    public static void EnsureAccess(Order o, CurrentUser user)
    {
        switch (user.Role)
        {
            case Roles.Receptionist:
                return;
            case Roles.Client when o.ClientId == user.Id:
                return;
            case Roles.Client:
                throw BusinessException.NotFound("Заявка не найдена");
            case Roles.Master when o.MasterId == user.Id:
                return;
            case Roles.Master:
                throw BusinessException.Forbidden("Заявка назначена другому мастеру");
            default:
                throw BusinessException.Forbidden();
        }
    }

    public async Task<OrderDetailsDto> GetAsync(int id, CurrentUser user) => ToDetails(await LoadAsync(id, user), user);

    public OrderDetailsDto ToDetails(Order o, CurrentUser user)
    {
        var worksTotal = CostCalculator.WorksTotal(o.Works.Select(w => w.Price));
        var partsTotal = CostCalculator.PartsTotal(o.Reservations.Select(Line));
        var paid = o.Payments.Sum(p => p.Amount);
        var due = CostCalculator.Due(o.TotalCost, o.Payments.Select(p => p.Amount));
        var status = o.Status.Code;
        var isMaster = user.Role == Roles.Master && o.MasterId == user.Id;
        var isReceptionist = user.Role == Roles.Receptionist;

        var dto = new OrderDetailsDto
        {
            Id = o.Id,
            Status = status,
            StatusName = o.Status.Name,
            StatusColor = o.Status.Color,
            Client = ToClientDto(o.Client),
            Device = new DeviceDto
            {
                Id = o.Device.Id, TypeId = o.Device.DeviceTypeId, TypeName = o.Device.DeviceType.Name,
                Brand = o.Device.Brand, Model = o.Device.Model, SerialNumber = o.Device.SerialNumber, Title = o.Device.Title,
            },
            ReceptionistName = o.Receptionist.ShortName,
            MasterId = o.MasterId,
            MasterName = o.Master?.ShortName,
            DeclaredFault = o.DeclaredFault,
            AppearanceNote = o.AppearanceNote,
            Diagnosis = o.Diagnosis,
            IsWarranty = o.IsWarranty,
            WarrantyMonths = o.WarrantyMonths,
            ClientDecision = o.ClientDecision,
            DecisionAt = o.DecisionAt,
            WorksTotal = worksTotal,
            PartsTotal = partsTotal,
            TotalCost = o.TotalCost,
            Paid = paid,
            Due = due,
            CreatedAt = o.CreatedAt,
            DueDate = o.DueDate,
            ReadyAt = o.ReadyAt,
            IssuedAt = o.IssuedAt,
            IsOverdue = OrderWorkflow.IsOverdue(status, o.DueDate, DateTime.Today),
            CompletenessIds = o.Completeness.Select(c => c.ItemId).ToList(),
            Completeness = o.Completeness.OrderBy(c => c.ItemId).Select(c => c.Item.Name).ToList(),
            Works = o.Works.OrderBy(w => w.CreatedAt).ThenBy(w => w.Id).Select(w => new WorkDto
            {
                Id = w.Id, ServiceId = w.ServiceId, Description = w.Description, Price = w.Price,
                MasterName = w.Master?.ShortName, CreatedAt = w.CreatedAt,
            }).ToList(),
            Parts = o.Reservations.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id).Select(r => ToReservationDto(r, o)).ToList(),
            History = o.History.OrderBy(h => h.ChangedAt).ThenBy(h => h.Id).Select(h => new HistoryDto
            {
                Status = h.Status.Code, StatusName = h.Status.Name, StatusColor = h.Status.Color,
                ChangedBy = h.ChangedBy.ShortName, ChangedAt = h.ChangedAt, Comment = h.Comment,
            }).ToList(),
            Payments = o.Payments.OrderBy(p => p.PaidAt).Select(p => new PaymentDto
            {
                Id = p.Id, Amount = p.Amount, Method = p.Method, MethodName = PaymentMethods.Title(p.Method),
                ReceivedBy = p.ReceivedBy?.ShortName, PaidAt = p.PaidAt,
            }).ToList(),
            Documents = DocumentTypes.All.Where(t => DocumentRegistry.RoleCanRead(user.Role, t)).Select(t =>
            {
                var d = o.Documents.FirstOrDefault(x => x.DocType == t);
                return new DocumentDto
                {
                    Type = t, Name = DocumentTypes.Title(t), Number = d?.Number ?? DocumentRegistry.NumberFor(t, o.Id),
                    CreatedAt = d?.CreatedAt, Available = DocumentRegistry.IsAvailable(o, t),
                };
            }).ToList(),
        };

        if (isMaster)
            dto.AllowedStatuses = OrderWorkflow.Allowed(status, Roles.Master, o.IsWarranty);
        else if (isReceptionist)
            dto.AllowedStatuses = OrderWorkflow.Allowed(status, Roles.Receptionist, o.IsWarranty).Where(s => s != S.Issued).ToList();

        dto.CanEditRepair = isMaster && RepairStatuses.Contains(status);
        dto.CanDecide = user.Role == Roles.Client && o.ClientId == user.Id && status == S.Approval;
        dto.CanPay = isReceptionist && !o.IsWarranty && due > 0 && status != S.Issued && status != S.Cancelled;
        dto.CanIssue = isReceptionist && (status == S.Cancelled || (status == S.Ready && CostCalculator.CanIssue(o.TotalCost, paid, o.IsWarranty)));
        dto.CanAssignMaster = isReceptionist && status is S.Accepted or S.Diagnostics or S.Approval or S.WaitingParts or S.InWork;
        return dto;
    }

    // ------------------------------------------------------------------ приём заявок

    public async Task<int> CreateAsync(CreateOrderRequest req, CurrentUser user)
    {
        var phone = Phone.Normalize(req.ClientPhone) ?? throw BusinessException.BadRequest("Телефон клиента должен быть в формате +7XXXXXXXXXX");
        if (!await _db.DeviceTypes.AnyAsync(t => t.Id == req.DeviceTypeId)) throw BusinessException.BadRequest("Выберите тип устройства");
        if (req.MasterId != null) await EnsureMasterAsync(req.MasterId.Value);
        var due = (req.DueDate ?? DateTime.Today.AddDays(7)).Date;
        if (due < DateTime.Today) throw BusinessException.BadRequest("Плановый срок не может быть в прошлом");
        var itemIds = (req.CompletenessIds ?? new()).Distinct().ToList();
        if (itemIds.Count > 0 && await _db.CompletenessItems.CountAsync(i => itemIds.Contains(i.Id)) != itemIds.Count)
            throw BusinessException.BadRequest("Неизвестный пункт комплектности");

        var order = await _db.InTransactionAsync(async () =>
        {
            var client = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Phone == phone);
            if (client != null && client.Role.Code != Roles.Client)
                throw BusinessException.Conflict("Телефон принадлежит сотруднику сервисного центра");
            if (client == null)
            {
                // быстрая регистрация клиента приёмщиком (без пароля — клиент может зарегистрироваться сам позже)
                if (string.IsNullOrWhiteSpace(req.ClientLastName) || string.IsNullOrWhiteSpace(req.ClientFirstName))
                    throw BusinessException.BadRequest("Клиент не найден — укажите фамилию и имя для регистрации");
                client = new User
                {
                    RoleId = await _db.Roles.Where(r => r.Code == Roles.Client).Select(r => r.Id).FirstAsync(),
                    Phone = phone,
                    LastName = req.ClientLastName.Trim(),
                    FirstName = req.ClientFirstName.Trim(),
                    MiddleName = string.IsNullOrWhiteSpace(req.ClientMiddleName) ? null : req.ClientMiddleName.Trim(),
                };
                _db.Users.Add(client);
                await _db.SaveChangesAsync();
            }

            var serial = string.IsNullOrWhiteSpace(req.SerialNumber) ? null : req.SerialNumber.Trim();
            var device = serial == null ? null : await _db.Devices.FirstOrDefaultAsync(d => d.ClientId == client.Id && d.SerialNumber == serial);
            if (device == null)
            {
                device = new Device { ClientId = client.Id, SerialNumber = serial };
                _db.Devices.Add(device);
            }
            device.DeviceTypeId = req.DeviceTypeId;
            device.Brand = req.Brand.Trim();
            device.Model = req.Model.Trim();

            var o = new Order
            {
                ClientId = client.Id,
                Device = device,
                ReceptionistId = user.Id,
                MasterId = req.MasterId,
                StatusId = await StatusIdAsync(S.Accepted),
                DeclaredFault = req.DeclaredFault.Trim(),
                AppearanceNote = string.IsNullOrWhiteSpace(req.AppearanceNote) ? null : req.AppearanceNote.Trim(),
                IsWarranty = req.IsWarranty,
                DueDate = due,
            };
            foreach (var itemId in itemIds) o.Completeness.Add(new OrderCompleteness { ItemId = itemId });
            _db.Orders.Add(o);
            await _db.SaveChangesAsync();

            _db.OrderStatusHistory.Add(new OrderStatusHistory { OrderId = o.Id, StatusId = o.StatusId, ChangedById = user.Id, Comment = "Заявка оформлена" });
            _registry.Register(o.Id, DocumentTypes.Receipt, user.Id);
            await _db.SaveChangesAsync();
            return o;
        });

        await Notify(order.Id, "Заявка принята в работу");
        return order.Id;
    }

    public async Task AssignMasterAsync(int id, int masterId, CurrentUser user)
    {
        var o = await LoadAsync(id, user);
        if (!(o.Status.Code is S.Accepted or S.Diagnostics or S.Approval or S.WaitingParts or S.InWork))
            throw BusinessException.Conflict("Назначить мастера можно только до готовности заявки");
        var master = await EnsureMasterAsync(masterId);
        if (o.MasterId == masterId) return;
        o.MasterId = masterId;
        _db.OrderStatusHistory.Add(new OrderStatusHistory { OrderId = o.Id, StatusId = o.StatusId, ChangedById = user.Id, Comment = $"Назначен мастер: {master.ShortName}" });
        await _db.SaveChangesAsync();
        await Notify(o.Id, $"Назначен мастер {master.ShortName}");
    }

    // ------------------------------------------------------------------ жизненный цикл

    public async Task ChangeStatusAsync(int id, ChangeStatusRequest req, CurrentUser user)
    {
        if (!OrderWorkflow.IsKnown(req.Status)) throw BusinessException.BadRequest("Неизвестный статус");
        if (req.Status == S.Issued)
        {
            await IssueAsync(id, user);
            return;
        }
        var o = await LoadAsync(id, user);
        var from = o.Status.Code;
        var to = req.Status;

        if (!OrderWorkflow.CanChange(from, to, user.Role, o.IsWarranty))
            throw BusinessException.Conflict($"Переход «{o.Status.Name}» → «{await StatusNameAsync(to)}» недоступен для роли «{Roles.Title(user.Role)}»");

        // бизнес-условия переходов
        switch (to)
        {
            case S.Approval:
                if (string.IsNullOrWhiteSpace(o.Diagnosis)) throw BusinessException.Conflict("Заполните заключение мастера перед отправкой сметы");
                if (o.Works.Count == 0) throw BusinessException.Conflict("Добавьте в смету хотя бы одну работу");
                break;
            case S.InWork when from == S.Diagnostics:
                if (string.IsNullOrWhiteSpace(o.Diagnosis)) throw BusinessException.Conflict("Заполните заключение мастера");
                break;
            case S.InWork when from == S.WaitingParts:
                if (o.Reservations.Any(r => r.Status == ReservationStatuses.Requested))
                    throw BusinessException.Conflict("Не все запчасти поступили на склад");
                break;
            case S.Ready:
                if (o.Reservations.Any(r => r.Status is ReservationStatuses.Requested or ReservationStatuses.Reserved))
                    throw BusinessException.Conflict("Не все запчасти получены со склада — выдайте их или отмените резерв");
                if (o.Works.Count == 0) throw BusinessException.Conflict("Журнал работ пуст");
                break;
        }

        await _db.InTransactionAsync(async () =>
        {
            if (to == S.Cancelled) await ReleaseReservationsAsync(o, user.Id);
            await SetStatusAsync(o, to, user.Id, req.Comment);
            if (to == S.Approval) _registry.Register(o.Id, DocumentTypes.Estimate, user.Id);
            if (to == S.Ready)
            {
                o.ReadyAt = DateTime.Now;
                _registry.Register(o.Id, DocumentTypes.Act, user.Id);
            }
            await _db.SaveChangesAsync();
        });
        await Notify(o.Id, $"Статус заявки: {o.Status.Name}");
    }

    // Согласование или отказ клиента
    public async Task DecideAsync(int id, DecisionRequest req, CurrentUser user)
    {
        var o = await LoadAsync(id, user);
        if (o.Status.Code != S.Approval) throw BusinessException.Conflict("Заявка не ожидает согласования стоимости");

        await _db.InTransactionAsync(async () =>
        {
            o.ClientDecision = req.Approve ? "approved" : "rejected";
            o.DecisionAt = DateTime.Now;
            if (req.Approve)
            {
                var waiting = o.Reservations.Any(r => r.Status == ReservationStatuses.Requested);
                var comment = "Клиент согласовал стоимость" + (string.IsNullOrWhiteSpace(req.Comment) ? "" : ": " + req.Comment.Trim());
                await SetStatusAsync(o, waiting ? S.WaitingParts : S.InWork, user.Id, comment);
            }
            else
            {
                await ReleaseReservationsAsync(o, user.Id);
                var comment = "Клиент отказался от ремонта" + (string.IsNullOrWhiteSpace(req.Comment) ? "" : ": " + req.Comment.Trim());
                await SetStatusAsync(o, S.Cancelled, user.Id, comment);
            }
            await _db.SaveChangesAsync();
        });
        await Notify(o.Id, req.Approve ? "Клиент согласовал стоимость" : "Клиент отказался от ремонта");
    }

    // ------------------------------------------------------------------ ремонт

    public async Task SaveDiagnosisAsync(int id, DiagnosisRequest req, CurrentUser user)
    {
        var o = await LoadForRepairAsync(id, user);
        o.Diagnosis = req.Diagnosis.Trim();
        o.WarrantyMonths = req.WarrantyMonths;
        await _db.SaveChangesAsync();
        await Notify(o.Id, "Обновлено заключение мастера");
    }

    public async Task<int> AddWorkAsync(int id, AddWorkRequest req, CurrentUser user)
    {
        var o = await LoadForRepairAsync(id, user);
        var work = new OrderWork { OrderId = o.Id, MasterId = user.Id };
        if (req.ServiceId != null)
        {
            var svc = await _db.Services.FirstOrDefaultAsync(s => s.Id == req.ServiceId && s.IsActive)
                      ?? throw BusinessException.BadRequest("Услуга не найдена в прайс-листе");
            work.ServiceId = svc.Id;
            work.Description = string.IsNullOrWhiteSpace(req.Description) ? svc.Name : req.Description.Trim();
            work.Price = req.Price ?? svc.Price;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(req.Description)) throw BusinessException.BadRequest("Опишите выполненную работу");
            if (req.Price == null) throw BusinessException.BadRequest("Укажите стоимость работы");
            work.Description = req.Description.Trim();
            work.Price = req.Price.Value;
        }
        if (work.Price < 0) throw BusinessException.BadRequest("Некорректная стоимость");
        o.Works.Add(work);
        Recalculate(o);
        await _db.SaveChangesAsync();
        await Notify(o.Id, "Обновлён журнал работ");
        return work.Id;
    }

    public async Task DeleteWorkAsync(int id, int workId, CurrentUser user)
    {
        var o = await LoadForRepairAsync(id, user);
        var work = o.Works.FirstOrDefault(w => w.Id == workId) ?? throw BusinessException.NotFound("Работа не найдена");
        _db.OrderWorks.Remove(work);
        o.Works.Remove(work);
        Recalculate(o);
        await _db.SaveChangesAsync();
        await Notify(o.Id, "Обновлён журнал работ");
    }

    public async Task<ReservationDto> AddPartAsync(int id, AddPartRequest req, CurrentUser user)
    {
        var o = await LoadForRepairAsync(id, user);
        var res = await _db.InTransactionAsync(async () =>
        {
            var r = await _warehouse.ReserveAsync(o.Id, req.PartId, req.Quantity, user.Id);
            // EF уже добавил резерв в коллекцию отслеживаемой заявки (fix-up связей)
            if (!o.Reservations.Contains(r)) o.Reservations.Add(r);
            Recalculate(o);
            // нехватка детали во время ремонта — заявка ожидает поступления
            if (r.Status == ReservationStatuses.Requested && o.Status.Code == S.InWork)
                await SetStatusAsync(o, S.WaitingParts, user.Id, $"Нет на складе: {r.Part.Name}");
            await _db.SaveChangesAsync();
            return r;
        });
        await Notify(o.Id, res.Status == ReservationStatuses.Reserved ? "Запчасть зарезервирована" : "Запчасть ожидает поступления");
        return ToReservationDto(res, o);
    }

    public async Task DeletePartAsync(int id, int reservationId, CurrentUser user)
    {
        var o = await LoadForRepairAsync(id, user);
        var r = o.Reservations.FirstOrDefault(x => x.Id == reservationId) ?? throw BusinessException.NotFound("Резерв не найден");
        await _db.InTransactionAsync(async () =>
        {
            await _warehouse.CancelReservationAsync(r, user.Id);
            Recalculate(o);
            await _db.SaveChangesAsync();
        });
        await Notify(o.Id, "Резерв запчасти отменён");
    }

    // Вызывается после оприходования: заявки, у которых все детали поступили, возвращаются в работу
    public async Task OnPartsArrivedAsync(IEnumerable<int> orderIds, int userId)
    {
        foreach (var orderId in orderIds)
        {
            var o = await _db.Orders.Include(x => x.Status).Include(x => x.Reservations).FirstAsync(x => x.Id == orderId);
            if (o.Status.Code == S.WaitingParts && o.Reservations.All(r => r.Status != ReservationStatuses.Requested))
            {
                await SetStatusAsync(o, S.InWork, userId, "Запчасти поступили на склад");
                await _db.SaveChangesAsync();
            }
            await Notify(o.Id, "Запчасти поступили на склад");
        }
    }

    // ------------------------------------------------------------------ расчёты

    public async Task PayAsync(int id, PaymentRequest req, CurrentUser user)
    {
        if (!PaymentMethods.All.Contains(req.Method)) throw BusinessException.BadRequest("Неизвестный способ оплаты");
        var o = await LoadAsync(id, user);
        if (o.Status.Code is S.Issued or S.Cancelled) throw BusinessException.Conflict("Заявка закрыта — приём оплаты невозможен");
        if (o.IsWarranty) throw BusinessException.Conflict("Гарантийный ремонт оплате не подлежит");
        await _db.InTransactionAsync(async () =>
        {
            var due = CostCalculator.Due(o.TotalCost, o.Payments.Select(p => p.Amount));
            if (due <= 0) throw BusinessException.Conflict("Заявка полностью оплачена");
            if (req.Amount > due) throw BusinessException.Conflict($"Сумма превышает остаток к оплате ({due:N2} ₽)");
            o.Payments.Add(new Payment { OrderId = o.Id, Amount = Math.Round(req.Amount, 2), Method = req.Method, ReceivedById = user.Id });
            await _db.SaveChangesAsync();
        });
        await Notify(o.Id, $"Принята оплата {req.Amount:N2} ₽");
    }

    public async Task IssueAsync(int id, CurrentUser user)
    {
        if (user.Role != Roles.Receptionist) throw BusinessException.Forbidden("Выдачу устройства выполняет приёмщик");
        var o = await LoadAsync(id, user);
        var from = o.Status.Code;
        if (!OrderWorkflow.CanChange(from, S.Issued, user.Role, o.IsWarranty))
            throw BusinessException.Conflict("Выдать можно только заявку в статусе «Готова к выдаче» или «Отменена»");
        var paid = o.Payments.Sum(p => p.Amount);
        if (from == S.Ready && !CostCalculator.CanIssue(o.TotalCost, paid, o.IsWarranty))
            throw BusinessException.Conflict($"Заявка не оплачена полностью. Остаток к оплате: {o.TotalCost - paid:N2} ₽");

        await _db.InTransactionAsync(async () =>
        {
            o.IssuedAt = DateTime.Now;
            await SetStatusAsync(o, S.Issued, user.Id, from == S.Cancelled ? "Возврат устройства клиенту без ремонта" : "Устройство выдано клиенту");
            _registry.Register(o.Id, DocumentTypes.Issue, user.Id);
            await _db.SaveChangesAsync();
        });
        await Notify(o.Id, "Устройство выдано клиенту");
    }

    // ------------------------------------------------------------------ вспомогательное

    private async Task<Order> LoadForRepairAsync(int id, CurrentUser user)
    {
        if (user.Role != Roles.Master) throw BusinessException.Forbidden("Операция доступна только мастеру");
        var o = await LoadAsync(id, user);
        if (!RepairStatuses.Contains(o.Status.Code))
            throw BusinessException.Conflict($"В статусе «{o.Status.Name}» изменение ремонта недоступно");
        return o;
    }

    private async Task ReleaseReservationsAsync(Order o, int userId)
    {
        foreach (var r in o.Reservations.Where(r => r.Status is ReservationStatuses.Requested or ReservationStatuses.Reserved).ToList())
            await _warehouse.CancelReservationAsync(r, userId);
        Recalculate(o);
    }

    private async Task SetStatusAsync(Order o, string code, int userId, string comment)
    {
        var st = await _db.OrderStatuses.FirstAsync(s => s.Code == code);
        o.StatusId = st.Id;
        o.Status = st;
        o.UpdatedAt = DateTime.Now;
        var h = new OrderStatusHistory { OrderId = o.Id, StatusId = st.Id, Status = st, ChangedById = userId, Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim() };
        _db.OrderStatusHistory.Add(h);
    }

    public static void Recalculate(Order o) =>
        o.TotalCost = CostCalculator.Total(o.Works.Select(w => w.Price), o.Reservations.Select(Line), o.IsWarranty);

    private static CostCalculator.PartLine Line(PartReservation r) => new(r.Price, r.Quantity, r.Status);

    private async Task<User> EnsureMasterAsync(int masterId)
    {
        var m = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == masterId && u.IsActive);
        if (m == null || m.Role.Code != Roles.Master) throw BusinessException.BadRequest("Выбранный сотрудник не является мастером");
        return m;
    }

    private Task<int> StatusIdAsync(string code) => _db.OrderStatuses.Where(s => s.Code == code).Select(s => s.Id).FirstAsync();

    private async Task<string> StatusNameAsync(string code) =>
        await _db.OrderStatuses.Where(s => s.Code == code).Select(s => s.Name).FirstOrDefaultAsync() ?? code;

    private async Task Notify(int orderId, string message)
    {
        var info = await _db.Orders.AsNoTracking().Where(o => o.Id == orderId)
            .Select(o => new { o.ClientId, o.Status.Code, o.Status.Name }).FirstAsync();
        await _notifier.OrderUpdatedAsync(info.ClientId, new OrderUpdatedEvent { OrderId = orderId, Status = info.Code, StatusName = info.Name, Message = message });
    }

    public static ClientDto ToClientDto(User u) => new()
    {
        Id = u.Id, LastName = u.LastName, FirstName = u.FirstName, MiddleName = u.MiddleName,
        Phone = u.Phone, Email = u.Email, IsRegistered = u.PasswordHash != null, FullName = u.FullName,
    };

    public static ReservationDto ToReservationDto(PartReservation r, Order o) => new()
    {
        Id = r.Id,
        OrderId = r.OrderId,
        OrderDevice = o?.Device?.Title,
        OrderStatusName = o?.Status?.Name,
        PartId = r.PartId,
        Sku = r.Part?.Sku,
        PartName = r.Part?.Name,
        Location = r.Part?.Location,
        Quantity = r.Quantity,
        Price = r.Price,
        Sum = r.Price * r.Quantity,
        Status = r.Status,
        StatusName = ReservationStatuses.Title(r.Status),
        RequestedBy = r.RequestedBy?.ShortName,
        IssuedBy = r.IssuedBy?.ShortName,
        CreatedAt = r.CreatedAt,
        IssuedAt = r.IssuedAt,
        Available = r.Part?.Available ?? 0,
    };
}
