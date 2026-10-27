using System.Security.Claims;
using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using back_end.Controllers;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.DTO.StaffDTOs;
using back_end.DTO.UserDTOs;
using back_end.Services;

namespace back_end.Tests.Controllers;

// Staff account updates: deactivate/reactivate, edits limited to staff/admin accounts,
// and switching location limited to assigned locations
public class StaffControllerTests : IDisposable
{
    private const int SignedInAdminId = 1;

    private readonly ApplicationDbContext _context;
    private readonly StaffController _controller;

    public StaffControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _controller = new StaffController(_context, new Mock<ILogger<StaffController>>().Object, new LocationAccessService(_context))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, SignedInAdminId.ToString()),
                        new Claim(ClaimTypes.Role, "Admin"),
                    }, "TestAuth")),
                },
            },
        };

        _context.Users.AddRange(
            new User { User_id = 1, Email = "admin@example.com", First_name = "Ada", Last_name = "Admin", Role = UserRoles.Admin },
            new User { User_id = 2, Email = "sue@example.com", First_name = "Sue", Last_name = "Staff", Role = UserRoles.Staff, Location_id = 1 },
            new User { User_id = 3, Email = "tom@example.com", First_name = "Tom", Last_name = "Staff", Role = UserRoles.Staff, Status = UserStatus.Inactive },
            new User { User_id = 4, Email = "mia@example.com", First_name = "Mia", Last_name = "Chen", Role = UserRoles.Customer }
        );
        _context.Locations.AddRange(
            new Locations { Location_Id = 1, Name = "Northside" },
            new Locations { Location_Id = 2, Name = "Southside" },
            new Locations { Location_Id = 3, Name = "Westside" }
        );
        // Sue works at Northside and may switch to Southside, but not Westside
        _context.StaffLocations.AddRange(
            new StaffLocation { User_Id = 2, Location_Id = 1 },
            new StaffLocation { User_Id = 2, Location_Id = 2 }
        );
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    private void SignInAs(int userId, UserRoles role) =>
        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role.ToString()),
        }, "TestAuth"));

    [Theory]
    [InlineData(2, UserStatus.Inactive)]
    [InlineData(3, UserStatus.Active)]
    public async Task UpdateStaffStatus_DeactivatesAndReactivates(int userId, UserStatus status)
    {
        var result = await _controller.UpdateStaffStatus(userId, new AccountStatusUpdateDTO { Status = status });

        result.Result.Should().BeOfType<OkObjectResult>();
        (await _context.Users.FindAsync(userId))!.Status.Should().Be(status);
    }

    [Fact]
    public async Task UpdateStaffStatus_WontLetAnAdminDeactivateThemselves()
    {
        var result = await _controller.UpdateStaffStatus(SignedInAdminId, new AccountStatusUpdateDTO { Status = UserStatus.Inactive });

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        (await _context.Users.FindAsync(SignedInAdminId))!.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public async Task UpdateStaffStatus_RejectsOtherStatuses()
    {
        var result = await _controller.UpdateStaffStatus(2, new AccountStatusUpdateDTO { Status = UserStatus.Deleted });

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateStaffStatus_IgnoresCustomerAccounts()
    {
        var result = await _controller.UpdateStaffStatus(4, new AccountStatusUpdateDTO { Status = UserStatus.Inactive });

        result.Result.Should().BeOfType<NotFoundObjectResult>();
        (await _context.Users.FindAsync(4))!.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public async Task UpdateStaff_CantTurnACustomerIntoStaff()
    {
        var result = await _controller.UpdateStaff(4, new StaffMemberUpdateDTO
        {
            FirstName = "Mia",
            Role = UserRoles.Staff,
            Status = UserStatus.Active,
        });

        result.Result.Should().BeOfType<NotFoundObjectResult>();
        (await _context.Users.FindAsync(4))!.Role.Should().Be(UserRoles.Customer);
    }

    [Theory]
    [InlineData(UserRoles.Staff, UserStatus.Active)]    // demote self
    [InlineData(UserRoles.Admin, UserStatus.Inactive)]  // deactivate self
    public async Task UpdateStaff_WontLetAnAdminChangeTheirOwnRoleOrStatus(UserRoles role, UserStatus status)
    {
        var result = await _controller.UpdateStaff(SignedInAdminId, new StaffMemberUpdateDTO
        {
            FirstName = "Changed",
            Role = role,
            Status = status,
        });

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        var self = await _context.Users.FindAsync(SignedInAdminId);
        self!.Role.Should().Be(UserRoles.Admin);
        self.Status.Should().Be(UserStatus.Active);
        self.First_name.Should().Be("Ada");
    }

    [Fact]
    public async Task UpdateStaff_LetsAnAdminEditTheirOwnName()
    {
        var result = await _controller.UpdateStaff(SignedInAdminId, new StaffMemberUpdateDTO
        {
            FirstName = "Adele",
            Role = UserRoles.Admin,
            Status = UserStatus.Active,
        });

        result.Result.Should().BeOfType<OkObjectResult>();
        (await _context.Users.FindAsync(SignedInAdminId))!.First_name.Should().Be("Adele");
    }

    [Fact]
    public async Task UpdateStaff_StillUpdatesStaffAccounts()
    {
        var result = await _controller.UpdateStaff(2, new StaffMemberUpdateDTO
        {
            FirstName = "Susan",
            Role = UserRoles.Staff,
            Status = UserStatus.Active,
        });

        result.Result.Should().BeOfType<OkObjectResult>();
        (await _context.Users.FindAsync(2))!.First_name.Should().Be("Susan");
    }

    [Fact]
    public async Task UpdateStaffLocation_LetsStaffSwitchToAnAssignedLocation()
    {
        SignInAs(2, UserRoles.Staff);

        var result = await _controller.UpdateStaffLocation(2);

        result.Should().BeOfType<OkObjectResult>();
        (await _context.Users.FindAsync(2))!.Location_id.Should().Be(2);
    }

    [Fact]
    public async Task UpdateStaffLocation_WontLetStaffSwitchToALocationTheyArentAssigned()
    {
        SignInAs(2, UserRoles.Staff);

        var result = await _controller.UpdateStaffLocation(3);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        (await _context.Users.FindAsync(2))!.Location_id.Should().Be(1);
    }

    [Fact]
    public async Task UpdateStaffLocation_LetsAdminsSwitchToAnyLocation()
    {
        var result = await _controller.UpdateStaffLocation(3);

        result.Should().BeOfType<OkObjectResult>();
        (await _context.Users.FindAsync(SignedInAdminId))!.Location_id.Should().Be(3);
    }

    [Fact]
    public async Task UpdateStaffLocation_RejectsALocationThatDoesntExist()
    {
        var result = await _controller.UpdateStaffLocation(99);

        result.Should().BeOfType<NotFoundObjectResult>();
        (await _context.Users.FindAsync(SignedInAdminId))!.Location_id.Should().BeNull();
    }

    [Fact]
    public async Task CreateStaff_AssignsStaffToTheLocationTheyStartAt()
    {
        var result = await _controller.CreateStaff(new CreateStaffDTO
        {
            Email = "new@example.com",
            First_name = "Nina",
            Last_name = "New",
            Password = "StaffPass123!",
            Role = UserRoles.Staff,
            Location_Id = 2,
        });

        result.Result.Should().BeOfType<CreatedAtActionResult>();
        var nina = await _context.Users.Include(u => u.AssignedLocations).SingleAsync(u => u.Email == "new@example.com");
        nina.Location_id.Should().Be(2);
        nina.AssignedLocations.Select(a => a.Location_Id).Should().Equal(2);
    }

    [Fact]
    public async Task CreateStaff_RejectsALocationThatDoesntExist()
    {
        var result = await _controller.CreateStaff(new CreateStaffDTO
        {
            Email = "new@example.com",
            First_name = "Nina",
            Last_name = "New",
            Password = "StaffPass123!",
            Role = UserRoles.Staff,
            Location_Id = 99,
        });

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        (await _context.Users.AnyAsync(u => u.Email == "new@example.com")).Should().BeFalse();
    }

    private List<int> AssignedTo(int userId) =>
        _context.StaffLocations.Where(sl => sl.User_Id == userId).Select(sl => sl.Location_Id).OrderBy(id => id).ToList();

    private static StaffMemberUpdateDTO SueWith(List<int>? locationIds) => new()
    {
        FirstName = "Sue",
        Role = UserRoles.Staff,
        Status = UserStatus.Active,
        Location_Ids = locationIds,
    };

    [Fact]
    public async Task UpdateStaff_ReplacesAssignedLocations_AndMovesStaffOffARemovedOne()
    {
        var result = await _controller.UpdateStaff(2, SueWith([2, 3]));

        result.Result.Should().BeOfType<OkObjectResult>();
        AssignedTo(2).Should().Equal(2, 3);
        (await _context.Users.FindAsync(2))!.Location_id.Should().Be(2);
    }

    [Fact]
    public async Task UpdateStaff_KeepsTheCurrentLocation_WhenItsStillAssigned()
    {
        await _controller.UpdateStaff(2, SueWith([1, 3]));

        AssignedTo(2).Should().Equal(1, 3);
        (await _context.Users.FindAsync(2))!.Location_id.Should().Be(1);
    }

    [Fact]
    public async Task UpdateStaff_ClearsTheCurrentLocation_WhenNoneAreLeft()
    {
        await _controller.UpdateStaff(2, SueWith([]));

        AssignedTo(2).Should().BeEmpty();
        (await _context.Users.FindAsync(2))!.Location_id.Should().BeNull();
    }

    [Fact]
    public async Task UpdateStaff_LeavesAssignedLocations_WhenNoneAreSent()
    {
        await _controller.UpdateStaff(2, SueWith(null));

        AssignedTo(2).Should().Equal(1, 2);
        (await _context.Users.FindAsync(2))!.Location_id.Should().Be(1);
    }

    [Fact]
    public async Task UpdateStaff_RejectsALocationThatDoesntExist()
    {
        var result = await _controller.UpdateStaff(2, SueWith([1, 99]));

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        AssignedTo(2).Should().Equal(1, 2);
    }

    [Fact]
    public async Task CreateStaff_AssignsEveryChosenLocation_AndStartsAtTheOneGiven()
    {
        await _controller.CreateStaff(new CreateStaffDTO
        {
            Email = "new@example.com",
            First_name = "Nina",
            Last_name = "New",
            Password = "StaffPass123!",
            Role = UserRoles.Staff,
            Location_Ids = [1, 2],
            Location_Id = 2,
        });

        var nina = await _context.Users.SingleAsync(u => u.Email == "new@example.com");
        AssignedTo(nina.User_id).Should().Equal(1, 2);
        nina.Location_id.Should().Be(2);
    }

    [Fact]
    public async Task ListStaff_IncludesEachStaffMembersLocations()
    {
        var result = await _controller.ListStaff();

        var sue = ((result.Result as OkObjectResult)!.Value as IEnumerable<StaffResponseDTO>)!.Single(u => u.User_Id == 2);
        sue.Location_Id.Should().Be(1);
        sue.Location_Ids.Should().Equal(1, 2);
        sue.Locations.Should().Equal("Northside", "Southside");
    }

    [Fact]
    public async Task GetMyLocations_GivesStaffTheirAssignedLocations()
    {
        SignInAs(2, UserRoles.Staff);

        var mine = ((await _controller.GetMyLocations()).Result as OkObjectResult)!.Value as MyLocationsDTO;

        mine!.Current_Location!.Location_Id.Should().Be(1);
        mine.Locations.Select(l => l.Name).Should().Equal("Northside", "Southside");
        mine.All_Locations.Should().BeFalse();
    }

    [Fact]
    public async Task GetMyLocations_GivesStaffWithNoLocationNothing()
    {
        SignInAs(3, UserRoles.Staff);

        var mine = ((await _controller.GetMyLocations()).Result as OkObjectResult)!.Value as MyLocationsDTO;

        mine!.Current_Location.Should().BeNull();
        mine.Locations.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMyLocations_GivesAdminsEveryLocation()
    {
        var mine = ((await _controller.GetMyLocations()).Result as OkObjectResult)!.Value as MyLocationsDTO;

        mine!.Locations.Select(l => l.Name).Should().Equal("Northside", "Southside", "Westside");
        mine.All_Locations.Should().BeTrue();
    }
}
