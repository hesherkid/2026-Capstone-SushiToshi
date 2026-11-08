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
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;

namespace back_end.Tests.Controllers;

public class AuthControllerTests : IDisposable
{
    private const string Password = "Password1!";
    private const string FrontendUrl = "https://app.test/";

    private readonly ApplicationDbContext _context;
    private readonly Mock<IEmailService> _email = new();
    private readonly Mock<IAuthTokenService> _tokens = new();

    public AuthControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _tokens
            .Setup(t => t.CreateAsync(It.IsAny<int>(), It.IsAny<AuthTokenPurpose>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("tok");
        _tokens
            .Setup(t => t.TryConsumeAsync(It.IsAny<AuthActionToken>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

    }
    public void Dispose() => _context.Dispose();

    private static readonly Dictionary<string, string?> FullConfig = new()
    {
        ["Jwt:Key"] = "test-signing-key-that-is-long-enough-for-hmac-sha256",
        ["Jwt:Issuer"] = "test",
        ["Jwt:Audience"] = "test",
        ["Jwt:ExpiresInMinutes"] = "60",
        ["Google:ClientId"] = "client-id.apps.googleusercontent.com",
    };

    private AuthController Controller(Dictionary<string, string?>? config = null) => new(
        _context,
        new ConfigurationBuilder().AddInMemoryCollection(config ?? FullConfig).Build(),
        _email.Object,
        _tokens.Object,
        Options.Create(new AuthEmailSettings { FrontendBaseUrl = FrontendUrl }));

    private async Task<User> AddUser(
        string email = "sam@example.com",
        string? password = Password,
        UserStatus status = UserStatus.Active,
        bool confirmed = true,
        UserRoles role = UserRoles.Customer)
    {
        var user = new User
        {
            Email = email,
            Password_hash = password == null ? null : BCryptNet.HashPassword(password),
            First_name = "Sam",
            Last_name = "Smith",
            Role = role,
            Status = status,
            Is_email_confirmed = confirmed,
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private static JsonElement Body(IActionResult result) =>
        JsonSerializer.SerializeToElement(((ObjectResult)result).Value);

    private static string? Message(IActionResult result) =>
        Body(result).GetProperty("message").GetString();

    private void NoEmailSent() => _email.VerifyNoOtherCalls();

    [Fact]
    public async Task Login_UnknownEmail_ReturnsUnauthorized()
    {
        var result = await Controller().Login(new LoginDto { UserEmail = "nobody@example.com", UserPassword = Password });

        result.Should().BeOfType<UnauthorizedObjectResult>();
        Message(result).Should().Be("Invalid email or password");
    }

    // Guests and Google-only accounts have no password, so no password can open them
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Login_AccountWithoutAPassword_ReturnsUnauthorized(string? storedHash)
    {
        var user = await AddUser();
        user.Password_hash = storedHash;
        await _context.SaveChangesAsync();

        var result = await Controller().Login(new LoginDto { UserEmail = "sam@example.com", UserPassword = "" });

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Login_MissingFields_ReturnsUnauthorizedInsteadOfThrowing()
    {
        await AddUser();

        var result = await Controller().Login(new LoginDto { UserEmail = null, UserPassword = null });

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Login_IgnoresEmailCaseAndSpaces()
    {
        await AddUser();

        var result = await Controller().Login(new LoginDto { UserEmail = "  SAM@Example.COM ", UserPassword = Password });

        result.Should().BeOfType<OkObjectResult>();
    }

    [Theory]
    [InlineData(UserStatus.Inactive)]
    [InlineData(UserStatus.Suspended)]
    [InlineData(UserStatus.Deleted)]
    public async Task Login_AccountThatIsNotActive_Returns403(UserStatus status)
    {
        await AddUser(status: status);

        var result = await Controller().Login(new LoginDto { UserEmail = "sam@example.com", UserPassword = Password });

        ((ObjectResult)result).StatusCode.Should().Be(403);
        Message(result).Should().Be("Account is not active");
    }

    [Fact]
    public async Task Login_UnverifiedEmail_Returns403WithTheVerificationFlag()
    {
        await AddUser(confirmed: false);

        var result = await Controller().Login(new LoginDto { UserEmail = "sam@example.com", UserPassword = Password });

        ((ObjectResult)result).StatusCode.Should().Be(403);
        Body(result).GetProperty("requires_email_verification").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Login_RecordsTheLastInteraction()
    {
        var user = await AddUser();

        await Controller().Login(new LoginDto { UserEmail = "sam@example.com", UserPassword = Password });

        (await _context.Users.SingleAsync(u => u.User_id == user.User_id))
            .Last_Interaction_at.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Login_WithoutASigningKey_ReturnsInvalidOperationException()
    {
        await AddUser();

        var act = () => Controller(new Dictionary<string, string?>())
            .Login(new LoginDto { UserEmail = "sam@example.com", UserPassword = Password });

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Jwt:Key*");
    }

    public static TheoryData<string?, string?, string?, string?> MissingFields => new()
    {
        { null, Password, "Sam", "Smith" },
        { "sam@example.com", null, "Sam", "Smith" },
        { "sam@example.com", Password, null, "Smith" },
        { "sam@example.com", Password, "Sam", null },
        { "   ", Password, "Sam", "Smith" },
        { "sam@example.com", Password, "   ", "Smith" },
        { "sam@example.com", Password, "Sam", "   " },
    };

    [Theory]
    [MemberData(nameof(MissingFields))]
    public async Task Register_MissingOrBlankField_ReturnsBadRequestAndCreatesNothing(
        string? email, string? password, string? first, string? last)
    {
        var result = await Controller().CreateUser(new RegisterDto
        {
            UserEmail = email,
            UserPassword = password,
            FirstName = first,
            LastName = last,
        });

        result.Should().BeOfType<BadRequestObjectResult>();
        _context.Users.Should().BeEmpty();
        NoEmailSent();
    }

    [Fact]
    public async Task Register_NullBody_ReturnsBadRequest()
    {
        var result = await Controller().CreateUser(null!);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Register_EmailAlreadyUsed_ReturnsConflictEvenWithDifferentCasing()
    {
        await AddUser();

        var result = await Controller().CreateUser(new RegisterDto
        {
            UserEmail = " Sam@Example.com ",
            UserPassword = Password,
            FirstName = "Other",
            LastName = "Person",
        });

        result.Should().BeOfType<ConflictObjectResult>();
        (await _context.Users.CountAsync()).Should().Be(1);
        NoEmailSent();
    }

    [Fact]
    public async Task Register_EmailsAVerificationLinkForNewAccount()
    {
        _tokens
            .Setup(t => t.CreateAsync(It.IsAny<int>(), AuthTokenPurpose.EmailVerification, It.IsAny<CancellationToken>()))
            .ReturnsAsync("a b+c");

        await Controller().CreateUser(new RegisterDto
        {
            UserEmail = "sam@example.com",
            UserPassword = Password,
            FirstName = "Sam",
            LastName = "Tanaka",
        });

        var user = await _context.Users.SingleAsync();
        _tokens.Verify(t => t.CreateAsync(user.User_id, AuthTokenPurpose.EmailVerification, It.IsAny<CancellationToken>()), Times.Once);
        _email.Verify(e => e.SendEmailVerificationAsync(
            "sam@example.com",
            "https://app.test/auth/verify-email?token=a%20b%2Bc",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task VerifyEmail_UnknownOrExpiredToken_ReturnsBadRequest()
    {
        await AddUser(confirmed: false);

        var result = await Controller().VerifyEmail(new VerifyEmailDto { Token = "nope" });

        result.Should().BeOfType<BadRequestObjectResult>();
        Message(result).Should().Be("Invalid or expired verification token");
        (await _context.Users.SingleAsync()).Is_email_confirmed.Should().BeFalse();
        _tokens.Verify(t => t.TryConsumeAsync(It.IsAny<AuthActionToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task VerifyEmail_TokenForAnAccountThatNoLongerExists_ReturnsBadRequest()
    {
        _tokens
            .Setup(t => t.FindValidAsync("link", AuthTokenPurpose.EmailVerification, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthActionToken { UserId = 9999 });

        var result = await Controller().VerifyEmail(new VerifyEmailDto { Token = "link" });

        result.Should().BeOfType<BadRequestObjectResult>();
        Message(result).Should().Be("Invalid verification token");
    }

    // A password-reset link must not be used to verify an email
    [Fact]
    public async Task VerifyEmail_OnlyAcceptsVerificationTokens()
    {
        var user = await AddUser(confirmed: false);
        _tokens
            .Setup(t => t.FindValidAsync("link", AuthTokenPurpose.PasswordReset, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthActionToken { UserId = user.User_id, Purpose = AuthTokenPurpose.PasswordReset });

        var result = await Controller().VerifyEmail(new VerifyEmailDto { Token = "link" });

        result.Should().BeOfType<BadRequestObjectResult>();
        (await _context.Users.SingleAsync()).Is_email_confirmed.Should().BeFalse();
    }

    [Fact]
    public async Task ForgotPassword_KnownAccount_EmailsAResetLink()
    {
        var user = await AddUser();

        var result = await Controller().ForgotPassword(new ForgotPasswordDto { Email = " SAM@example.com " });

        result.Should().BeOfType<OkObjectResult>();
        _tokens.Verify(t => t.CreateAsync(user.User_id, AuthTokenPurpose.PasswordReset, It.IsAny<CancellationToken>()), Times.Once);
        _email.Verify(e => e.SendPasswordResetAsync(
            "sam@example.com",
            "https://app.test/auth/reset-password?token=tok",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // Google-only and guest accounts have no password to reset
    [Fact]
    public async Task ForgotPassword_AccountWithoutAPassword_SendsNothing()
    {
        await AddUser(password: null);

        var result = await Controller().ForgotPassword(new ForgotPasswordDto { Email = "sam@example.com" });

        result.Should().BeOfType<OkObjectResult>();
        NoEmailSent();
    }

    [Fact]
    public async Task ResetPassword_UnknownOrExpiredToken_ReturnsBadRequestAndKeepsThePassword()
    {
        await AddUser();

        var result = await Controller().ResetPassword(new ResetPasswordDto { Token = "nope", NewPassword = "NewPassword1!" });

        result.Should().BeOfType<BadRequestObjectResult>();
        Message(result).Should().Be("Invalid or expired reset token");
        BCryptNet.Verify(Password, (await _context.Users.SingleAsync()).Password_hash).Should().BeTrue();
    }

    [Fact]
    public async Task ResetPassword_TokenForAnAccountThatNoLongerExists_ReturnsBadRequest()
    {
        _tokens
            .Setup(t => t.FindValidAsync("link", AuthTokenPurpose.PasswordReset, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthActionToken { UserId = 9999 });

        var result = await Controller().ResetPassword(new ResetPasswordDto { Token = "link", NewPassword = "NewPassword1!" });

        result.Should().BeOfType<BadRequestObjectResult>();
        Message(result).Should().Be("Invalid reset token");
    }

    [Fact]
    public async Task ResetPassword_ValidToken_ReplacesThePassword()
    {
        var user = await AddUser();
        var token = new AuthActionToken { UserId = user.User_id, Purpose = AuthTokenPurpose.PasswordReset };
        _tokens
            .Setup(t => t.FindValidAsync("link", AuthTokenPurpose.PasswordReset, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        var result = await Controller().ResetPassword(new ResetPasswordDto { Token = "link", NewPassword = "NewPassword1!" });

        result.Should().BeOfType<OkObjectResult>();
        _tokens.Verify(t => t.TryConsumeAsync(token, It.IsAny<CancellationToken>()), Times.Once);
        var hash = (await _context.Users.SingleAsync()).Password_hash;
        BCryptNet.Verify("NewPassword1!", hash).Should().BeTrue();
        BCryptNet.Verify(Password, hash).Should().BeFalse();
    }

    // An email-verification link must not be used to reset a password
    [Fact]
    public async Task ResetPassword_OnlyAcceptsResetTokens()
    {
        var user = await AddUser();
        _tokens
            .Setup(t => t.FindValidAsync("link", AuthTokenPurpose.EmailVerification, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthActionToken { UserId = user.User_id, Purpose = AuthTokenPurpose.EmailVerification });

        var result = await Controller().ResetPassword(new ResetPasswordDto { Token = "link", NewPassword = "NewPassword1!" });

        result.Should().BeOfType<BadRequestObjectResult>();
        BCryptNet.Verify(Password, (await _context.Users.SingleAsync()).Password_hash).Should().BeTrue();
    }

    [Fact]
    public async Task ResendVerification_UnverifiedAccount_EmailsANewLink()
    {
        var user = await AddUser(confirmed: false);

        var result = await Controller().ResendVerification(new ResendVerificationDto { Email = " Sam@Example.com" });

        result.Should().BeOfType<OkObjectResult>();
        _tokens.Verify(t => t.CreateAsync(user.User_id, AuthTokenPurpose.EmailVerification, It.IsAny<CancellationToken>()), Times.Once);
        _email.Verify(e => e.SendEmailVerificationAsync(
            "sam@example.com",
            "https://app.test/auth/verify-email?token=tok",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("sam@example.com")]     // already verified
    [InlineData("nobody@example.com")]  // no such account
    public async Task ResendVerification_NothingToVerify_SendsNothing(string email)
    {
        await AddUser(confirmed: true);

        var result = await Controller().ResendVerification(new ResendVerificationDto { Email = email });

        result.Should().BeOfType<OkObjectResult>();
        Message(result).Should().Be("If this account needs verification, a new email will be sent.");
        NoEmailSent();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Google_NoToken_ReturnsBadRequest(string? idToken)
    {
        var result = await Controller().GoogleLogin(new GoogleTokenDto { IdToken = idToken! });

        result.Should().BeOfType<BadRequestObjectResult>();
        Message(result).Should().Be("Token is required");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Google_ClientIdNotConfigured_Returns500WithoutDetails(string? clientId)
    {
        var config = new Dictionary<string, string?>(FullConfig) { ["Google:ClientId"] = clientId };

        var result = await Controller(config).GoogleLogin(new GoogleTokenDto { IdToken = "x.y.z" });

        ((ObjectResult)result).StatusCode.Should().Be(500);
        Message(result).Should().Be("Google sign-in is not available");
    }
}