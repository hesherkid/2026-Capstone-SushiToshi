using back_end.domain.Entities;
using back_end.Services;

namespace back_end.Helpers
{
    /// <summary>
    /// Bill arithmetic in one place: base charges per guest, then gratuity on the subtotal,
    /// then GST on the subtotal plus gratuity.
    /// </summary>
    public static class BillCalculator
    {
        public static decimal BaseCharges(Billing bill, PricingInfo pricing) =>
            bill.Adult_Count * pricing.AdultBasePrice +
            bill.Senior_Count * pricing.SeniorBasePrice +
            bill.Child_Count * pricing.ChildBasePrice +
            bill.Tot_Count * pricing.ToddlerBasePrice;

        /// <summary>
        /// The gratuity applies once the whole table has enough guests; a minimum of 0 means never.
        /// </summary>
        public static decimal GratuityRateFor(PricingInfo pricing, int partySize) =>
            pricing.GratuityMinPartySize > 0 && partySize >= pricing.GratuityMinPartySize
                ? pricing.GratuityRate
                : 0m;

        public static BillTotals Totals(decimal subtotal, decimal gratuityRate, decimal taxRate)
        {
            var gratuity = Math.Round(subtotal * gratuityRate, 2);

            // A mandatory gratuity is part of the price, so GST applies to it too (unlike a tip)
            var tax = Math.Round((subtotal + gratuity) * taxRate, 2);

            return new BillTotals(gratuity, tax, subtotal + gratuity + tax);
        }
    }

    public sealed record BillTotals(decimal Gratuity, decimal Tax, decimal Total);
}
