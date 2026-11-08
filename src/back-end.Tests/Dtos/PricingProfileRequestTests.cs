using System.ComponentModel.DataAnnotations;
using Xunit;
using FluentAssertions;
using back_end.domain.enums;
using back_end.Dtos;

namespace back_end.Tests.Dtos
{
    public class PricingProfileRequestTests
    {
        private static PricingProfileRequest Valid() => new()
        {
            TaxRate = 0.05m,
            GratuityRate = 0.18m,
            GratuityMinPartySize = 6,
            LeftoverFee = 15m,
            WeekendDays = new() { DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday },
            Rates = Enum.GetValues<PricingDayType>()
                .Select(d => new PricingRateDto { DayType = d, AdultPrice = 40m })
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
            request.Rates.RemoveAll(r => r.DayType == PricingDayType.Holiday);

            Errors(request).Should().Contain(nameof(PricingProfileRequest.Rates));
        }

        [Fact]
        public void SameDayTypeTwice_IsRejected()
        {
            var request = Valid();
            request.Rates[2].DayType = PricingDayType.Weekend;   // Weekday, Weekend, Weekend

            Errors(request).Should().Contain(nameof(PricingProfileRequest.Rates));
        }

        [Fact]
        public void UnknownDayType_IsRejected()
        {
            var request = Valid();
            request.Rates[2].DayType = (PricingDayType)7;   // the enum converter accepts numbers

            Errors(request).Should().Contain(nameof(PricingProfileRequest.Rates));
        }

        [Fact]
        public void RepeatedWeekendDay_IsRejected()
        {
            var request = Valid();
            request.WeekendDays.Add(DayOfWeek.Friday);

            Errors(request).Should().Contain(nameof(PricingProfileRequest.WeekendDays));
        }

        [Fact]
        public void UnknownWeekendDay_IsRejected()
        {
            var request = Valid();
            request.WeekendDays.Add((DayOfWeek)7);

            Errors(request).Should().Contain(nameof(PricingProfileRequest.WeekendDays));
        }

        [Fact]
        public void NoWeekendDays_IsAllowed()
        {
            var request = Valid();
            request.WeekendDays.Clear();   // every day uses weekday prices

            Errors(request).Should().BeEmpty();
        }

        [Theory]
        [InlineData(-0.01)]
        [InlineData(1.5)]   // 150%: someone typed 15 meaning 15%... or 0.15 as 15
        public void TaxRateOutsideZeroToOne_IsRejected(decimal taxRate)
        {
            var request = Valid();
            request.TaxRate = taxRate;

            Errors(request).Should().Contain(nameof(PricingProfileRequest.TaxRate));
        }

        [Fact]
        public void GratuityPartySizeOver100_IsRejected()
        {
            var request = Valid();
            request.GratuityMinPartySize = 101;

            Errors(request).Should().Contain(nameof(PricingProfileRequest.GratuityMinPartySize));
        }

        [Fact]
        public void NegativePrice_IsRejected()
        {
            var rate = new PricingRateDto { DayType = PricingDayType.Weekday, AdultPrice = -1m };

            Errors(rate).Should().Contain(nameof(PricingRateDto.AdultPrice));
        }
    }
}
