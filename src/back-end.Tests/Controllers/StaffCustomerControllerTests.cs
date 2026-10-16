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

// Customer management endpoints on StaffController: list and delete
public class StaffCustomerControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly StaffController _controller;

    public StaffCustomerControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _controller = new StaffController(_context, new Mock<ILogger<StaffController>>().Object);

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
