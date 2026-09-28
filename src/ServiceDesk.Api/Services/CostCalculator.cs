using ServiceDesk.Contracts;

namespace ServiceDesk.Api.Services;

// Расчёт стоимости по формуле ТЗ (п. 5.1): S = Σ Ц(р)ᵢ + Σ Ц(з)ⱼ × Kⱼ; для гарантийной заявки S = 0
public static class CostCalculator
{
    public record PartLine(decimal Price, int Quantity, string Status);

    public static decimal WorksTotal(IEnumerable<decimal> works) => works.Sum();

    public static decimal PartsTotal(IEnumerable<PartLine> parts) =>
        parts.Where(p => p.Status != ReservationStatuses.Cancelled).Sum(p => p.Price * p.Quantity);

    public static decimal Total(IEnumerable<decimal> works, IEnumerable<PartLine> parts, bool isWarranty) =>
        isWarranty ? 0m : WorksTotal(works) + PartsTotal(parts);

    public static decimal Due(decimal total, IEnumerable<decimal> payments) =>
        Math.Max(0m, total - payments.Sum());

    // Выдача разрешена после полной оплаты; гарантийные и бесплатные заявки — без оплаты
    public static bool CanIssue(decimal total, decimal paid, bool isWarranty) =>
        isWarranty || total == 0m || paid >= total;
}
