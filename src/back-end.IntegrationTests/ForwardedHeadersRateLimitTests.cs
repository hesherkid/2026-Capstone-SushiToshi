using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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

// Behind a proxy every request comes from the proxy's address. With forwarded headers on, the guest
// limit applies per real client, but only when the request comes from a trusted proxy.
public class ForwardedHeadersRateLimitTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly IPAddress Loopback = IPAddress.Loopback;
    private static readonly IPAddress Untrusted = IPAddress.Parse("203.0.113.9");

    private readonly WebApplicationFactory<Program> _baseFactory;

    public ForwardedHeadersRateLimitTests(WebApplicationFactory<Program> factory)
    {
        _baseFactory = factory;
    }

    private WebApplicationFactory<Program> Factory(bool enabled)
    {
        var databaseName = "ForwardedHeaders" + Guid.NewGuid();
        return _baseFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ForwardedHeaders:Enabled", enabled.ToString());
            builder.ConfigureTestServices(services =>
            {
                // Same in-memory swap as GuestLoginRateLimitTests
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(databaseName));
            });
        });
    }

    private static async Task<int> GuestLogin(WebApplicationFactory<Program> factory, IPAddress from, string? forwardedFor)
    {
        var context = await factory.Server.SendAsync(c =>
        {
            c.Request.Method = HttpMethods.Post;
            c.Request.Path = "/api/auth/guest";
            c.Connection.RemoteIpAddress = from;
            if (forwardedFor != null)
                c.Request.Headers["X-Forwarded-For"] = forwardedFor;
        });
        return context.Response.StatusCode;
    }

    private static async Task UseUpTheLimit(WebApplicationFactory<Program> factory, IPAddress from, string? forwardedFor)
    {
        for (var i = 0; i < GuestAccounts.RateLimitPerMinute; i++)
        {
            (await GuestLogin(factory, from, forwardedFor)).Should().Be(StatusCodes.Status200OK);
        }
    }

    [Fact]
    public async Task Enabled_ClientsBehindATrustedProxyEachGetTheirOwnLimit()
    {
        using var factory = Factory(enabled: true);

        await UseUpTheLimit(factory, Loopback, "198.51.100.1");

        (await GuestLogin(factory, Loopback, "198.51.100.1")).Should().Be(StatusCodes.Status429TooManyRequests);
        (await GuestLogin(factory, Loopback, "198.51.100.2")).Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task Enabled_IgnoresForwardedForFromAnUntrustedAddress()
    {
        using var factory = Factory(enabled: true);

        await UseUpTheLimit(factory, Untrusted, "198.51.100.1");

        // A new X-Forwarded-For value doesn't get a fresh limit
        (await GuestLogin(factory, Untrusted, "198.51.100.2")).Should().Be(StatusCodes.Status429TooManyRequests);
    }

    [Fact]
    public async Task Disabled_IgnoresForwardedFor()
    {
        using var factory = Factory(enabled: false);

        await UseUpTheLimit(factory, Loopback, "198.51.100.1");

        (await GuestLogin(factory, Loopback, "198.51.100.2")).Should().Be(StatusCodes.Status429TooManyRequests);
    }
}
