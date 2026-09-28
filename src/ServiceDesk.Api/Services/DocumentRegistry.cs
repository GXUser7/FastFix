using ServiceDesk.Api.Data;
using ServiceDesk.Contracts;
using S = ServiceDesk.Contracts.OrderStatuses;

namespace ServiceDesk.Api.Services;

// Реестр документов: уникальный номер, доступность по статусу и права ролей (ТЗ, таблица 2)
public class DocumentRegistry
{
    private readonly AppDbContext _db;

    public DocumentRegistry(AppDbContext db) => _db = db;

    public static string NumberFor(string type, int orderId) => $"{DocumentTypes.Prefix(type)}-{orderId}";

    public static bool RoleCanRead(string role, string type) => role switch
    {
        Roles.Receptionist => true,
        Roles.Client => type is DocumentTypes.Receipt or DocumentTypes.Estimate or DocumentTypes.Act,
        Roles.Master => type is DocumentTypes.Estimate or DocumentTypes.Act,
        _ => false,
    };

    public static bool IsAvailable(Order o, string type)
    {
        var st = o.Status?.Code;
        return type switch
        {
            DocumentTypes.Receipt => true,
            DocumentTypes.Estimate => o.Works.Count > 0 || o.Reservations.Any(r => r.Status != ReservationStatuses.Cancelled),
            DocumentTypes.Act => st is S.Ready or S.Issued && o.Works.Count > 0,
            DocumentTypes.Issue => st == S.Issued,
            _ => false,
        };
    }

    // Регистрирует документ, если он ещё не внесён в реестр (номер = префикс + номер заявки)
    public Document Register(int orderId, string type, int userId)
    {
        var existing = _db.Documents.Local.FirstOrDefault(d => d.OrderId == orderId && d.DocType == type)
                       ?? _db.Documents.FirstOrDefault(d => d.OrderId == orderId && d.DocType == type);
        if (existing != null) return existing;
        var doc = new Document { OrderId = orderId, DocType = type, Number = NumberFor(type, orderId), CreatedById = userId };
        _db.Documents.Add(doc);
        return doc;
    }
}
