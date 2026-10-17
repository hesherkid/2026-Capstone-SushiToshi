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

namespace back_end.Tests.Controllers;

// Staff account updates: deactivate/reactivate, and edits limited to staff/admin accounts
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
        _controller = new StaffController(_context, new Mock<ILogger<StaffController>>().Object)
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
            new User { User_id = 2, Email = "sue@example.com", First_name = "Sue", Last_name = "Staff", Role = UserRoles.Staff },
            new User { User_id = 3, Email = "tom@example.com", First_name = "Tom", Last_name = "Staff", Role = UserRoles.Staff, Status = UserStatus.Inactive },
            new User { User_id = 4, Email = "mia@example.com", First_name = "Mia", Last_name = "Chen", Role = UserRoles.Customer }
        );
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

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
}
