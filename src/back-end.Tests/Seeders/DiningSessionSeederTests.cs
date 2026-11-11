using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.Seeders;

namespace back_end.Tests.Seeders;

public class DiningSessionSeederTests
{
    /// <summary>
    /// One location with 4 single tables and 2 table groups (6 seats).
    /// That is fewer than the 10 active sessions the seeder wants, which used to force repeats.
    /// </summary>
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new ApplicationDbContext(options);

        context.Locations.Add(new Locations { Location_Id = 1, Name = "Downtown" });
        context.Menus.Add(new Menu { Menu_id = 1, Name = "Dinner" });
        context.TableGroups.AddRange(
            new TableGroup { TableGroup_Id = 1, Group_Name = "Patio", Location_Id = 1 },
            new TableGroup { TableGroup_Id = 2, Group_Name = "Booths", Location_Id = 1 });
        for (var id = 1; id <= 4; id++)
        {
            context.Tables.Add(new TableEntity { Table_Id = id, table_number = id, Location_Id = 1, is_active = true });
        }
        context.SaveChanges();

        return context;
    }

    private static void RunSeeder(ApplicationDbContext context)
    {
        new DiningSessionSeeder(context, new Mock<ILogger<DiningSessionSeeder>>().Object).Seed();
        context.SaveChanges(); // the seeder adds sessions; its caller saves them
    }

    [Fact]
    public void Seed_NeverGivesATableOrGroupTwoActiveSessions()
    {
        using var context = CreateContext();
        RunSeeder(context);

        var active = context.DiningSessions.Where(s => s.Ended_At == null).ToList();
        active.Should().NotBeEmpty();
        active.Count.Should().BeLessThanOrEqualTo(6, "there are only 6 seats");
        active.Where(s => s.Table_Id != null).Select(s => s.Table_Id).Should().OnlyHaveUniqueItems();
        active.Where(s => s.TableGroup_Id != null).Select(s => s.TableGroup_Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Seed_ActiveSessionsStartedWithinTheLast90Minutes()
    {
        using var context = CreateContext();
        RunSeeder(context);
        var now = DateTime.UtcNow;

        var active = context.DiningSessions.Where(s => s.Ended_At == null).ToList();
        active.Should().NotBeEmpty();
        active.Should().AllSatisfy(s =>
        {
            s.Started_At.Should().BeOnOrBefore(now, "a party can't be seated in the future");
            (now - s.Started_At).TotalMinutes.Should().BeLessThanOrEqualTo(90,
                "an active party shouldn't have been seated longer than the 90 minute visit limit");
        });
    }

    [Fact]
    public void Seed_ActiveSessionsFirstOrderIsBetweenSeatingAndNow()
    {
        using var context = CreateContext();
        RunSeeder(context);
        var now = DateTime.UtcNow;

        var active = context.DiningSessions.Where(s => s.Ended_At == null).ToList();
        active.Should().AllSatisfy(s =>
        {
            s.First_Order_At.Should().BeOnOrAfter(s.Started_At, "you can't order before you sit down");
            s.First_Order_At.Should().BeOnOrBefore(now, "a first order can't be in the future");
        });
    }

    [Fact]
    public void Seed_KeepsHistoricalSessionsForAnalytics()
    {
        using var context = CreateContext();
        RunSeeder(context);

        var historical = context.DiningSessions.Where(s => s.Ended_At != null).ToList();
        historical.Should().NotBeEmpty("past visits are needed for the analytics reports");
        historical.Should().AllSatisfy(s => s.Ended_At.Should().BeAfter(s.Started_At));
    }

    [Fact]
    public void Seed_HistoricalSessionsHaveRealisticDurations()
    {
        using var context = CreateContext();
        RunSeeder(context);

        var historical = context.DiningSessions.Where(s => s.Ended_At != null).ToList();
        historical.Should().AllSatisfy(s =>
            (s.Ended_At!.Value - s.Started_At).TotalMinutes.Should().BeLessThanOrEqualTo(120,
                "no party should stay 3+ hours"));
        historical.Should().Contain(s => (s.Ended_At!.Value - s.Started_At).TotalMinutes > 90,
            "a few visits should run over the limit so the 'over time limit' analytics aren't empty");
    }
}
