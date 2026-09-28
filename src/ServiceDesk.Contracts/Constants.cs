namespace ServiceDesk.Contracts;

// Коды ролей (совпадают с roles.code в БД)
public static class Roles
{
    public const string Client = "client";
    public const string Receptionist = "receptionist";
    public const string Master = "master";
    public const string Storekeeper = "storekeeper";

    public const string Staff = Receptionist + "," + Master + "," + Storekeeper;

    public static string Title(string role) => role switch
    {
        Client => "Клиент",
        Receptionist => "Приёмщик",
        Master => "Мастер",
        Storekeeper => "Кладовщик",
        _ => role
    };
}

// Коды статусов заявки (совпадают с order_statuses.code)
public static class OrderStatuses
{
    public const string Accepted = "accepted";
    public const string Diagnostics = "diagnostics";
    public const string Approval = "approval";
    public const string WaitingParts = "waiting_parts";
    public const string InWork = "in_work";
    public const string Ready = "ready";
    public const string Issued = "issued";
    public const string Cancelled = "cancelled";

    // Этапы шкалы прогресса в карточке заявки
    public static readonly string[] Steps = { Accepted, Diagnostics, Approval, InWork, Ready, Issued };

    public const string OverdueColor = "#C0392B";
}

public static class DocumentTypes
{
    public const string Receipt = "receipt";
    public const string Estimate = "estimate";
    public const string Act = "act";
    public const string Issue = "issue";

    public static readonly string[] All = { Receipt, Estimate, Act, Issue };

    public static string Title(string type) => type switch
    {
        Receipt => "Квитанция о приёме",
        Estimate => "Смета",
        Act => "Акт выполненных работ",
        Issue => "Акт выдачи",
        _ => type
    };

    public static string Prefix(string type) => type switch
    {
        Receipt => "КВ",
        Estimate => "СМ",
        Act => "АВР",
        Issue => "АВ",
        _ => "ДОК"
    };
}

public static class PaymentMethods
{
    public const string Cash = "cash";
    public const string Card = "card";
    public const string Sbp = "sbp";

    public static readonly string[] All = { Cash, Card, Sbp };

    public static string Title(string method) => method switch
    {
        Cash => "Наличные",
        Card => "Банковская карта",
        Sbp => "СБП",
        _ => method
    };
}

public static class ReservationStatuses
{
    public const string Requested = "requested";
    public const string Reserved = "reserved";
    public const string Issued = "issued";
    public const string Cancelled = "cancelled";

    public static string Title(string status) => status switch
    {
        Requested => "Ожидает поступления",
        Reserved => "Зарезервирована",
        Issued => "Выдана мастеру",
        Cancelled => "Отменена",
        _ => status
    };
}

public static class MovementTypes
{
    public const string Receipt = "receipt";
    public const string Reserve = "reserve";
    public const string Unreserve = "unreserve";
    public const string Issue = "issue";
    public const string Inventory = "inventory";

    public static string Title(string type) => type switch
    {
        Receipt => "Оприходование",
        Reserve => "Резерв",
        Unreserve => "Снятие резерва",
        Issue => "Выдача мастеру",
        Inventory => "Инвентаризация",
        _ => type
    };
}
