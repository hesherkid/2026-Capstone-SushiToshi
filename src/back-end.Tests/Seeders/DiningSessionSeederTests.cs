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
    [Fact]
    public void Seed_NeverGivesATableOrGroupTwoActiveSessions()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var context = new ApplicationDbContext(options);

        // Fewer seats (4 tables + 2 groups) than the 10 active sessions the seeder wants,
        // which used to force repeats
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

        new DiningSessionSeeder(context, new Mock<ILogger<DiningSessionSeeder>>().Object).Seed();
        context.SaveChanges(); // the seeder adds sessions; its caller saves them

        var active = context.DiningSessions.Where(s => s.Ended_At == null).ToList();
        active.Should().NotBeEmpty();
        active.Count.Should().BeLessThanOrEqualTo(6, "there are only 6 seats");
        active.Where(s => s.Table_Id != null).Select(s => s.Table_Id).Should().OnlyHaveUniqueItems();
        active.Where(s => s.TableGroup_Id != null).Select(s => s.TableGroup_Id).Should().OnlyHaveUniqueItems();
    }
}
