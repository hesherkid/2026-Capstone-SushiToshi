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
        public decimal TaxRate { get; set; }
        public decimal LeftoverFee { get; set; }
    }
}