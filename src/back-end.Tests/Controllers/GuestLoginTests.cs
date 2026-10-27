using System.IdentityModel.Tokens.Jwt;
using back_end.Services;
using System.Security.Claims;
using System.Text.Json;
using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using back_end.Configurations;
using back_end.controllers;
using back_end.Controllers;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.DTO.DiningSessionDTOs;
using back_end.DTO.UserDTOs;
using back_end.Helpers;
using back_end.Services.Auth;
using back_end.Services.Email;

namespace back_end.Tests.Controllers;

// POST /api/auth/guest: one throwaway account per "Continue as Guest"
public class GuestLoginTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly AuthController _auth;

    public GuestLoginTests()
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

    private async Task<(JsonElement body, JwtSecurityToken token)> SignInAsGuest()
    {
        var result = await _auth.GuestLogin();
        var json = JsonSerializer.SerializeToElement((result as OkObjectResult)!.Value);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(json.GetProperty("access_token").GetString());
        return (json, token);
    }

    [Fact]
    public async Task GuestLogin_CreatesAPasswordlessGuestAccount()
    {
        var (body, _) = await SignInAsGuest();

        var guest = await _context.Users.SingleAsync();
        GuestAccounts.IsGuest(guest.Email).Should().BeTrue();
        guest.Password_hash.Should().BeNull();
        guest.Role.Should().Be(UserRoles.Customer);
        guest.First_name.Should().Be("Guest");
        guest.Last_name.Should().MatchRegex("^[A-HJ-NP-Z2-9]{4}$");
        body.GetProperty("user").GetProperty("last_name").GetString().Should().Be(guest.Last_name);
    }

    [Fact]
    public async Task GuestLogin_GivesEveryGuestTheirOwnAccount()
    {
        var (_, first) = await SignInAsGuest();
        var (_, second) = await SignInAsGuest();

        var ids = new[] { first, second }
            .Select(t => t.Claims.First(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "nameid").Value);
        ids.Should().OnlyHaveUniqueItems();
        (await _context.Users.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task GuestLogin_TokenLastsAWholeVisitAndCarriesAReadableName()
    {
        var (body, token) = await SignInAsGuest();
        var guest = await _context.Users.SingleAsync();

        body.GetProperty("expires_in").GetInt32().Should().Be(GuestAccounts.DefaultTokenMinutes * 60);
        token.ValidTo.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(GuestAccounts.DefaultTokenMinutes), TimeSpan.FromMinutes(1));
        token.Claims.Should().Contain(c => c.Type == "given_name" && c.Value == "Guest");
        token.Claims.Should().Contain(c => c.Type == "family_name" && c.Value == guest.Last_name);
        token.Claims.Should().Contain(c => c.Type == GuestAccounts.TokenClaim && c.Value == "true");
    }

    [Fact]
    public async Task GuestsAtOneTable_AreSeparateParticipants()
    {
        _context.Locations.Add(new Locations { Location_Id = 1, Name = "Downtown" });
        _context.Tables.Add(new TableEntity { Table_Id = 101, table_number = 1, Location_Id = 1, is_active = true });
        _context.DiningSessions.Add(new DiningSession { Session_Id = 7, Menu_Id = 1, Location_Id = 1, Table_Id = 101, Started_At = DateTime.UtcNow });
        await _context.SaveChangesAsync();

        foreach (var _ in Enumerable.Range(0, 2))
        {
            var (_, token) = await SignInAsGuest();
            var sessions = new DiningSessionController(_context, new Mock<IConfiguration>().Object, new Mock<ILogger<DashBoardController>>().Object, new LocationAccessService(_context))
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(token.Claims, "TestAuth", "nameid", "role")),
                    },
                },
            };
            await sessions.JoinTableSession(new JoinTableSessionDTO { Table_Number = 1, Location_Id = 1 });
        }

        var participants = await _context.SessionParticipants.Where(p => p.Session_Id == 7).ToListAsync();
        participants.Should().HaveCount(2);
        participants.Select(p => p.User_Name).Should().AllSatisfy(n => n.Should().StartWith("Guest "));
        participants.Select(p => p.User_Name).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task GuestAccounts_AreLeftOutOfTheCustomerList()
    {
        await SignInAsGuest();
        _context.Users.Add(new User { User_id = 500, Email = "mia@example.com", First_name = "Mia", Last_name = "Chen", Role = UserRoles.Customer });
        await _context.SaveChangesAsync();

        var customers = new CustomerController(_context, new Mock<ILogger<CustomerController>>().Object);
        var result = await customers.ListCustomers();

        var list = (result.Result as OkObjectResult)!.Value as IEnumerable<UserResponseDTO>;
        list!.Select(c => c.Email).Should().Equal("mia@example.com");
    }
}
