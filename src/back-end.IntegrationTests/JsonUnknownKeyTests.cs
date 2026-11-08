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

namespace back_end.IntegrationTests;

// In development, a request body with a key the DTO doesn't have is rejected instead of silently binding to 0 or null,
// so a front-end key that wasn't renamed shows up as an error
public class JsonUnknownKeyTests : IClassFixture<WebApplicationFactory<Program>>
{
    // A key LoginDto doesn't have
    private const string LoginWithUnknownKey = """{ "email": "nobody@example.com", "password": "wrong-password", "remember_me": true }""";

    private readonly WebApplicationFactory<Program> _baseFactory;

    public JsonUnknownKeyTests(WebApplicationFactory<Program> factory)
    {
        _baseFactory = factory;
    }

    // Same in-memory swap as AuthRateLimitTests
    private WebApplicationFactory<Program> Factory(bool? rejectUnknownKeys = null)
    {
        var databaseName = "JsonUnknownKey" + Guid.NewGuid();
        return _baseFactory.WithWebHostBuilder(builder =>
        {
            if (rejectUnknownKeys.HasValue)
                builder.UseSetting("Json:RejectUnknownKeys", rejectUnknownKeys.Value.ToString());
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(databaseName));
            });
        });
    }

    private static async Task<(int Status, string Body)> PostLogin(WebApplicationFactory<Program> factory, string json)
    {
        var context = await factory.Server.SendAsync(c =>
        {
            c.Request.Method = HttpMethods.Post;
            c.Request.Path = "/api/auth/login";
            c.Request.ContentType = "application/json";
            c.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
            // Its own address, so the sign-in rate limit from other tests doesn't apply
            c.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100." + Random.Shared.Next(10, 250));
        });
        return (context.Response.StatusCode, await new StreamReader(context.Response.Body).ReadToEndAsync());
    }

    [Fact]
    public async Task UnknownKey_InDevelopment_Returns400NamingTheKey()
    {
        await using var factory = Factory();

        var (status, body) = await PostLogin(factory, LoginWithUnknownKey);

        status.Should().Be(StatusCodes.Status400BadRequest);
        body.Should().Contain("remember_me");
    }

    [Fact]
    public async Task UnknownKey_WithSettingOff_IsIgnored()
    {
        await using var factory = Factory(rejectUnknownKeys: false);

        var (status, body) = await PostLogin(factory, LoginWithUnknownKey);

        // The login itself fails because the user doesn't exist, but not because of the extra key
        body.Should().NotContain("remember_me");
        status.Should().NotBe(StatusCodes.Status400BadRequest);
    }
}
