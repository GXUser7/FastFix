using System.ComponentModel.DataAnnotations;

namespace ServiceDesk.Contracts;

public class OrderListItemDto
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public string ClientName { get; set; }
    public string ClientPhone { get; set; }
    public string DeviceType { get; set; }
    public string Device { get; set; }
    public string Status { get; set; }
    public string StatusName { get; set; }
    public string StatusColor { get; set; }
    public string MasterName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime DueDate { get; set; }
    public bool IsOverdue { get; set; }
    public bool IsWarranty { get; set; }
    public decimal TotalCost { get; set; }
    public decimal Paid { get; set; }
}

public class DeviceDto
{
    public int Id { get; set; }
    public int TypeId { get; set; }
    public string TypeName { get; set; }
    public string Brand { get; set; }
    public string Model { get; set; }
    public string SerialNumber { get; set; }
    public string Title { get; set; }
}

public class OrderDetailsDto
{
    public int Id { get; set; }
    public string Status { get; set; }
    public string StatusName { get; set; }
    public string StatusColor { get; set; }
    public ClientDto Client { get; set; }
    public DeviceDto Device { get; set; }
    public string ReceptionistName { get; set; }
    public int? MasterId { get; set; }
    public string MasterName { get; set; }
    public string DeclaredFault { get; set; }
    public string AppearanceNote { get; set; }
    public string Diagnosis { get; set; }
    public bool IsWarranty { get; set; }
    public int? WarrantyMonths { get; set; }
    public string ClientDecision { get; set; }
    public DateTime? DecisionAt { get; set; }
    public decimal WorksTotal { get; set; }
    public decimal PartsTotal { get; set; }
    public decimal TotalCost { get; set; }
    public decimal Paid { get; set; }
    public decimal Due { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? ReadyAt { get; set; }
    public DateTime? IssuedAt { get; set; }
    public bool IsOverdue { get; set; }
    public List<int> CompletenessIds { get; set; } = new();
    public List<string> Completeness { get; set; } = new();
    public List<WorkDto> Works { get; set; } = new();
    public List<ReservationDto> Parts { get; set; } = new();
    public List<HistoryDto> History { get; set; } = new();
    public List<PaymentDto> Payments { get; set; } = new();
    public List<DocumentDto> Documents { get; set; } = new();

    // Права текущего пользователя в отношении заявки
    public List<string> AllowedStatuses { get; set; } = new();
    public bool CanEditRepair { get; set; }
    public bool CanDecide { get; set; }
    public bool CanPay { get; set; }
    public bool CanIssue { get; set; }
    public bool CanAssignMaster { get; set; }
}

public class WorkDto
{
    public int Id { get; set; }
    public int? ServiceId { get; set; }
    public string Description { get; set; }
    public decimal Price { get; set; }
    public string MasterName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ReservationDto
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string OrderDevice { get; set; }
    public string OrderStatusName { get; set; }
    public int PartId { get; set; }
    public string Sku { get; set; }
    public string PartName { get; set; }
    public string Location { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Sum { get; set; }
    public string Status { get; set; }
    public string StatusName { get; set; }
    public string RequestedBy { get; set; }
    public string IssuedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? IssuedAt { get; set; }
    public int Available { get; set; }
}

public class HistoryDto
{
    public string Status { get; set; }
    public string StatusName { get; set; }
    public string StatusColor { get; set; }
    public string ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; }
    public string Comment { get; set; }
}

public class PaymentDto
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; }
    public string MethodName { get; set; }
    public string ReceivedBy { get; set; }
    public DateTime PaidAt { get; set; }
}

public class DocumentDto
{
    public string Type { get; set; }
    public string Name { get; set; }
    public string Number { get; set; }
    public DateTime? CreatedAt { get; set; }
    public bool Available { get; set; }
}

public class CreateOrderRequest
{
    public int? ClientId { get; set; }

    [Required(ErrorMessage = "Укажите телефон клиента")]
    public string ClientPhone { get; set; }

    [StringLength(60)] public string ClientLastName { get; set; }
    [StringLength(60)] public string ClientFirstName { get; set; }
    [StringLength(60)] public string ClientMiddleName { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Выберите тип устройства")]
    public int DeviceTypeId { get; set; }

    [Required(ErrorMessage = "Укажите производителя"), StringLength(60)]
    public string Brand { get; set; }

    [Required(ErrorMessage = "Укажите модель"), StringLength(100)]
    public string Model { get; set; }

    [StringLength(60)] public string SerialNumber { get; set; }

    [Required(ErrorMessage = "Опишите неисправность"), StringLength(500)]
    public string DeclaredFault { get; set; }

    [StringLength(300)] public string AppearanceNote { get; set; }

    public List<int> CompletenessIds { get; set; } = new();
    public int? MasterId { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsWarranty { get; set; }
}

public class CreateOrderResponse
{
    public int Id { get; set; }
}

public class ChangeStatusRequest
{
    [Required(ErrorMessage = "Укажите статус")]
    public string Status { get; set; }

    [StringLength(300)] public string Comment { get; set; }
}

public class AssignMasterRequest
{
    public int MasterId { get; set; }
}

public class DiagnosisRequest
{
    [Required(ErrorMessage = "Заполните заключение мастера")]
    public string Diagnosis { get; set; }

    [Range(0, 36, ErrorMessage = "Гарантия — от 0 до 36 месяцев")]
    public int? WarrantyMonths { get; set; }
}

public class AddWorkRequest
{
    public int? ServiceId { get; set; }

    [StringLength(300)] public string Description { get; set; }

    [Range(0, 1_000_000, ErrorMessage = "Некорректная стоимость")]
    public decimal? Price { get; set; }
}

public class AddPartRequest
{
    public int PartId { get; set; }

    [Range(1, 1000, ErrorMessage = "Количество — от 1 до 1000")]
    public int Quantity { get; set; } = 1;
}

public class DecisionRequest
{
    public bool Approve { get; set; }

    [StringLength(300)] public string Comment { get; set; }
}

public class PaymentRequest
{
    [Range(0.01, 10_000_000, ErrorMessage = "Сумма должна быть больше нуля")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "Выберите способ оплаты")]
    public string Method { get; set; }
}

// Событие SignalR «заявка изменилась»
public class OrderUpdatedEvent
{
    public int OrderId { get; set; }
    public string Status { get; set; }
    public string StatusName { get; set; }
    public string Message { get; set; }
}
