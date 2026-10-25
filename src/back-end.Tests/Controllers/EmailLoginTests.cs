using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using back_end.Configurations;
using back_end.controllers;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.DTO.Auth;
using back_end.Services.Auth;
using back_end.Services.Email;
using BCryptNet = BCrypt.Net.BCrypt;

namespace back_end.Tests.Controllers;

// POST /api/auth/login: email and password sign-in
public class EmailLoginTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly AuthController _auth;

    public EmailLoginTests()
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
            })
            .Build();

        _auth = new AuthController(
            _context,
            config,
            new Mock<IEmailService>().Object,
            new Mock<IAuthTokenService>().Object,
            Options.Create(new AuthEmailSettings()));
    }

    public void Dispose() => _context.Dispose();

    // The token carries no name, so the home page greets the customer from the response
    [Fact]
    public async Task Login_ReturnsTheCustomersName()
    {
        _context.Users.Add(new User
        {
            Email = "sam@example.com",
            Password_hash = BCryptNet.HashPassword("Password1!"),
            First_name = "Sam",
            Last_name = "Tanaka",
            Role = UserRoles.Customer,
            Status = UserStatus.Active,
            Is_email_confirmed = true,
        });
        await _context.SaveChangesAsync();

        var result = await _auth.Login(new LoginDTO { UserEmail = "sam@example.com", UserPassword = "Password1!" });

        var json = JsonSerializer.SerializeToElement((result as OkObjectResult)!.Value);
        json.GetProperty("user").GetProperty("first_name").GetString().Should().Be("Sam");
        json.GetProperty("user").GetProperty("last_name").GetString().Should().Be("Tanaka");
    }

    // The server reads "sub" back as a NameIdentifier, so it must be the ID like "nameid"
    [Fact]
    public async Task Login_TokenIdentifiesTheUserByIdOnly()
    {
        var user = new User
        {
            Email = "sam@example.com",
            Password_hash = BCryptNet.HashPassword("Password1!"),
            Role = UserRoles.Customer,
            Status = UserStatus.Active,
            Is_email_confirmed = true,
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var result = await _auth.Login(new LoginDTO { UserEmail = "sam@example.com", UserPassword = "Password1!" });

        var json = JsonSerializer.SerializeToElement((result as OkObjectResult)!.Value);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(json.GetProperty("access_token").GetString());
        token.Claims.Should().Contain(c => c.Type == "sub" && c.Value == user.User_id.ToString());
        token.Claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == user.User_id.ToString());
    }
}
