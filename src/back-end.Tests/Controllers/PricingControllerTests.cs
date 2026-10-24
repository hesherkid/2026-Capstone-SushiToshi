using System.Reflection;
using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using back_end.Controllers;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.DTO.PricingDTOs;
using back_end.Services;

namespace back_end.Tests.Controllers
{
    // Default pricing: adults $40.95 weekday, $42.95 weekend, $44.95 holiday; Fri–Sun weekends
    public class PricingControllerTests : IDisposable
    {
        private const int LocationId = 1;
        private readonly ApplicationDbContext _context;

        public PricingControllerTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _context = new ApplicationDbContext(options);

            _context.Locations.Add(new Locations { Location_Id = LocationId, Name = "Downtown", Province_Code = "AB" });
            _context.PricingProfiles.Add(new PricingProfile
            {
                Tax_Rate = 0.05m,
                Gratuity_Rate = 0.18m,
                Gratuity_Min_Party_Size = 6,
                Leftover_Fee = 15m,
                Weekend_Days = new() { DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday },
                Rates = new List<PricingRate>
                {
                    new() { Day_Type = PricingDayType.Weekday, Adult_Price = 40.95m },
                    new() { Day_Type = PricingDayType.Weekend, Adult_Price = 42.95m },
                    new() { Day_Type = PricingDayType.Holiday, Adult_Price = 44.95m },
                },
            });
            _context.SaveChanges();
            _context.ChangeTracker.Clear();
        }

        public void Dispose() => _context.Dispose();

        private PricingController Pricing() => new(
            _context,
            new PricingService(_context, new Mock<ILogger<PricingService>>().Object),
            new Mock<ILogger<PricingController>>().Object);

        // Saturday/Sunday weekends; only the weekday adult price and tax vary
        private static PricingProfileRequest Request(decimal weekdayAdult, decimal taxRate = 0.05m) => new()
        {
            Tax_Rate = taxRate,
            Gratuity_Rate = 0.18m,
            Gratuity_Min_Party_Size = 6,
            Leftover_Fee = 15m,
            Weekend_Days = new() { DayOfWeek.Sunday, DayOfWeek.Saturday },
            Rates = new()
            {
                new() { Day_Type = PricingDayType.Weekday, Adult_Price = weekdayAdult },
                new() { Day_Type = PricingDayType.Weekend, Adult_Price = 50m },
                new() { Day_Type = PricingDayType.Holiday, Adult_Price = 55m },
            },
        };

        private static T Body<T>(IActionResult result) => (T)(result as OkObjectResult)!.Value!;

        private static decimal WeekdayAdult(PricingProfileResponse profile) =>
            profile.Rates.Single(r => r.Day_Type == PricingDayType.Weekday).Adult_Price;

        private void AddHoliday(DateOnly date, HolidayStatus status)
        {
            _context.Holidays.Add(new Holiday { Province_Code = "AB", Date = date, Name = "Christmas Day", Status = status });
            _context.SaveChanges();
            _context.ChangeTracker.Clear();
        }

        // ---------- Default pricing ----------

        [Fact]
        public async Task GetDefault_ReturnsDefaultProfileWithRatesInDayOrder()
        {
            var profile = Body<PricingProfileResponse>(await Pricing().GetDefault());

            profile.Source.Should().Be("Default");
            profile.Location_Id.Should().BeNull();
            profile.Tax_Rate.Should().Be(0.05m);
            profile.Rates.Select(r => r.Day_Type).Should().Equal(
                PricingDayType.Weekday, PricingDayType.Weekend, PricingDayType.Holiday);
        }

        [Fact]
        public async Task UpdateDefault_SavesPricesAndSortsWeekendDays()
        {
            await Pricing().UpdateDefault(Request(weekdayAdult: 39.954m, taxRate: 0.05m));
            _context.ChangeTracker.Clear();

            var profile = Body<PricingProfileResponse>(await Pricing().GetDefault());
            WeekdayAdult(profile).Should().Be(39.95m);   // rounded to the cent
            profile.Weekend_Days.Should().Equal(DayOfWeek.Saturday, DayOfWeek.Sunday);   // Monday-first order
        }

        // ---------- Location pricing ----------

        [Fact]
        public async Task GetLocation_WithoutOwnPricing_ReturnsDefault()
        {
            var profile = Body<PricingProfileResponse>(await Pricing().GetLocation(LocationId));

            profile.Source.Should().Be("Default");
            WeekdayAdult(profile).Should().Be(40.95m);
        }

        [Fact]
        public async Task UpdateLocation_CreatesLocationPricingAndLeavesDefaultAlone()
        {
            var profile = Body<PricingProfileResponse>(await Pricing().UpdateLocation(LocationId, Request(45m)));
            _context.ChangeTracker.Clear();

            profile.Source.Should().Be("Location");
            profile.Location_Id.Should().Be(LocationId);
            WeekdayAdult(profile).Should().Be(45m);

            WeekdayAdult(Body<PricingProfileResponse>(await Pricing().GetDefault())).Should().Be(40.95m);
        }

        [Fact]
        public async Task UpdateLocation_Twice_UpdatesTheSameProfileAndRates()
        {
            await Pricing().UpdateLocation(LocationId, Request(45m));
            _context.ChangeTracker.Clear();
            var rateIds = await _context.PricingRates
                .Where(r => r.PricingProfile.Location_Id == LocationId)
                .Select(r => r.Pricing_Rate_Id)
                .ToListAsync();

            await Pricing().UpdateLocation(LocationId, Request(46m));
            _context.ChangeTracker.Clear();

            (await _context.PricingProfiles.CountAsync()).Should().Be(2);   // default + this location
            (await _context.PricingRates
                .Where(r => r.PricingProfile.Location_Id == LocationId)
                .Select(r => r.Pricing_Rate_Id)
                .ToListAsync()).Should().BeEquivalentTo(rateIds);
            WeekdayAdult(Body<PricingProfileResponse>(await Pricing().GetLocation(LocationId))).Should().Be(46m);
        }

        [Fact]
        public async Task UpdateLocation_UnknownLocation_Returns404AndSavesNothing()
        {
            var result = await Pricing().UpdateLocation(999, Request(45m));

            result.Should().BeOfType<NotFoundObjectResult>();
            (await _context.PricingProfiles.CountAsync()).Should().Be(1);
        }

        [Fact]
        public async Task DeleteLocation_GoesBackToDefault()
        {
            await Pricing().UpdateLocation(LocationId, Request(45m));
            _context.ChangeTracker.Clear();

            var profile = Body<PricingProfileResponse>(await Pricing().DeleteLocation(LocationId));
            _context.ChangeTracker.Clear();

            profile.Source.Should().Be("Default");
            WeekdayAdult(profile).Should().Be(40.95m);
            (await _context.PricingProfiles.CountAsync()).Should().Be(1);
            (await _context.PricingRates.CountAsync()).Should().Be(3);   // location rates went with it
        }

        [Fact]
        public async Task DeleteLocation_AlreadyOnDefault_Returns404()
        {
            (await Pricing().DeleteLocation(LocationId)).Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task GetLocations_ShowsWhichLocationsHaveTheirOwnPricing()
        {
            _context.Locations.Add(new Locations { Location_Id = 2, Name = "South", Province_Code = "AB" });
            await _context.SaveChangesAsync();
            await Pricing().UpdateLocation(LocationId, Request(45m));
            _context.ChangeTracker.Clear();

            var locations = Body<List<LocationPricingSummary>>(await Pricing().GetLocations());

            locations.Select(l => (l.Location_Id, l.Source)).Should().Equal((1, "Location"), (2, "Default"));
        }

        // ---------- Preview ----------

        [Theory]
        [InlineData(2026, 11, 12, "Weekday", 40.95)]   // Thursday
        [InlineData(2026, 11, 14, "Weekend", 42.95)]   // Saturday
        public async Task Preview_UsesTheDayOfTheWeek(int year, int month, int day, string pricingType, decimal adult)
        {
            var preview = Body<PricingPreviewResponse>(
                await Pricing().Preview(LocationId, new DateOnly(year, month, day)));

            preview.Pricing_Type.Should().Be(pricingType);
            preview.Pricing_Source.Should().Be("Default");
            preview.Adult_Price.Should().Be(adult);
        }

        [Fact]
        public async Task Preview_ApprovedHoliday_UsesHolidayPricing()
        {
            AddHoliday(new DateOnly(2026, 12, 25), HolidayStatus.Approved);

            var preview = Body<PricingPreviewResponse>(await Pricing().Preview(LocationId, new DateOnly(2026, 12, 25)));

            preview.Pricing_Type.Should().Be("Holiday");
            preview.Holiday_Name.Should().Be("Christmas Day");
            preview.Adult_Price.Should().Be(44.95m);
        }

        [Fact]
        public async Task Preview_PendingHoliday_IsNotAHolidayYet()
        {
            AddHoliday(new DateOnly(2026, 12, 25), HolidayStatus.Pending);

            var preview = Body<PricingPreviewResponse>(await Pricing().Preview(LocationId, new DateOnly(2026, 12, 25)));

            preview.Pricing_Type.Should().Be("Weekend");   // 25 Dec 2026 is a Friday
            preview.Holiday_Name.Should().BeNull();
        }

        [Fact]
        public async Task Preview_UsesTheLocationsOwnPricing()
        {
            await Pricing().UpdateLocation(LocationId, Request(45m));
            _context.ChangeTracker.Clear();

            var preview = Body<PricingPreviewResponse>(await Pricing().Preview(LocationId, new DateOnly(2026, 11, 12)));

            preview.Pricing_Source.Should().Be("Location");
            preview.Adult_Price.Should().Be(45m);
        }

        [Fact]
        public async Task Preview_UnknownLocation_Returns404()
        {
            (await Pricing().Preview(999, new DateOnly(2026, 11, 12))).Should().BeOfType<NotFoundObjectResult>();
        }

        // ---------- Authorization ----------

        [Fact]
        public void Class_RequiresLoginButNoPolicy()
        {
            // A class-level adminOnly would also apply to Preview and lock staff out
            typeof(PricingController).GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().BeNull();
        }

        [Theory]
        [InlineData(nameof(PricingController.GetDefault), "adminOnly")]
        [InlineData(nameof(PricingController.UpdateDefault), "adminOnly")]
        [InlineData(nameof(PricingController.GetLocations), "adminOnly")]
        [InlineData(nameof(PricingController.GetLocation), "adminOnly")]
        [InlineData(nameof(PricingController.UpdateLocation), "adminOnly")]
        [InlineData(nameof(PricingController.DeleteLocation), "adminOnly")]
        [InlineData(nameof(PricingController.Preview), "staffOnly")]
        public void Action_HasExpectedPolicy(string action, string policy)
        {
            typeof(PricingController).GetMethod(action)!
                .GetCustomAttributes<AuthorizeAttribute>()
                .Select(a => a.Policy)
                .Should().ContainSingle().Which.Should().Be(policy);
        }
    }
}
