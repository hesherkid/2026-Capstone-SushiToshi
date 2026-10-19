using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using back_end.domain.DbContexts;
using back_end.Helpers;

namespace back_end.IntegrationTests;

// POST /api/auth/guest creates an account per call, so it's capped per IP address
public class GuestLoginRateLimitTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _databaseName = "GuestRateLimit" + Guid.NewGuid();

    public GuestLoginRateLimitTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                // Swap MySQL for an in-memory database. EF Core 9 also registers an options
                // configuration per context, which has to go too or both providers get loaded.
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();

                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName));
            });
        });
    }

    [Fact]
    public async Task GuestLogin_RefusesCallsOverTheLimitWithoutCreatingAccounts()
    {
        var client = _factory.CreateClient();

        for (var i = 0; i < GuestAccounts.RateLimitPerMinute; i++)
        {
            var allowed = await client.PostAsync("/api/auth/guest", null);
            allowed.StatusCode.Should().Be(HttpStatusCode.OK, $"call {i + 1} is within the limit");
        }

        var refused = await client.PostAsync("/api/auth/guest", null);
        refused.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var guestAccounts = await context.Users
            .CountAsync(u => u.Email.EndsWith("@" + GuestAccounts.EmailDomain));
        guestAccounts.Should().Be(GuestAccounts.RateLimitPerMinute);
    }
}
