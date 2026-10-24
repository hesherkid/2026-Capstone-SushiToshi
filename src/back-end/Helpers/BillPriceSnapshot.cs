using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.Services;

namespace back_end.Helpers
{
    /// <summary>
    /// Prices are copied onto a bill when it closes, so editing pricing or holidays later
    /// doesn't change bills that are already closed.
    /// </summary>
    public static class BillPriceSnapshot
    {
        public static void Freeze(Billing bill, PricingInfo pricing, decimal appliedGratuityRate)
        {
            bill.Pricing_Type = Enum.Parse<PricingDayType>(pricing.PricingType);
            bill.Holiday_Name = pricing.HolidayName;
            bill.Adult_Price = pricing.AdultBasePrice;
            bill.Senior_Price = pricing.SeniorBasePrice;
            bill.Child_Price = pricing.ChildBasePrice;
            bill.Toddler_Price = pricing.ToddlerBasePrice;
            bill.Tax_Rate = pricing.TaxRate;
            bill.Gratuity_Rate = appliedGratuityRate;
            bill.Gratuity_Min_Party_Size = pricing.GratuityMinPartySize;
        }

        /// <summary>
        /// The frozen prices, or null for open bills and bills closed before snapshots existed.
        /// GratuityRate is the rate that was charged, not the rate a large party would get.
        /// </summary>
        public static PricingInfo? Read(Billing bill)
        {
            if (bill.Pricing_Type is not PricingDayType dayType)
            {
                return null;
            }

            // Freeze sets every column together, so they're all present once Pricing_Type is
            return new PricingInfo
            {
                PricingType = dayType.ToString(),
                HolidayName = bill.Holiday_Name,
                AdultBasePrice = bill.Adult_Price!.Value,
                SeniorBasePrice = bill.Senior_Price!.Value,
                ChildBasePrice = bill.Child_Price!.Value,
                ToddlerBasePrice = bill.Toddler_Price!.Value,
                TaxRate = bill.Tax_Rate!.Value,
                GratuityRate = bill.Gratuity_Rate!.Value,
                GratuityMinPartySize = bill.Gratuity_Min_Party_Size!.Value,
            };
        }
    }
}
