using ServiceDesk.Api.Services;
using ServiceDesk.Contracts;
using Xunit;
using S = ServiceDesk.Contracts.OrderStatuses;

namespace ServiceDesk.Tests;

// Жизненный цикл заявки (ТЗ, таблица 3)
public class OrderWorkflowTests
{
    [Theory]
    [InlineData(S.Accepted, S.Diagnostics, Roles.Master)]
    [InlineData(S.Accepted, S.Cancelled, Roles.Receptionist)]
    [InlineData(S.Diagnostics, S.Approval, Roles.Master)]
    [InlineData(S.Diagnostics, S.Cancelled, Roles.Receptionist)]
    [InlineData(S.Approval, S.InWork, Roles.Client)]
    [InlineData(S.Approval, S.WaitingParts, Roles.Client)]
    [InlineData(S.Approval, S.Cancelled, Roles.Client)]
    [InlineData(S.WaitingParts, S.InWork, Roles.Master)]
    [InlineData(S.InWork, S.WaitingParts, Roles.Master)]
    [InlineData(S.InWork, S.Ready, Roles.Master)]
    [InlineData(S.Ready, S.Issued, Roles.Receptionist)]
    [InlineData(S.Cancelled, S.Issued, Roles.Receptionist)]
    public void AllowedTransition(string from, string to, string role) =>
        Assert.True(OrderWorkflow.CanChange(from, to, role));

    [Theory]
    [InlineData(S.Accepted, S.Ready, Roles.Master)]            // пропуск этапов
    [InlineData(S.Accepted, S.Diagnostics, Roles.Receptionist)] // чужой этап
    [InlineData(S.Diagnostics, S.Approval, Roles.Client)]
    [InlineData(S.InWork, S.Ready, Roles.Receptionist)]
    [InlineData(S.Ready, S.Issued, Roles.Master)]
    [InlineData(S.InWork, S.Cancelled, Roles.Receptionist)]    // отмена в работе не предусмотрена
    [InlineData(S.Issued, S.InWork, Roles.Master)]             // конечный статус
    [InlineData(S.Issued, S.Cancelled, Roles.Receptionist)]
    [InlineData(S.Approval, S.InWork, Roles.Master)]           // согласует только клиент
    [InlineData(S.Accepted, S.Diagnostics, Roles.Storekeeper)]
    public void ForbiddenTransition(string from, string to, string role) =>
        Assert.False(OrderWorkflow.CanChange(from, to, role));

    [Fact]
    public void WarrantyOrderSkipsApproval()
    {
        Assert.True(OrderWorkflow.CanChange(S.Diagnostics, S.InWork, Roles.Master, isWarranty: true));
        Assert.False(OrderWorkflow.CanChange(S.Diagnostics, S.InWork, Roles.Master, isWarranty: false));
        Assert.False(OrderWorkflow.CanChange(S.Diagnostics, S.Approval, Roles.Master, isWarranty: true));
    }

    [Fact]
    public void SystemMovesOrderAfterPartsArrive() =>
        Assert.True(OrderWorkflow.CanChange(S.WaitingParts, S.InWork, OrderWorkflow.System));

    [Fact]
    public void IssuedHasNoNextStatuses()
    {
        foreach (var role in new[] { Roles.Client, Roles.Receptionist, Roles.Master, Roles.Storekeeper })
            Assert.Empty(OrderWorkflow.Allowed(S.Issued, role));
    }

    [Fact]
    public void MasterAllowedFromInWork() =>
        Assert.Equal(new HashSet<string> { S.WaitingParts, S.Ready }, OrderWorkflow.Allowed(S.InWork, Roles.Master).ToHashSet());

    [Fact]
    public void Overdue()
    {
        var today = new DateTime(2026, 10, 10);
        Assert.True(OrderWorkflow.IsOverdue(S.InWork, new DateTime(2026, 10, 9), today));
        Assert.False(OrderWorkflow.IsOverdue(S.InWork, new DateTime(2026, 10, 10), today));
        Assert.False(OrderWorkflow.IsOverdue(S.Issued, new DateTime(2026, 10, 1), today));
        Assert.False(OrderWorkflow.IsOverdue(S.Cancelled, new DateTime(2026, 10, 1), today));
    }
}

// Расчёт стоимости (ТЗ, п. 5.1)
public class CostCalculatorTests
{
    private static readonly decimal[] Works = { 1200m, 500m };
    private static readonly CostCalculator.PartLine[] Parts =
    {
        new(2000m, 1, ReservationStatuses.Reserved),
        new(450m, 2, ReservationStatuses.Issued),
        new(9999m, 3, ReservationStatuses.Cancelled),   // отменённый резерв не учитывается
        new(3800m, 1, ReservationStatuses.Requested),
    };

    [Fact]
    public void TotalIsWorksPlusParts() => Assert.Equal(1700m + 2000m + 900m + 3800m, CostCalculator.Total(Works, Parts, false));

    [Fact]
    public void WarrantyIsFree() => Assert.Equal(0m, CostCalculator.Total(Works, Parts, true));

    [Fact]
    public void EmptyOrderCostsZero() => Assert.Equal(0m, CostCalculator.Total(Array.Empty<decimal>(), Array.Empty<CostCalculator.PartLine>(), false));

    [Fact]
    public void DueAfterPartialPayment() => Assert.Equal(1200m, CostCalculator.Due(3200m, new[] { 1500m, 500m }));

    [Fact]
    public void DueNeverNegative() => Assert.Equal(0m, CostCalculator.Due(1000m, new[] { 1000m }));

    [Theory]
    [InlineData(3200, 3200, false, true)]
    [InlineData(3200, 3000, false, false)]
    [InlineData(0, 0, false, true)]
    [InlineData(3200, 0, true, true)]
    public void IssueRequiresFullPayment(decimal total, decimal paid, bool warranty, bool expected) =>
        Assert.Equal(expected, CostCalculator.CanIssue(total, paid, warranty));
}
