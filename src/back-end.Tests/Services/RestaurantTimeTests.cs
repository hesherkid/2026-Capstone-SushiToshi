using System.Text.Json;
using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.Services;

namespace back_end.Tests.Services;

// Times are stored in UTC; the restaurant is in Edmonton (UTC-6 in summer, UTC-7 in winter
// until Alberta moved to UTC-6 year-round on 1 Nov 2026)
public class RestaurantTimeTests
{
    private static PricingService Pricing()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Pricing:Weekend:Days:0"] = "Friday",
                ["Pricing:Weekend:Days:1"] = "Saturday",
                ["Pricing:Weekend:Days:2"] = "Sunday",
                ["Pricing:Holiday:Dates:0"] = "2026-12-25",
            })
            .Build();
        return new PricingService(config, new Mock<ILogger<PricingService>>().Object);
    }

    private static DateTime Utc(int year, int month, int day, int hour, int minute = 0) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    [Fact]
    public void ThursdayEveningInEdmonton_IsWeekdayPricing()
    {
        // Thu 15 Oct 2026, 7pm MDT = Fri 01:00 UTC
        Pricing().GetPricingForDate(Utc(2026, 10, 16, 1)).PricingType.Should().Be("Weekday");
    }

    [Fact]
    public void SundayEveningInEdmonton_IsWeekendPricing()
    {
        // Sun 18 Oct 2026, 8pm MDT = Mon 02:00 UTC
        Pricing().GetPricingForDate(Utc(2026, 10, 19, 2)).PricingType.Should().Be("Weekend");
    }

    [Fact]
    public void ChristmasEveningInEdmonton_IsHolidayPricing()
    {
        // Sat 26 Dec 01:00 UTC is still 25 Dec in Edmonton: 7pm under the year-round UTC-6 rule,
        // or 6pm on servers whose time zone data predates it
        Pricing().GetPricingForDate(Utc(2026, 12, 26, 1)).PricingType.Should().Be("Holiday");
    }

    [Fact]
    public void ValuesWithNoTimeZone_AreTreatedAsUtc()
    {
        // How MySQL hands DateTimes back before EF marks them as UTC
        var unspecified = new DateTime(2026, 10, 16, 1, 0, 0, DateTimeKind.Unspecified);

        PricingService.ToRestaurantTime(unspecified).Should().Be(new DateTime(2026, 10, 15, 19, 0, 0));
    }

    [Fact]
    public async Task StoredTimes_ComeBackAsUtcAndSerializeWithAZ()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using (var context = new ApplicationDbContext(options))
        {
            context.DiningSessions.Add(new DiningSession
            {
                Session_Id = 1,
                Menu_Id = 1,
                Location_Id = 1,
                Started_At = new DateTime(2026, 10, 18, 4, 25, 59, DateTimeKind.Unspecified),
            });
            await context.SaveChangesAsync();
        }

        await using (var context = new ApplicationDbContext(options))
        {
            var session = await context.DiningSessions.AsNoTracking().SingleAsync();

            session.Started_At.Kind.Should().Be(DateTimeKind.Utc);
            JsonSerializer.Serialize(session.Started_At).Should().Be("\"2026-10-18T04:25:59Z\"");
        }
    }

    [Fact]
    public async Task LocalTimes_AreSavedAsUtc()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var before = DateTime.UtcNow;

        await using (var context = new ApplicationDbContext(options))
        {
            // ServiceRequest.Created_At used to default to DateTime.Now (the server's local clock)
            context.Users.Add(new User { User_id = 1, Email = "a@example.com", First_name = "A", Last_name = "B", Created_at = DateTime.Now });
            await context.SaveChangesAsync();
        }

        await using (var context = new ApplicationDbContext(options))
        {
            var created = (await context.Users.AsNoTracking().SingleAsync()).Created_at;
            created.Should().BeCloseTo(before, TimeSpan.FromSeconds(5));
        }
    }
}
