using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Data;

namespace ServiceDesk.Api.Infrastructure;

// Складские и платёжные операции выполняются в транзакции (ТЗ, п. 5.2)
public static class DbTransactions
{
    public static async Task<T> InTransactionAsync<T>(this AppDbContext db, Func<Task<T>> action)
    {
        if (!db.Database.IsRelational() || db.Database.CurrentTransaction != null) return await action();
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var result = await action();
            await tx.CommitAsync();
            return result;
        }
        catch
        {
            await tx.RollbackAsync();
            db.ChangeTracker.Clear();
            throw;
        }
    }

    public static Task InTransactionAsync(this AppDbContext db, Func<Task> action) =>
        db.InTransactionAsync(async () => { await action(); return true; });
}
