using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using back_end.domain.DbContexts;

namespace back_end.Helpers
{
    // Stops two joins from the same person (a double tap, a retry, two tabs) both adding a seat
    public static class SeatLock
    {
        // Starts a transaction holding the user's row lock until it's committed or disposed, so a
        // second join waits and then finds the seat the first one made.
        // Call it before reading their seats: MariaDB's snapshot starts at the first read.
        // The in-memory test database has no transactions or row locks, so there it returns null.
        public static async Task<IDbContextTransaction?> LockAsync(ApplicationDbContext context, int userId)
        {
            if (!context.Database.IsRelational()) return null;

            var transaction = await context.Database.BeginTransactionAsync();
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT User_id FROM users WHERE User_id = {userId} FOR UPDATE");
            return transaction;
        }
    }
}
