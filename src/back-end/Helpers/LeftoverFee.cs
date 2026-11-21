using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using Microsoft.EntityFrameworkCore;

namespace back_end.Helpers
{
    /// <summary>
    /// The table's leftover fee is shared across its open bills by how many guests are on each 
    /// bill (including toddlers). Every guest at the table pays about the same. 
    /// Closed bills keep what they had.
    /// </summary>
    public static class LeftoverFee
    {
        /// <summary>
        /// Splits an amount into <paramref name="count"/> shares to the cent that add up exactly.
        /// Leftover cents go one each to the first bills, e.g. $10.00 / 3 = $3.34, $3.33, $3.33.
        /// Used when a dining session has no guest count information yet.
        /// </summary>
        public static IReadOnlyList<decimal> SplitEvenly(decimal amount, int count)
        {
            if (count <= 0) return Array.Empty<decimal>();

            var cents = ToCents(amount);
            var baseCents = cents / count;
            var extraCents = cents % count;

            return Enumerable.Range(0, count)
                .Select(i => (baseCents + (i < extraCents ? 1 : 0)) / 100m)
                .ToList();
        }

        /// <summary>
        /// Splits an amount across bills by their guest counts. Each bill gets its share rounded down,
        /// the cents left over go one each to the bills that lost the most to rounding.
        /// e.g. $20.00 over a bill of 4 guests and a bill of 1 guest, bill of 4 = $16.00. bill of 1 = $4.00. 
        /// </summary>
        public static IReadOnlyList<decimal> SplitByGuests(decimal amount, IReadOnlyList<int> guestCounts)
        {
            if (guestCounts.Count == 0) return Array.Empty<decimal>();

            var weights = guestCounts.Select(g => (long)Math.Max(0, g)).ToList();
            var totalGuests = weights.Sum();
            if (totalGuests == 0) return SplitEvenly(amount, guestCounts.Count);

            var cents = ToCents(amount);
            var shares = new long[weights.Count];
            var remainders = new long[weights.Count];
            for (var i = 0; i < weights.Count; i++)
            {
                var exact = cents * weights[i];
                shares[i] = exact / totalGuests;
                remainders[i] = exact % totalGuests;
            }

            var leftover = cents - shares.Sum();
            foreach (var i in Enumerable.Range(0, weights.Count)
                         .OrderByDescending(i => remainders[i])
                         .ThenBy(i => i)
                         .Take((int)leftover))
            {
                shares[i]++;
            }

            return shares.Select(c => c / 100m).ToList();
        }

        /// <summary>
        /// Shares the table's current fee across its open bills again, after a guest count changed
        /// or a bill was split off. Uses the counts on bills already loaded, even before they're saved.
        /// A new bill that isn't saved yet is passed in, so it takes its share. Does nothing when
        /// there's no fee.
        /// </summary>
        public static async Task ReshareAsync(ApplicationDbContext context, int sessionId, Billing? newBill = null)
        {
            var openBills = await context.Bills
                .Where(b => b.Session_Id == sessionId && b.Status == BillStatus.Open)
                .OrderBy(b => b.Bill_Id)
                .ToListAsync();
            var fee = openBills.Sum(b => b.Leftover_Charge);
            if (fee == 0m) return;

            if (newBill != null) openBills.Add(newBill);
            var shares = SplitByGuests(fee, openBills.Select(b => b.Total_Count).ToList());
            for (var i = 0; i < openBills.Count; i++)
            {
                openBills[i].Leftover_Charge = shares[i];
            }
        }

        private static long ToCents(decimal amount) =>
            (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);
    }
}