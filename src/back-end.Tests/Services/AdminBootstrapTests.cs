using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.Services;

namespace back_end.Tests.Services;

// The first admin is created from configuration, once, and never by changing an existing account
public class AdminBootstrapTests
{
    private const string Email = "owner@restaurant.example";
    private const string Password = "a-long-enough-password";

    private static ApplicationDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static IConfiguration Config(string? email, string? password) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [AdminBootstrap.EmailKey] = email,
                [AdminBootstrap.PasswordKey] = password
            })
            .Build();

    private static Task<bool> Run(ApplicationDbContext context, IConfiguration config) =>
        AdminBootstrap.EnsureAdminAsync(context, config, new Mock<ILogger>().Object);

    [Fact]
    public async Task NoAdmin_CreatesAnActiveConfirmedAdminWithTheConfiguredPassword()
    {
        using var context = NewContext();

        var created = await Run(context, Config("  Owner@Restaurant.Example ", Password));

        created.Should().BeTrue();
        var admin = context.Users.Single();
        admin.Email.Should().Be(Email);
        admin.Role.Should().Be(UserRoles.Admin);
        admin.Status.Should().Be(UserStatus.Active);
        admin.Is_email_confirmed.Should().BeTrue();
        BCrypt.Net.BCrypt.Verify(Password, admin.Password_hash).Should().BeTrue();
    }

    [Fact]
    public async Task AdminAlreadyExists_ChangesNothing()
    {
        using var context = NewContext();
        context.Users.Add(new User { Email = Email, Role = UserRoles.Admin, Password_hash = "existing-hash" });
        await context.SaveChangesAsync();

        var created = await Run(context, Config(Email, Password));

        created.Should().BeFalse();
        context.Users.Single().Password_hash.Should().Be("existing-hash");
    }

    [Fact]
    public async Task NothingConfigured_CreatesNoAdmin()
    {
        using var context = NewContext();

        var created = await Run(context, Config(null, null));

        created.Should().BeFalse();
        context.Users.Should().BeEmpty();
    }

    [Theory]
    [InlineData(Email, null)]
    [InlineData(null, Password)]
    [InlineData("not-an-email", Password)]
    [InlineData(Email, "short")]
    public async Task IncompleteOrInvalidSettings_Throw(string? email, string? password)
    {
        using var context = NewContext();

        var act = () => Run(context, Config(email, password));

        await act.Should().ThrowAsync<InvalidOperationException>();
        context.Users.Should().BeEmpty();
    }

    [Fact]
    public async Task EmailBelongsToAnExistingAccount_ThrowsInsteadOfPromotingIt()
    {
        using var context = NewContext();
        context.Users.Add(new User { Email = Email, Role = UserRoles.Customer });
        await context.SaveChangesAsync();

        var act = () => Run(context, Config(Email, Password));

        await act.Should().ThrowAsync<InvalidOperationException>();
        context.Users.Single().Role.Should().Be(UserRoles.Customer);
    }
}
