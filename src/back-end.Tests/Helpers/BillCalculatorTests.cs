using Xunit;
using FluentAssertions;
using back_end.domain.Entities;
using back_end.Helpers;
using back_end.Services;

namespace back_end.Tests.Helpers
{
    public class BillCalculatorTests
    {
        private static readonly PricingInfo Pricing = new()
        {
            PricingType = "Weekday",
            AdultBasePrice = 40m,
            SeniorBasePrice = 35m,
            ChildBasePrice = 25m,
            ToddlerBasePrice = 7.95m,
            TaxRate = 0.05m,
            GratuityRate = 0.18m,
            GratuityMinPartySize = 6,
        };

        [Fact]
        public void BaseCharges_PricesEachGuestType()
        {
            var bill = new Billing { Adult_Count = 2, Senior_Count = 1, Child_Count = 1, Tot_Count = 1 };

            BillCalculator.BaseCharges(bill, Pricing).Should().Be(147.95m);   // 80 + 35 + 25 + 7.95
        }

        [Theory]
        [InlineData(5, 6, 0)]      // one short of the minimum
        [InlineData(6, 6, 0.18)]   // exactly the minimum
        [InlineData(20, 0, 0)]     // minimum 0 = never
        public void GratuityRateFor_AppliesFromTheMinimumPartySize(int partySize, int minPartySize, decimal expected)
        {
            var pricing = new PricingInfo { GratuityRate = 0.18m, GratuityMinPartySize = minPartySize };

            BillCalculator.GratuityRateFor(pricing, partySize).Should().Be(expected);
        }

        [Fact]
        public void Totals_ChargesGstOnTheGratuityToo()
        {
            var totals = BillCalculator.Totals(subtotal: 100m, gratuityRate: 0.18m, taxRate: 0.05m);

            totals.Gratuity.Should().Be(18.00m);
            totals.Tax.Should().Be(5.90m);      // 5% of 118
            totals.Total.Should().Be(123.90m);
        }

        [Fact]
        public void Totals_WithoutGratuity_IsSubtotalPlusGst()
        {
            var totals = BillCalculator.Totals(subtotal: 80m, gratuityRate: 0m, taxRate: 0.05m);

            totals.Gratuity.Should().Be(0m);
            totals.Tax.Should().Be(4.00m);
            totals.Total.Should().Be(84.00m);
        }

        [Fact]
        public void Totals_RoundsGratuityAndTaxToTheCent()
        {
            var totals = BillCalculator.Totals(subtotal: 33.33m, gratuityRate: 0.18m, taxRate: 0.05m);

            totals.Gratuity.Should().Be(6.00m);   // 5.9994
            totals.Tax.Should().Be(1.97m);        // 5% of 39.33 = 1.9665
            totals.Total.Should().Be(41.30m);
        }
    }
}
