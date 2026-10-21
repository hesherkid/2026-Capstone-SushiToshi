namespace back_end.Services
{
    public interface IPricingService
    {
        PricingInfo GetPricingForDate(DateTime date);
        PricingInfo GetCurrentPricing();
    }

    public class PricingInfo
    {
        public string PricingType { get; set; } = string.Empty;
        public decimal AdultBasePrice { get; set; }
        public decimal SeniorBasePrice { get; set; }
        public decimal ChildBasePrice { get; set; }
        public decimal ToddlerBasePrice { get; set; }
        // GST (Alberta has no provincial sales tax)
        public decimal TaxRate { get; set; }

        // Automatic gratuity once the table has this many guests; 0 = never
        public decimal GratuityRate { get; set; }
        public int GratuityMinPartySize { get; set; }
    }
}