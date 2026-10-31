using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using back_end.domain.DbContexts;
using back_end.domain.enums;
using back_end.domain.Seeders;

namespace back_end.Tests.Seeders;

// Seeded staff are assigned to the location they start working at
public class StaffLocationSeedTests
{
    private static ApplicationDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static IHostEnvironment Environment(string name) =>
        Mock.Of<IHostEnvironment>(e => e.EnvironmentName == name);

    [Fact]
    public void UserSeeder_AssignsEachStaffMemberToTheirCurrentLocation()
    {
        using var context = NewContext();

        new UserSeeder(context, new Mock<ILogger<UserSeeder>>().Object, Environment(Environments.Development)).Seed();
        context.SaveChanges();

        var staff = context.Users
            .Include(u => u.AssignedLocations)
            .Where(u => u.Role == UserRoles.Staff)
            .ToList();

        staff.Should().NotBeEmpty();
        staff.Should().OnlyContain(u =>
            u.Location_id != null
            && u.AssignedLocations.Select(a => a.Location_Id).SequenceEqual(new[] { u.Location_id.Value }));
    }

    [Fact]
    public void UserSeeder_AssignsNoLocationsToCustomers()
    {
        using var context = NewContext();

        new UserSeeder(context, new Mock<ILogger<UserSeeder>>().Object, Environment(Environments.Development)).Seed();
        context.SaveChanges();

        context.StaffLocations.Should().OnlyContain(sl => sl.User.Role == UserRoles.Staff);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void UserSeeder_OutsideDevelopment_AddsNoAccounts(string environment)
    {
        using var context = NewContext();

        new UserSeeder(context, new Mock<ILogger<UserSeeder>>().Object, Environment(environment)).Seed();
        context.SaveChanges();

        context.Users.Should().BeEmpty();
        context.StaffLocations.Should().BeEmpty();
    }
}
