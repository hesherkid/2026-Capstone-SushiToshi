using System.Net;
using System.Text;
using FluentAssertions;
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

// Sign-in and account endpoints are capped per IP address, so one client can't block the others
public class AuthRateLimitTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly IPAddress ClientA = IPAddress.Parse("198.51.100.1");
    private static readonly IPAddress ClientB = IPAddress.Parse("198.51.100.2");

    private readonly WebApplicationFactory<Program> _baseFactory;

    public AuthRateLimitTests(WebApplicationFactory<Program> factory)
    {
        _baseFactory = factory;
    }

    public static TheoryData<string, int> Endpoints => new()
    {
        { "/api/auth/login", AuthRateLimits.SignInPerMinute },
        { "/api/auth/google", AuthRateLimits.SignInPerMinute },
        { "/api/auth/verify-email", AuthRateLimits.SignInPerMinute },
        { "/api/auth/reset-password", AuthRateLimits.SignInPerMinute },
        { "/api/auth/register", AuthRateLimits.EmailPerMinute },
        { "/api/auth/forgot-password", AuthRateLimits.EmailPerMinute },
        { "/api/auth/resend-verification", AuthRateLimits.EmailPerMinute },
    };

    // A fresh app per test, so each starts with unused limits
    private WebApplicationFactory<Program> Factory()
    {
        var databaseName = "AuthRateLimit" + Guid.NewGuid();
        return _baseFactory.WithWebHostBuilder(builder =>
        {
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

    // An empty body is enough: the limit is checked before the request is read
    private static async Task<int> Post(WebApplicationFactory<Program> factory, string path, IPAddress from)
    {
        var context = await factory.Server.SendAsync(c =>
        {
            c.Request.Method = HttpMethods.Post;
            c.Request.Path = path;
            c.Request.ContentType = "application/json";
            c.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
            c.Connection.RemoteIpAddress = from;
        });
        return context.Response.StatusCode;
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task OverTheLimit_IsRefusedForThatIpOnly(string path, int limit)
    {
        using var factory = Factory();

        for (var i = 0; i < limit; i++)
        {
            (await Post(factory, path, ClientA)).Should()
                .NotBe(StatusCodes.Status429TooManyRequests, $"call {i + 1} is within the limit");
        }

        (await Post(factory, path, ClientA)).Should().Be(StatusCodes.Status429TooManyRequests);
        (await Post(factory, path, ClientB)).Should().NotBe(StatusCodes.Status429TooManyRequests);
    }

    [Fact]
    public async Task EndpointsInOnePolicy_ShareTheLimit()
    {
        using var factory = Factory();

        for (var i = 0; i < AuthRateLimits.SignInPerMinute; i++)
        {
            await Post(factory, "/api/auth/login", ClientA);
        }

        // Switching to Google sign-in doesn't give a fresh allowance
        (await Post(factory, "/api/auth/google", ClientA)).Should().Be(StatusCodes.Status429TooManyRequests);
        // The email endpoints have their own
        (await Post(factory, "/api/auth/forgot-password", ClientA)).Should().NotBe(StatusCodes.Status429TooManyRequests);
    }
}
