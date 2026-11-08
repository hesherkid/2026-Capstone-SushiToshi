using System.ComponentModel.DataAnnotations;
using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using back_end.Configurations;
using back_end.Controllers;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.Dtos;
using back_end.Services.Auth;
using back_end.Services.Email;
using BCryptNet = BCrypt.Net.BCrypt;

namespace back_end.Tests.Controllers;

// Every endpoint that sets a password uses one rule: 8 to 200 characters, not only whitespace, never trimmed
public class PasswordRuleTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IAuthTokenService> _tokens = new();
    private readonly AuthController _auth;

    public PasswordRuleTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "test-signing-key-that-is-long-enough-for-hmac-sha256",
                ["Jwt:Issuer"] = "test",
                ["Jwt:Audience"] = "test",
                ["Jwt:ExpiresInMinutes"] = "60",
                ["Jwt:StaffExpiresInMinutes"] = "600",
                ["Jwt:AdminExpiresInMinutes"] = "90",
            })
            .Build();

        _tokens
            .Setup(t => t.CreateAsync(It.IsAny<int>(), It.IsAny<AuthTokenPurpose>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("token");
        _tokens
            .Setup(t => t.TryConsumeAsync(It.IsAny<AuthActionToken>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _auth = new AuthController(
            _context,
            config,
            new Mock<IEmailService>().Object,
            _tokens.Object,
            Options.Create(new AuthEmailSettings()));
    }

    public void Dispose() => _context.Dispose();

    private async Task<User> AddConfirmedUser(string password)
    {
        var user = new User
        {
            Email = "sam@example.com",
            Password_hash = BCryptNet.HashPassword(password),
            Role = UserRoles.Customer,
            Status = UserStatus.Active,
            Is_email_confirmed = true,
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private static bool IsValid(object dto) =>
        Validator.TryValidateObject(dto, new ValidationContext(dto), null, validateAllProperties: true);

    // A trailing space from a password manager must be kept, or the same password won't log in
    [Fact]
    public async Task Register_KeepsSurroundingSpacesInThePassword()
    {
        var result = await _auth.CreateUser(new RegisterDto
        {
            UserEmail = "sam@example.com",
            UserPassword = " Password1! ",
            FirstName = "Sam",
            LastName = "Tanaka",
        });

        result.Should().BeOfType<OkObjectResult>();
        var hash = (await _context.Users.SingleAsync()).Password_hash;
        BCryptNet.Verify(" Password1! ", hash).Should().BeTrue();
        BCryptNet.Verify("Password1!", hash).Should().BeFalse();
    }

    // Spaces count toward the minimum since they are part of the password
    [Theory]
    [InlineData("Passwd1", false)]
    [InlineData("Passwd1 ", true)]
    public async Task Register_RequiresEightCharacters(string password, bool accepted)
    {
        var result = await _auth.CreateUser(new RegisterDto
        {
            UserEmail = "sam@example.com",
            UserPassword = password,
            FirstName = "Sam",
            LastName = "Tanaka",
        });

        if (accepted) result.Should().BeOfType<OkObjectResult>();
        else result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Register_RejectsPasswordsOver200Characters()
    {
        var result = await _auth.CreateUser(new RegisterDto
        {
            UserEmail = "sam@example.com",
            UserPassword = new string('a', 201),
            FirstName = "Sam",
            LastName = "Tanaka",
        });

        result.Should().BeOfType<BadRequestObjectResult>();
        _context.Users.Should().BeEmpty();
    }

    [Fact]
    public async Task Register_RejectsAPasswordOfOnlyWhitespace()
    {
        var result = await _auth.CreateUser(new RegisterDto
        {
            UserEmail = "sam@example.com",
            UserPassword = " \t        ",
            FirstName = "Sam",
            LastName = "Tanaka",
        });

        result.Should().BeOfType<BadRequestObjectResult>();
        _context.Users.Should().BeEmpty();
    }

    [Fact]
    public async Task Login_TreatsSpacesAsPartOfThePassword()
    {
        await AddConfirmedUser("Password1! ");

        var withSpace = await _auth.Login(new LoginDto { UserEmail = "sam@example.com", UserPassword = "Password1! " });
        var withoutSpace = await _auth.Login(new LoginDto { UserEmail = "sam@example.com", UserPassword = "Password1!" });

        withSpace.Should().BeOfType<OkObjectResult>();
        withoutSpace.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task ResetPassword_KeepsSurroundingSpacesInThePassword()
    {
        var user = await AddConfirmedUser("OldPassword1!");
        _tokens
            .Setup(t => t.FindValidAsync("reset", AuthTokenPurpose.PasswordReset, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthActionToken { UserId = user.User_id, Purpose = AuthTokenPurpose.PasswordReset });

        await _auth.ResetPassword(new ResetPasswordDto { Token = "reset", NewPassword = " NewPassword1! " });

        var login = await _auth.Login(new LoginDto { UserEmail = "sam@example.com", UserPassword = " NewPassword1! " });
        login.Should().BeOfType<OkObjectResult>();
    }

    // These DTOs are checked by [ApiController] before the endpoint runs
    [Theory]
    [InlineData(7, false)]
    [InlineData(8, true)]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void PasswordDtos_AllowEightTo200Characters(int length, bool valid)
    {
        var password = new string('a', length);

        IsValid(new ResetPasswordDto { Token = "reset", NewPassword = password }).Should().Be(valid);
        IsValid(new CreateStaffDto { Email = "sam@example.com", First_name = "Sam", Last_name = "Tanaka", Password = password }).Should().Be(valid);
        IsValid(new ResetPasswordRequestDto { New_password = password }).Should().Be(valid);
        IsValid(new StaffMemberChangePasswordDto { New_password = password, Current_password = "Current1!" }).Should().Be(valid);
    }

    // [Required] rejects whitespace-only strings; staff create checks this in the controller instead
    [Fact]
    public void PasswordDtos_RejectAPasswordOfOnlyWhitespace()
    {
        var password = " \t        ";

        IsValid(new ResetPasswordDto { Token = "reset", NewPassword = password }).Should().BeFalse();
        IsValid(new ResetPasswordRequestDto { New_password = password }).Should().BeFalse();
        IsValid(new StaffMemberChangePasswordDto { New_password = password, Current_password = "Current1!" }).Should().BeFalse();
    }
}
