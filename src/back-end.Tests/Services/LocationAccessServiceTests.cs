using System.Security.Claims;
using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.Services;

namespace back_end.Tests.Services;

// Staff only get their current location; admins (and customers) get whichever location they ask for
public class LocationAccessServiceTests
{
    private const int StaffId = 1;
    private const int UnassignedStaffId = 2;
    private const int AdminId = 3;
    private const int CustomerId = 4;

    private static ApplicationDbContext NewContext()
    {
        var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        context.Locations.AddRange(
            new Locations { Location_Id = 1, Name = "Northside" },
            new Locations { Location_Id = 2, Name = "Southside" });
        context.Users.AddRange(
            new User { User_id = StaffId, Email = "staff@sushitoshi.ca", Role = UserRoles.Staff, Location_id = 1 },
            new User { User_id = UnassignedStaffId, Email = "new@sushitoshi.ca", Role = UserRoles.Staff },
            new User { User_id = AdminId, Email = "admin@sushitoshi.ca", Role = UserRoles.Admin },
            new User { User_id = CustomerId, Email = "emma@email.com", Role = UserRoles.Customer });
        context.StaffLocations.Add(new StaffLocation { User_Id = StaffId, Location_Id = 1 });
        context.SaveChanges();

        return context;
    }

    private static ClaimsPrincipal SignedInAs(int userId, UserRoles role) =>
        new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role.ToString()),
        }, "Test"));

    [Fact]
    public async Task ResolveLocation_GivesStaffTheirCurrentLocation_WhateverTheyAskFor()
    {
        using var context = NewContext();
        var service = new LocationAccessService(context);

        var scope = await service.ResolveLocationAsync(SignedInAs(StaffId, UserRoles.Staff), requestedLocationId: 2);

        scope.Should().Be(LocationScope.For(1));
    }

    [Fact]
    public async Task ResolveLocation_GivesStaffTheirCurrentLocation_WhenTheyDontAsk()
    {
        using var context = NewContext();
        var service = new LocationAccessService(context);

        var scope = await service.ResolveLocationAsync(SignedInAs(StaffId, UserRoles.Staff), requestedLocationId: null);

        scope.Should().Be(LocationScope.For(1));
    }

    [Fact]
    public async Task ResolveLocation_ForbidsStaffWithNoLocation()
    {
        using var context = NewContext();
        var service = new LocationAccessService(context);

        var scope = await service.ResolveLocationAsync(SignedInAs(UnassignedStaffId, UserRoles.Staff), requestedLocationId: 1);

        scope.IsForbidden.Should().BeTrue();
    }

    // Not "all locations": staff with no location must not see every location's data
    [Fact]
    public async Task ResolveLocation_ForbidsStaffWithNoLocation_WhenTheyDontAsk()
    {
        using var context = NewContext();
        var service = new LocationAccessService(context);

        var scope = await service.ResolveLocationAsync(SignedInAs(UnassignedStaffId, UserRoles.Staff), requestedLocationId: null);

        scope.IsForbidden.Should().BeTrue();
    }

    // Switching or an admin's change applies on the next request, without logging in again
    [Fact]
    public async Task ResolveLocation_ReadsTheLocationFreshOnEachRequest()
    {
        using var context = NewContext();
        var service = new LocationAccessService(context);
        var staff = SignedInAs(StaffId, UserRoles.Staff);

        (await service.ResolveLocationAsync(staff, null)).Should().Be(LocationScope.For(1));

        context.Users.Single(u => u.User_id == StaffId).Location_id = 2;
        context.SaveChanges();

        (await service.ResolveLocationAsync(staff, null)).Should().Be(LocationScope.For(2));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ResolveLocation_GivesAdminsTheLocationTheyAskFor(int requested)
    {
        using var context = NewContext();
        var service = new LocationAccessService(context);

        var scope = await service.ResolveLocationAsync(SignedInAs(AdminId, UserRoles.Admin), requested);

        scope.Should().Be(LocationScope.For(requested));
    }

    [Fact]
    public async Task ResolveLocation_GivesAdminsAllLocations_WhenTheyDontAsk()
    {
        using var context = NewContext();
        var service = new LocationAccessService(context);

        var scope = await service.ResolveLocationAsync(SignedInAs(AdminId, UserRoles.Admin), requestedLocationId: null);

        scope.Should().Be(LocationScope.For(null));
    }

    // Customer endpoints check the customer is in the session instead
    [Fact]
    public async Task ResolveLocation_LeavesCustomersUnlimited()
    {
        using var context = NewContext();
        var service = new LocationAccessService(context);

        var scope = await service.ResolveLocationAsync(SignedInAs(CustomerId, UserRoles.Customer), requestedLocationId: 2);

        scope.Should().Be(LocationScope.For(2));
    }

    [Fact]
    public async Task ResolveLocation_ForbidsStaffWhoArentInTheDatabase()
    {
        using var context = NewContext();
        var service = new LocationAccessService(context);

        var scope = await service.ResolveLocationAsync(SignedInAs(99, UserRoles.Staff), requestedLocationId: 1);

        scope.IsForbidden.Should().BeTrue();
    }

    [Fact]
    public async Task CanAccessLocation_LeavesCustomersUnlimited()
    {
        using var context = NewContext();
        var service = new LocationAccessService(context);

        var canAccess = await service.CanAccessLocationAsync(SignedInAs(CustomerId, UserRoles.Customer), 2);

        canAccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public async Task CanAccessLocation_LetsStaffReachOnlyTheirCurrentLocation(int itemLocationId, bool allowed)
    {
        using var context = NewContext();
        var service = new LocationAccessService(context);

        var canAccess = await service.CanAccessLocationAsync(SignedInAs(StaffId, UserRoles.Staff), itemLocationId);

        canAccess.Should().Be(allowed);
    }

    [Fact]
    public async Task CanAccessLocation_LetsStaffWithNoLocationReachNothing()
    {
        using var context = NewContext();
        var service = new LocationAccessService(context);

        var canAccess = await service.CanAccessLocationAsync(SignedInAs(UnassignedStaffId, UserRoles.Staff), 1);

        canAccess.Should().BeFalse();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task CanAccessLocation_LetsAdminsReachEveryLocation(int itemLocationId)
    {
        using var context = NewContext();
        var service = new LocationAccessService(context);

        var canAccess = await service.CanAccessLocationAsync(SignedInAs(AdminId, UserRoles.Admin), itemLocationId);

        canAccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public async Task CanSwitchToLocation_LetsStaffSwitchOnlyToTheirAssignedLocations(int locationId, bool allowed)
    {
        using var context = NewContext();
        var service = new LocationAccessService(context);

        var canSwitch = await service.CanSwitchToLocationAsync(SignedInAs(StaffId, UserRoles.Staff), locationId);

        canSwitch.Should().Be(allowed);
    }

    [Fact]
    public async Task CanSwitchToLocation_LetsStaffWithNoAssignmentsSwitchNowhere()
    {
        using var context = NewContext();
        var service = new LocationAccessService(context);

        var canSwitch = await service.CanSwitchToLocationAsync(SignedInAs(UnassignedStaffId, UserRoles.Staff), 1);

        canSwitch.Should().BeFalse();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task CanSwitchToLocation_LetsAdminsSwitchAnywhere(int locationId)
    {
        using var context = NewContext();
        var service = new LocationAccessService(context);

        var canSwitch = await service.CanSwitchToLocationAsync(SignedInAs(AdminId, UserRoles.Admin), locationId);

        canSwitch.Should().BeTrue();
    }
}
