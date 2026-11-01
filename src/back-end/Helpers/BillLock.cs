using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using back_end.domain.DbContexts;

namespace back_end.Helpers
{
    // Stops an order being placed on a bill while staff are closing it
    public static class BillLock
    {
        // Starts a transaction holding the bill's row lock until it's committed or disposed.
        // Call it before reading anything: MariaDB's snapshot starts at the first read, so reads
        // made after the lock see what the other request committed.
        // The in-memory test database has no transactions or row locks, so there it returns null.
        public static async Task<IDbContextTransaction?> LockAsync(ApplicationDbContext context, int billId)
        {
            if (!context.Database.IsRelational()) return null;

            var transaction = await context.Database.BeginTransactionAsync();
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT Bill_Id FROM bills WHERE Bill_Id = {billId} FOR UPDATE");
            return transaction;
        }
    }
}
