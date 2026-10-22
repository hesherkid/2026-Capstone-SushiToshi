namespace back_end.Helpers
{
    /// <summary>
    /// The table's leftover fee is split evenly across its open bills.
    /// </summary>
    public static class LeftoverFee
    {
        /// <summary>
        /// Splits an amount into <paramref name="count"/> shares to the cent that add up exactly.
        /// Leftover cents go one each to the first bills, e.g. $10.00 / 3 = $3.34, $3.33, $3.33.
        /// </summary>
        public static IReadOnlyList<decimal> SplitEvenly(decimal amount, int count)
        {
            if (count <= 0) return Array.Empty<decimal>();

            var cents = (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);
            var baseCents = cents / count;
            var extraCents = cents % count;

            return Enumerable.Range(0, count)
                .Select(i => (baseCents + (i < extraCents ? 1 : 0)) / 100m)
                .ToList();
        }
    }
}