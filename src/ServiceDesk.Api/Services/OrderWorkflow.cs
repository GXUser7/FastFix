using ServiceDesk.Contracts;
using S = ServiceDesk.Contracts.OrderStatuses;

namespace ServiceDesk.Api.Services;

// Конечный автомат жизненного цикла заявки (ТЗ, п. 4.4, таблица 3).
// Не зависит от БД и HTTP — полностью покрывается модульными тестами.
public static class OrderWorkflow
{
    // Особый исполнитель — сама система (автоматические переходы после согласования и поступления деталей)
    public const string System = "system";

    private record Rule(string From, string To, params string[] Actors);

    private static readonly Rule[] Rules =
    {
        new(S.Accepted, S.Diagnostics, Roles.Master),
        new(S.Accepted, S.Cancelled, Roles.Receptionist),

        new(S.Diagnostics, S.Approval, Roles.Master),
        new(S.Diagnostics, S.InWork, Roles.Master),            // только для гарантийных заявок
        new(S.Diagnostics, S.Cancelled, Roles.Receptionist),

        new(S.Approval, S.InWork, Roles.Client, System),
        new(S.Approval, S.WaitingParts, Roles.Client, System),
        new(S.Approval, S.Cancelled, Roles.Client, Roles.Receptionist),

        new(S.WaitingParts, S.InWork, Roles.Master, System),
        new(S.WaitingParts, S.Cancelled, Roles.Receptionist),

        new(S.InWork, S.WaitingParts, Roles.Master, System),
        new(S.InWork, S.Ready, Roles.Master),

        new(S.Ready, S.Issued, Roles.Receptionist),

        new(S.Cancelled, S.Issued, Roles.Receptionist),         // возврат устройства клиенту
    };

    public static bool IsKnown(string status) =>
        status is S.Accepted or S.Diagnostics or S.Approval or S.WaitingParts or S.InWork or S.Ready or S.Issued or S.Cancelled;

    // Допустим ли переход с учётом роли; для гарантийной заявки Диагностика → В работе без согласования
    public static bool CanChange(string from, string to, string actor, bool isWarranty = false)
    {
        if (from == to) return false;
        if (from == S.Diagnostics && to == S.InWork && !isWarranty) return false;
        if (from == S.Diagnostics && to == S.Approval && isWarranty) return false;
        return Rules.Any(r => r.From == from && r.To == to && r.Actors.Contains(actor));
    }

    // Переходы, доступные роли из текущего статуса
    public static List<string> Allowed(string from, string actor, bool isWarranty = false) =>
        Rules.Where(r => r.From == from).Select(r => r.To).Distinct()
             .Where(to => CanChange(from, to, actor, isWarranty)).ToList();

    public static bool IsFinal(string status) => status == S.Issued;

    // Просрочка: плановый срок прошёл, а заявка не выдана и не отменена
    public static bool IsOverdue(string status, DateTime dueDate, DateTime today) =>
        status != S.Issued && status != S.Cancelled && dueDate.Date < today.Date;
}
