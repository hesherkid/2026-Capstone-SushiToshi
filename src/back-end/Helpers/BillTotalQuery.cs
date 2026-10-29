using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.Services;
using Microsoft.EntityFrameworkCore;

namespace back_end.Helpers
{
    /// <summary>
    /// The database side of a bill's total, shared by the bill summary and past visits so the
    /// two can't disagree. The arithmetic itself is in BillCalculator.
    /// </summary>
    public static class BillTotalQuery
    {
        // Lines that cost extra on a bill: add-ons, over-limit units and lines staff priced.
        // Cancelled orders and cancelled items don't count
        public static IQueryable<OrderItems> ChargedLines(ApplicationDbContext context, int billId) =>
            context.OrderItems.Where(oi =>
                oi.SessionOrder.Bill_Id == billId &&
                oi.SessionOrder.Status != OrderStatus.Cancelled &&
                oi.Order_Item_Status != OrderStatus.Cancelled &&
                oi.Price_At_Time > 0);

        // Guests on every bill at the table, for the gratuity's minimum party size
        public static Task<int> PartySizeAsync(ApplicationDbContext context, int sessionId) =>
            context.Bills
                .Where(b => b.Session_Id == sessionId && b.Status != BillStatus.Cancelled)
                .SumAsync(b => b.Adult_Count + b.Senior_Count + b.Child_Count + b.Tot_Count);

        /// <summary>
        /// What the bill comes to, the same way the bill summary works it out: closed bills use the
        /// prices frozen when they closed, others the location's prices on the day the bill was made.
        /// </summary>
        public static async Task<decimal> TotalAsync(ApplicationDbContext context, IPricingService pricingService, Billing bill, int locationId)
        {
            var frozen = BillPriceSnapshot.Read(bill);
            var pricing = frozen ?? await pricingService.GetPricingAsync(locationId, bill.Created_At);

            var charged = await ChargedLines(context, bill.Bill_Id)
                .SumAsync(oi => oi.Quantity * oi.Price_At_Time);
            var subtotal = BillCalculator.BaseCharges(bill, pricing) + charged + bill.Leftover_Charge;

            var gratuityRate = frozen?.GratuityRate
                ?? BillCalculator.GratuityRateFor(pricing, await PartySizeAsync(context, bill.Session_Id));

            return BillCalculator.Totals(subtotal, gratuityRate, pricing.TaxRate).Total;
        }
    }
}
