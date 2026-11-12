using System.Security.Claims;
using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using back_end.Controllers;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.Dtos;
using back_end.Helpers;
using back_end.Services.Auth;
using BCryptNet = BCrypt.Net.BCrypt;

namespace back_end.Tests.Controllers;

public class AccountControllerTests : IDisposable
{
    private const string Password = "Password1!";

    private readonly ApplicationDbContext _context;
    private readonly Mock<IAuthTokenService> _tokens = new();
    private readonly AccountController _controller;

    public AccountControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _controller = new AccountController(_context, _tokens.Object);
    }

    public void Dispose() => _context.Dispose();

    private User AddUser(UserRoles role = UserRoles.Customer, string? password = Password)
    {
        var user = new User
        {
            Email = $"user{_context.Users.Count()}@example.com",
            First_name = "Sam",
            Last_name = "Tanaka",
            Role = role,
            Password_hash = password == null ? null : BCryptNet.HashPassword(password),
            Is_email_confirmed = true,
        };
        _context.Users.Add(user);
        _context.SaveChanges();
        return user;
    }

    private void SignIn(User user, bool guest = false)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, user.Role.ToString()),
            new(ClaimTypes.NameIdentifier, user.User_id.ToString()),
            new(ClaimTypes.Email, user.Email),
        };
        if (guest) claims.Add(new Claim(GuestAccounts.TokenClaim, "true"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) }
        };
    }

    private static AccountDto AccountOf(IActionResult result) =>
        (AccountDto)result.Should().BeOfType<OkObjectResult>().Subject.Value!;

    private static int? StatusOf(IActionResult result) => (result as IStatusCodeActionResult)?.StatusCode;

    private string? HashOf(User user) =>
        _context.Users.AsNoTracking().Single(u => u.User_id == user.User_id).Password_hash;

    [Fact]
    public async Task GetAccount_ReturnsTheSignedInUsersDetails()
    {
        // Arrange
        AddUser();
        var user = AddUser(UserRoles.Staff);
        SignIn(user);

        // Act
        var account = AccountOf(await _controller.GetAccount());

        // Assert
        account.Email.Should().Be(user.Email);
        account.FirstName.Should().Be("Sam");
        account.LastName.Should().Be("Tanaka");
        account.Role.Should().Be(UserRoles.Staff);
        account.HasPassword.Should().BeTrue();
    }

    [Fact]
    public async Task GetAccount_GoogleOnlyAccount_HasNoPassword()
    {
        // Arrange
        SignIn(AddUser(password: null));

        // Act
        var account = AccountOf(await _controller.GetAccount());

        // Assert
        account.HasPassword.Should().BeFalse();
    }

    [Fact]
    public async Task Guest_IsForbidden()
    {
        // Arrange
        var guest = AddUser(password: null);
        SignIn(guest, guest: true);

        // Act
        var get = await _controller.GetAccount();
        var update = await _controller.UpdateAccount(new UpdateAccountDto { FirstName = "New", LastName = "Name" });
        var change = await _controller.ChangePassword(new ChangePasswordDto { CurrentPassword = Password, NewPassword = "NewPassword1!" });

        // Assert
        StatusOf(get).Should().Be(StatusCodes.Status403Forbidden);
        StatusOf(update).Should().Be(StatusCodes.Status403Forbidden);
        StatusOf(change).Should().Be(StatusCodes.Status403Forbidden);
        _context.Users.AsNoTracking().Single().First_name.Should().Be("Sam");
    }

    [Fact]
    public async Task GetAccount_DeletedAccount_ReturnsNotFound()
    {
        // Arrange
        var user = AddUser();
        SignIn(user);
        _context.Users.Remove(user);
        _context.SaveChanges();

        // Act
        var result = await _controller.GetAccount();

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpdateAccount_SavesTheTrimmedName()
    {
        // Arrange
        var user = AddUser();
        SignIn(user);

        // Act
        var account = AccountOf(await _controller.UpdateAccount(new UpdateAccountDto { FirstName = "  Alex ", LastName = " Kim  " }));

        // Assert
        account.FirstName.Should().Be("Alex");
        account.LastName.Should().Be("Kim");
        var saved = _context.Users.AsNoTracking().Single();
        saved.First_name.Should().Be("Alex");
        saved.Last_name.Should().Be("Kim");
    }

    [Fact]
    public async Task UpdateAccount_Staff_IsForbidden()
    {
        // Arrange
        var staff = AddUser(UserRoles.Staff);
        SignIn(staff);

        // Act
        var result = await _controller.UpdateAccount(new UpdateAccountDto { FirstName = "Alex", LastName = "Kim" });

        // Assert
        StatusOf(result).Should().Be(StatusCodes.Status403Forbidden);
        _context.Users.AsNoTracking().Single().First_name.Should().Be("Sam");
    }

    [Fact]
    public async Task UpdateAccount_Admin_ChangesTheirOwnName()
    {
        // Arrange
        SignIn(AddUser(UserRoles.Admin));

        // Act
        var account = AccountOf(await _controller.UpdateAccount(new UpdateAccountDto { FirstName = "Alex", LastName = "Kim" }));

        // Assert
        account.FirstName.Should().Be("Alex");
    }

    [Theory]
    [InlineData(UserRoles.Customer, true)]
    [InlineData(UserRoles.Admin, true)]
    [InlineData(UserRoles.Staff, false)]
    public async Task GetAccount_SaysWhetherTheNameCanBeChanged(UserRoles role, bool canChange)
    {
        // Arrange
        SignIn(AddUser(role));

        // Act
        var account = AccountOf(await _controller.GetAccount());

        // Assert
        account.CanChangeName.Should().Be(canChange);
    }

    [Fact]
    public async Task UpdateAccount_OnlyChangesTheSignedInUser()
    {
        // Arrange
        var other = AddUser();
        SignIn(AddUser());

        // Act
        await _controller.UpdateAccount(new UpdateAccountDto { FirstName = "Alex", LastName = "Kim" });

        // Assert
        _context.Users.AsNoTracking().Single(u => u.User_id == other.User_id).First_name.Should().Be("Sam");
    }

    [Theory]
    [InlineData("   ", "Kim")]
    [InlineData("Alex", "")]
    public async Task UpdateAccount_BlankName_ReturnsBadRequest(string first, string last)
    {
        // Arrange
        SignIn(AddUser());

        // Act
        var result = await _controller.UpdateAccount(new UpdateAccountDto { FirstName = first, LastName = last });

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        _context.Users.AsNoTracking().Single().First_name.Should().Be("Sam");
    }

    [Fact]
    public async Task ChangePassword_WithTheCurrentPassword_SetsTheNewOne()
    {
        // Arrange
        var user = AddUser();
        SignIn(user);

        // Act
        var result = await _controller.ChangePassword(new ChangePasswordDto { CurrentPassword = Password, NewPassword = " NewPassword1! " });

        // Assert
        result.Should().BeOfType<NoContentResult>();
        BCryptNet.Verify(" NewPassword1! ", HashOf(user)).Should().BeTrue();
        BCryptNet.Verify(Password, HashOf(user)).Should().BeFalse();
    }

    [Fact]
    public async Task ChangePassword_RevokesLinksAlreadySent()
    {
        // Arrange
        var user = AddUser();
        SignIn(user);

        // Act
        await _controller.ChangePassword(new ChangePasswordDto { CurrentPassword = Password, NewPassword = "NewPassword1!" });

        // Assert
        _tokens.Verify(t => t.RevokeAllAsync(user.User_id, It.IsAny<CancellationToken>()), Times.Once);
    }

    // A 401 would make the front end sign the user out
    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_ReturnsBadRequest()
    {
        // Arrange
        var user = AddUser();
        SignIn(user);

        // Act
        var result = await _controller.ChangePassword(new ChangePasswordDto { CurrentPassword = "Wrong1234!", NewPassword = "NewPassword1!" });

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        BCryptNet.Verify(Password, HashOf(user)).Should().BeTrue();
        _tokens.Verify(t => t.RevokeAllAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChangePassword_GoogleOnlyAccount_ReturnsBadRequest()
    {
        // Arrange
        var user = AddUser(password: null);
        SignIn(user);

        // Act
        var result = await _controller.ChangePassword(new ChangePasswordDto { CurrentPassword = "", NewPassword = "NewPassword1!" });

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        HashOf(user).Should().BeNull();
    }
}
