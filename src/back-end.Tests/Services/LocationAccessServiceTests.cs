using System.Security.Claims;
using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.Services;

namespace back_end.Tests.Services;

// Staff only get their current location; admins get whichever location they ask for
public class LocationAccessServiceTests
{
    private const int StaffId = 1;
    private const int UnassignedStaffId = 2;
    private const int AdminId = 3;

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
            new User { User_id = AdminId, Email = "admin@sushitoshi.ca", Role = UserRoles.Admin });
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

        var location = await service.ResolveLocationAsync(SignedInAs(StaffId, UserRoles.Staff), requestedLocationId: 2);

        location.Should().Be(1);
    }

    [Fact]
    public async Task ResolveLocation_GivesStaffTheirCurrentLocation_WhenTheyDontAsk()
    {
        using var context = NewContext();
        var service = new LocationAccessService(context);

        var location = await service.ResolveLocationAsync(SignedInAs(StaffId, UserRoles.Staff), requestedLocationId: null);

        location.Should().Be(1);
    }

    [Fact]
    public async Task ResolveLocation_GivesNothing_ToStaffWithNoLocation()
    {
        using var context = NewContext();
        var service = new LocationAccessService(context);

        var location = await service.ResolveLocationAsync(SignedInAs(UnassignedStaffId, UserRoles.Staff), requestedLocationId: 1);

        location.Should().BeNull();
    }

    // Switching or an admin's change applies on the next request, without logging in again
    [Fact]
    public async Task ResolveLocation_ReadsTheLocationFreshOnEachRequest()
    {
        using var context = NewContext();
        var service = new LocationAccessService(context);
        var staff = SignedInAs(StaffId, UserRoles.Staff);

        (await service.ResolveLocationAsync(staff, null)).Should().Be(1);

        context.Users.Single(u => u.User_id == StaffId).Location_id = 2;
        context.SaveChanges();

        (await service.ResolveLocationAsync(staff, null)).Should().Be(2);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ResolveLocation_GivesAdminsTheLocationTheyAskFor(int requested)
    {
        using var context = NewContext();
        var service = new LocationAccessService(context);

        var location = await service.ResolveLocationAsync(SignedInAs(AdminId, UserRoles.Admin), requested);

        location.Should().Be(requested);
    }

    [Fact]
    public async Task ResolveLocation_GivesNothing_ToAdminsWhoDontAsk()
    {
        using var context = NewContext();
        var service = new LocationAccessService(context);

        var location = await service.ResolveLocationAsync(SignedInAs(AdminId, UserRoles.Admin), requestedLocationId: null);

        location.Should().BeNull();
    }

    [Fact]
    public async Task ResolveLocation_GivesNothing_WhenTheUserIsntInTheDatabase()
    {
        using var context = NewContext();
        var service = new LocationAccessService(context);

        var location = await service.ResolveLocationAsync(SignedInAs(99, UserRoles.Staff), requestedLocationId: 1);

        location.Should().BeNull();
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
}
