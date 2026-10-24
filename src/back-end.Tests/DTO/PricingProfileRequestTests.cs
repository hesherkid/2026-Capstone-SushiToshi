using System.ComponentModel.DataAnnotations;
using Xunit;
using FluentAssertions;
using back_end.domain.enums;
using back_end.DTO.PricingDTOs;

namespace back_end.Tests.DTO
{
    public class PricingProfileRequestTests
    {
        private static PricingProfileRequest Valid() => new()
        {
            Tax_Rate = 0.05m,
            Gratuity_Rate = 0.18m,
            Gratuity_Min_Party_Size = 6,
            Leftover_Fee = 15m,
            Weekend_Days = new() { DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday },
            Rates = Enum.GetValues<PricingDayType>()
                .Select(d => new PricingRateDTO { Day_Type = d, Adult_Price = 40m })
                .ToList(),
        };

        // Field names with errors
        private static IEnumerable<string> Errors(object model)
        {
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
            return results.SelectMany(r => r.MemberNames);
        }

        [Fact]
        public void ValidRequest_HasNoErrors()
        {
            Errors(Valid()).Should().BeEmpty();
        }

        [Fact]
        public void MissingHolidayRate_IsRejected()
        {
            var request = Valid();
            request.Rates.RemoveAll(r => r.Day_Type == PricingDayType.Holiday);

            Errors(request).Should().Contain(nameof(PricingProfileRequest.Rates));
        }

        [Fact]
        public void SameDayTypeTwice_IsRejected()
        {
            var request = Valid();
            request.Rates[2].Day_Type = PricingDayType.Weekend;   // Weekday, Weekend, Weekend

            Errors(request).Should().Contain(nameof(PricingProfileRequest.Rates));
        }

        [Fact]
        public void UnknownDayType_IsRejected()
        {
            var request = Valid();
            request.Rates[2].Day_Type = (PricingDayType)7;   // the enum converter accepts numbers

            Errors(request).Should().Contain(nameof(PricingProfileRequest.Rates));
        }

        [Fact]
        public void RepeatedWeekendDay_IsRejected()
        {
            var request = Valid();
            request.Weekend_Days.Add(DayOfWeek.Friday);

            Errors(request).Should().Contain(nameof(PricingProfileRequest.Weekend_Days));
        }

        [Fact]
        public void UnknownWeekendDay_IsRejected()
        {
            var request = Valid();
            request.Weekend_Days.Add((DayOfWeek)7);

            Errors(request).Should().Contain(nameof(PricingProfileRequest.Weekend_Days));
        }

        [Fact]
        public void NoWeekendDays_IsAllowed()
        {
            var request = Valid();
            request.Weekend_Days.Clear();   // every day uses weekday prices

            Errors(request).Should().BeEmpty();
        }

        [Theory]
        [InlineData(-0.01)]
        [InlineData(1.5)]   // 150%: someone typed 15 meaning 15%... or 0.15 as 15
        public void TaxRateOutsideZeroToOne_IsRejected(decimal taxRate)
        {
            var request = Valid();
            request.Tax_Rate = taxRate;

            Errors(request).Should().Contain(nameof(PricingProfileRequest.Tax_Rate));
        }

        [Fact]
        public void GratuityPartySizeOver100_IsRejected()
        {
            var request = Valid();
            request.Gratuity_Min_Party_Size = 101;

            Errors(request).Should().Contain(nameof(PricingProfileRequest.Gratuity_Min_Party_Size));
        }

        [Fact]
        public void NegativePrice_IsRejected()
        {
            var rate = new PricingRateDTO { Day_Type = PricingDayType.Weekday, Adult_Price = -1m };

            Errors(rate).Should().Contain(nameof(PricingRateDTO.Adult_Price));
        }
    }
}
