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
using back_end.Dtos.Auth;
using back_end.Services.Auth;
using back_end.Services.Email;
using BCryptNet = BCrypt.Net.BCrypt;

namespace back_end.Tests.Controllers;

// POST /api/auth/verify-email and /api/auth/reset-password: a link submitted twice at once
public class AuthTokenLinkTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IAuthTokenService> _tokens = new();
    private readonly AuthController _auth;
    private readonly User _user;

    public AuthTokenLinkTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _auth = new AuthController(
            _context,
            new ConfigurationBuilder().Build(),
            new Mock<IEmailService>().Object,
            _tokens.Object,
            Options.Create(new AuthEmailSettings()));

        _user = new User
        {
            Email = "sam@example.com",
            Password_hash = BCryptNet.HashPassword("OldPassword1!"),
            Role = UserRoles.Customer,
            Status = UserStatus.Active,
            Is_email_confirmed = false,
        };
        _context.Users.Add(_user);
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    // Both requests find the unused token; only one can consume it
    private void TokenFoundButConsumed(bool consumed)
    {
        _tokens
            .Setup(t => t.FindValidAsync("link", It.IsAny<AuthTokenPurpose>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthActionToken { UserId = _user.User_id });
        _tokens
            .Setup(t => t.TryConsumeAsync(It.IsAny<AuthActionToken>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(consumed);
    }

    [Fact]
    public async Task VerifyEmail_ConfirmsTheEmailForTheRequestThatConsumesTheToken()
    {
        TokenFoundButConsumed(true);

        var result = await _auth.VerifyEmail(new VerifyEmailDto { Token = "link" });

        result.Should().BeOfType<OkObjectResult>();
        (await _context.Users.SingleAsync()).Is_email_confirmed.Should().BeTrue();
    }

    // The losing request gets a 400, not a 500
    [Fact]
    public async Task VerifyEmail_RejectsATokenAnotherRequestJustUsed()
    {
        TokenFoundButConsumed(false);

        var result = await _auth.VerifyEmail(new VerifyEmailDto { Token = "link" });

        result.Should().BeOfType<BadRequestObjectResult>();
        (await _context.Users.SingleAsync()).Is_email_confirmed.Should().BeFalse();
    }

    [Fact]
    public async Task ResetPassword_RejectsATokenAnotherRequestJustUsed()
    {
        TokenFoundButConsumed(false);

        var result = await _auth.ResetPassword(new ResetPasswordDto { Token = "link", NewPassword = "NewPassword1!" });

        result.Should().BeOfType<BadRequestObjectResult>();
        BCryptNet.Verify("OldPassword1!", (await _context.Users.SingleAsync()).Password_hash).Should().BeTrue();
    }
}
