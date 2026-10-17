using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using back_end.Controllers;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.DTO.UserDTOs;

namespace back_end.Tests.Controllers;

// Admin customer management: list, deactivate/reactivate, delete
public class CustomerControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly CustomerController _controller;

    public CustomerControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _controller = new CustomerController(_context, new Mock<ILogger<CustomerController>>().Object);

        _context.Users.AddRange(
            new User { User_id = 1, Email = "mia@example.com", First_name = "Mia", Last_name = "Chen", Role = UserRoles.Customer, Password_hash = "hash", GoogleSocial = "google-1", Is_email_confirmed = true },
            new User { User_id = 2, Email = "sam@example.com", First_name = "Sam", Last_name = "Lee", Role = UserRoles.Customer, Status = UserStatus.Inactive },
            new User { User_id = 3, Email = "gone@example.com", First_name = "Gone", Last_name = "User", Role = UserRoles.Customer, Status = UserStatus.Deleted },
            new User { User_id = 4, Email = "staff@example.com", First_name = "Sue", Last_name = "Staff", Role = UserRoles.Staff }
        );
        _context.AuthActionTokens.Add(new AuthActionToken { Id = 1, UserId = 1, TokenHash = "abc", Purpose = AuthTokenPurpose.PasswordReset, ExpiresAt = DateTime.UtcNow.AddHours(1) });
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task ListCustomers_LeavesOutDeletedCustomersAndStaff()
    {
        var result = await _controller.ListCustomers();

        var customers = (result.Result as OkObjectResult)!.Value as IEnumerable<UserResponseDTO>;
        customers!.Select(c => c.User_id).Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Theory]
    [InlineData(1, UserStatus.Inactive)]
    [InlineData(2, UserStatus.Active)]
    public async Task UpdateCustomerStatus_DeactivatesAndReactivates(int userId, UserStatus status)
    {
        var result = await _controller.UpdateCustomerStatus(userId, new AccountStatusUpdateDTO { Status = status });

        result.Result.Should().BeOfType<OkObjectResult>();
        (await _context.Users.FindAsync(userId))!.Status.Should().Be(status);
    }

    [Theory]
    [InlineData(UserStatus.Deleted)]
    [InlineData(UserStatus.Suspended)]
    public async Task UpdateCustomerStatus_RejectsOtherStatuses(UserStatus status)
    {
        var result = await _controller.UpdateCustomerStatus(1, new AccountStatusUpdateDTO { Status = status });

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        (await _context.Users.FindAsync(1))!.Status.Should().Be(UserStatus.Active);
    }

    [Theory]
    [InlineData(3)]   // already deleted
    [InlineData(4)]   // staff account
    [InlineData(99)]  // doesn't exist
    public async Task UpdateCustomerStatus_OnlyAppliesToCustomers(int userId)
    {
        var result = await _controller.UpdateCustomerStatus(userId, new AccountStatusUpdateDTO { Status = UserStatus.Inactive });

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void CustomerEndpoints_AreAdminOnly()
    {
        var authorize = typeof(CustomerController)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .Single();

        authorize.Roles.Should().Be("Admin");
    }

    [Fact]
    public async Task DeleteCustomer_AnonymisesTheAccountAndRemovesSignIn()
    {
        var result = await _controller.DeleteCustomer(1);

        result.Should().BeOfType<NoContentResult>();
        var user = await _context.Users.FindAsync(1);
        user!.Status.Should().Be(UserStatus.Deleted);
        user.Email.Should().Be("deleted-1@deleted.invalid");
        user.First_name.Should().Be("Deleted");
        user.Last_name.Should().Be("Customer");
        user.Password_hash.Should().BeNull();
        user.GoogleSocial.Should().BeNull();
        _context.AuthActionTokens.Any(t => t.UserId == 1).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteCustomer_FreesTheEmailForANewSignUp()
    {
        await _controller.DeleteCustomer(1);

        _context.Users.Any(u => u.Email == "mia@example.com").Should().BeFalse();
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public async Task DeleteCustomer_WontTouchStaffOrDeletedAccounts(int userId)
    {
        var result = await _controller.DeleteCustomer(userId);

        result.Should().BeOfType<NotFoundObjectResult>();
        (await _context.Users.FindAsync(4))!.Email.Should().Be("staff@example.com");
    }
}
