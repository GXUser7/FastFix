using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Infrastructure;
using ServiceDesk.Contracts;

namespace ServiceDesk.Api.Services;

// Формирование PDF-документов по заявке (QuestPDF): квитанция, смета, акт выполненных работ, акт выдачи
public class DocumentService
{
    private const string Font = "DejaVu Sans";
    private const string Navy = "#1B2A41";
    private const string Blue = "#2D6CDF";
    private const string Muted = "#7A8794";
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    private readonly AppDbContext _db;
    private readonly OrderService _orders;
    private readonly DocumentRegistry _registry;
    private readonly IConfiguration _cfg;

    public DocumentService(AppDbContext db, OrderService orders, DocumentRegistry registry, IConfiguration cfg)
    {
        _db = db;
        _orders = orders;
        _registry = registry;
        _cfg = cfg;
    }

    public static void Init()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseEnvironmentFonts = false;
        var asm = typeof(DocumentService).Assembly;
        foreach (var name in asm.GetManifestResourceNames().Where(n => n.EndsWith(".ttf")))
        {
            using var s = asm.GetManifestResourceStream(name);
            QuestPDF.Drawing.FontManager.RegisterFont(s);
        }
    }

    public async Task<(byte[] Pdf, string FileName)> BuildAsync(int orderId, string type, CurrentUser user)
    {
        if (!DocumentTypes.All.Contains(type)) throw BusinessException.NotFound("Неизвестный тип документа");
        if (!DocumentRegistry.RoleCanRead(user.Role, type)) throw BusinessException.Forbidden("Документ недоступен для вашей роли");
        var o = await _orders.LoadAsync(orderId, user);
        if (!DocumentRegistry.IsAvailable(o, type))
            throw BusinessException.Conflict($"Документ «{DocumentTypes.Title(type)}» ещё не сформирован для этой заявки");

        var doc = _registry.Register(o.Id, type, user.Id);
        await _db.SaveChangesAsync();

        var pdf = QuestPDF.Fluent.Document.Create(c => c.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(18, Unit.Millimetre);
            page.DefaultTextStyle(x => x.FontFamily(Font).FontSize(10).FontColor("#212121"));
            page.Header().Element(h => Header(h, DocumentTypes.Title(type), doc.Number, doc.CreatedAt));
            page.Content().PaddingVertical(8).Column(col =>
            {
                col.Spacing(8);
                Parties(col, o);
                switch (type)
                {
                    case DocumentTypes.Receipt: Receipt(col, o); break;
                    case DocumentTypes.Estimate: Estimate(col, o); break;
                    case DocumentTypes.Act: Act(col, o); break;
                    case DocumentTypes.Issue: Issue(col, o); break;
                }
            });
            page.Footer().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(x => x.FontSize(8).FontColor(Muted));
                t.Span("ИС «СервисДеск» · заявка № " + o.Id + " · стр. ");
                t.CurrentPageNumber();
                t.Span(" из ");
                t.TotalPages();
            });
        })).GeneratePdf();

        return (pdf, $"{doc.Number}.pdf");
    }

    // ------------------------------------------------------------------ блоки

    private void Header(IContainer c, string title, string number, DateTime date)
    {
        var company = _cfg["Company:Name"] ?? "Сервисный центр «СервисДеск»";
        var address = _cfg["Company:Address"] ?? "";
        var phone = _cfg["Company:Phone"] ?? "";
        c.Column(col =>
        {
            col.Item().Background(Navy).Padding(10).Row(r =>
            {
                r.RelativeItem().Text(t =>
                {
                    t.Span("Сервис").FontColor(Colors.White).FontSize(18).Bold();
                    t.Span("Деск").FontColor(Blue).FontSize(18).Bold();
                });
                r.RelativeItem().AlignRight().AlignMiddle().Column(cc =>
                {
                    cc.Item().AlignRight().Text(company).FontColor(Colors.White).FontSize(9);
                    if (address != "") cc.Item().AlignRight().Text(address).FontColor("#C9D6EA").FontSize(8);
                    if (phone != "") cc.Item().AlignRight().Text(phone).FontColor("#C9D6EA").FontSize(8);
                });
            });
            col.Item().PaddingTop(10).AlignCenter().Text($"{title} № {number}").FontSize(15).Bold();
            col.Item().AlignCenter().Text($"от {date:dd.MM.yyyy}").FontColor(Muted);
        });
    }

    private static void Parties(ColumnDescriptor col, Order o)
    {
        col.Item().Border(0.5f).BorderColor("#C9D2DC").Padding(8).Table(t =>
        {
            t.ColumnsDefinition(cd => { cd.ConstantColumn(130); cd.RelativeColumn(); });
            void Row(string k, string v)
            {
                t.Cell().PaddingVertical(2).Text(k).FontColor(Muted);
                t.Cell().PaddingVertical(2).Text(v ?? "—");
            }
            Row("Клиент", o.Client.FullName);
            Row("Телефон", Phone.Format(o.Client.Phone));
            Row("Устройство", o.Device.Title);
            Row("Серийный номер / IMEI", o.Device.SerialNumber);
            Row("Дата приёма", o.CreatedAt.ToString("dd.MM.yyyy HH:mm"));
            if (o.IsWarranty) Row("Вид ремонта", "Гарантийный");
        });
    }

    private void Receipt(ColumnDescriptor col, Order o)
    {
        Section(col, "Заявленная неисправность", o.DeclaredFault);
        var accessories = o.Completeness.Where(c => c.Item.Kind == "accessory").Select(c => c.Item.Name).ToList();
        var appearance = o.Completeness.Where(c => c.Item.Kind == "appearance").Select(c => c.Item.Name).ToList();
        Section(col, "Комплектность", accessories.Count == 0 ? "Только устройство" : string.Join(", ", accessories));
        var look = string.Join("; ", appearance.Concat(string.IsNullOrWhiteSpace(o.AppearanceNote) ? Array.Empty<string>() : new[] { o.AppearanceNote }));
        Section(col, "Внешний вид", look == "" ? "Без видимых повреждений" : look);
        Section(col, "Плановый срок готовности", o.DueDate.ToString("dd.MM.yyyy"));
        Section(col, "Мастер", o.Master?.ShortName ?? "будет назначен");
        col.Item().PaddingTop(6).Text(
            "Стоимость ремонта определяется после диагностики и согласовывается с клиентом в личном кабинете или по телефону. " +
            "Без согласования клиента платный ремонт не выполняется. Статус заявки можно отслеживать в личном кабинете по номеру телефона. " +
            "Устройство выдаётся при предъявлении квитанции после полной оплаты.").FontSize(8.5f).FontColor(Muted);
        Signatures(col, "Устройство принял (приёмщик)", o.Receptionist.ShortName, "С условиями согласен (клиент)", o.Client.ShortName);
    }

    private static void Estimate(ColumnDescriptor col, Order o)
    {
        Section(col, "Заключение мастера", o.Diagnosis ?? "—");
        CostTable(col, o);
        if (o.ClientDecision != null)
            Section(col, "Решение клиента",
                (o.ClientDecision == "approved" ? "Стоимость согласована" : "Клиент отказался от ремонта") + $" {o.DecisionAt:dd.MM.yyyy HH:mm}");
        Signatures(col, "Мастер", o.Master?.ShortName, "Клиент", o.Client.ShortName);
    }

    private static void Act(ColumnDescriptor col, Order o)
    {
        Section(col, "Заявленная неисправность", o.DeclaredFault);
        Section(col, "Заключение мастера", o.Diagnosis ?? "—");
        CostTable(col, o);
        Section(col, "Гарантия на выполненные работы", o.WarrantyMonths is > 0 ? $"{o.WarrantyMonths} мес." : "не предоставляется");
        Section(col, "Дата готовности", o.ReadyAt?.ToString("dd.MM.yyyy") ?? "—");
        col.Item().Text("Работы выполнены в полном объёме. Клиент претензий по объёму, качеству и срокам выполнения работ не имеет.").FontSize(9);
        Signatures(col, "Исполнитель (мастер)", o.Master?.ShortName, "Заказчик (клиент)", o.Client.ShortName);
    }

    private static void Issue(ColumnDescriptor col, Order o)
    {
        var paid = o.Payments.Sum(p => p.Amount);
        Section(col, "Результат", o.History.Any(h => h.Status.Code == OrderStatuses.Cancelled)
            ? "Устройство возвращено клиенту без ремонта"
            : "Устройство выдано клиенту после ремонта");
        Section(col, "Стоимость ремонта", o.IsWarranty ? "Гарантийный ремонт — 0,00 ₽" : Money(o.TotalCost));
        if (o.Payments.Count > 0)
        {
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(cd => { cd.RelativeColumn(2); cd.RelativeColumn(2); cd.RelativeColumn(1.4f); });
                HeadCell(t, "Дата оплаты"); HeadCell(t, "Способ"); HeadCell(t, "Сумма");
                foreach (var p in o.Payments.OrderBy(p => p.PaidAt))
                {
                    BodyCell(t, p.PaidAt.ToString("dd.MM.yyyy HH:mm"));
                    BodyCell(t, PaymentMethods.Title(p.Method));
                    BodyCell(t, Money(p.Amount), true);
                }
            });
        }
        Section(col, "Оплачено", Money(paid));
        Section(col, "Дата выдачи", o.IssuedAt?.ToString("dd.MM.yyyy HH:mm") ?? "—");
        Section(col, "Гарантия на работы", o.WarrantyMonths is > 0 ? $"{o.WarrantyMonths} мес. с даты выдачи" : "не предоставляется");
        col.Item().Text("Устройство получено, комплектность и внешний вид проверены, претензий не имею.").FontSize(9);
        Signatures(col, "Выдал (приёмщик)", o.History.OrderBy(h => h.ChangedAt).ThenBy(h => h.Id).LastOrDefault()?.ChangedBy?.ShortName ?? o.Receptionist.ShortName, "Получил (клиент)", o.Client.ShortName);
    }

    private static void CostTable(ColumnDescriptor col, Order o)
    {
        var parts = o.Reservations.Where(r => r.Status != ReservationStatuses.Cancelled).ToList();
        col.Item().PaddingTop(4).Text("Работы и запчасти").Bold().FontColor(Navy);
        col.Item().Table(t =>
        {
            t.ColumnsDefinition(cd =>
            {
                cd.ConstantColumn(24); cd.RelativeColumn(5); cd.RelativeColumn(1); cd.RelativeColumn(1.6f); cd.RelativeColumn(1.6f);
            });
            HeadCell(t, "№"); HeadCell(t, "Наименование"); HeadCell(t, "Кол."); HeadCell(t, "Цена, ₽"); HeadCell(t, "Сумма, ₽");
            var n = 0;
            foreach (var w in o.Works.OrderBy(w => w.CreatedAt).ThenBy(w => w.Id))
            {
                BodyCell(t, (++n).ToString()); BodyCell(t, "Работа: " + w.Description); BodyCell(t, "1", true);
                BodyCell(t, Num(w.Price), true); BodyCell(t, Num(w.Price), true);
            }
            foreach (var r in parts)
            {
                BodyCell(t, (++n).ToString()); BodyCell(t, $"Запчасть: {r.Part.Name} ({r.Part.Sku})"); BodyCell(t, $"{r.Quantity} {r.Part.Unit}", true);
                BodyCell(t, Num(r.Price), true); BodyCell(t, Num(r.Price * r.Quantity), true);
            }
            if (n == 0) { t.Cell().ColumnSpan(5).Padding(4).Text("Позиции не добавлены").FontColor(Muted); }
        });
        var works = o.Works.Sum(w => w.Price);
        var partsSum = parts.Sum(r => r.Price * r.Quantity);
        col.Item().AlignRight().Column(c =>
        {
            c.Item().AlignRight().Text($"Работы: {Money(works)}");
            c.Item().AlignRight().Text($"Запчасти: {Money(partsSum)}");
            if (o.IsWarranty) c.Item().AlignRight().Text("Гарантийный ремонт: оплата не требуется").FontColor(Muted);
            c.Item().AlignRight().Text($"Итого к оплате: {Money(o.TotalCost)}").FontSize(12).Bold();
        });
    }

    private static void Section(ColumnDescriptor col, string title, string text) =>
        col.Item().Text(t =>
        {
            t.Span(title + ": ").Bold().FontColor(Navy);
            t.Span(text ?? "—");
        });

    private static void Signatures(ColumnDescriptor col, string leftRole, string leftName, string rightRole, string rightName) =>
        col.Item().PaddingTop(28).Row(r =>
        {
            void Sign(RowDescriptor row, string role, string name) => row.RelativeItem().Column(c =>
            {
                c.Item().Text(role).FontColor(Muted).FontSize(9);
                c.Item().PaddingTop(18).Text($"__________________ / {name ?? "__________"}");
            });
            Sign(r, leftRole, leftName);
            r.ConstantItem(30);
            Sign(r, rightRole, rightName);
        });

    private static void HeadCell(TableDescriptor t, string text) =>
        t.Cell().Background("#E3EAF4").Border(0.5f).BorderColor("#C9D2DC").Padding(4).Text(text).Bold().FontSize(9);

    private static void BodyCell(TableDescriptor t, string text, bool right = false)
    {
        var cell = t.Cell().Border(0.5f).BorderColor("#C9D2DC").Padding(4);
        (right ? cell.AlignRight() : cell).Text(text).FontSize(9);
    }

    private static string Num(decimal v) => v.ToString("N2", Ru);
    private static string Money(decimal v) => v.ToString("N2", Ru) + " ₽";
}
