using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using back_end.Configurations;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.Helpers;
using back_end.Services;

namespace back_end.Tests.Services;

public class GuestCleanupTests
{
    private static readonly DateTime Now = new(2026, 11, 7, 10, 0, 0, DateTimeKind.Utc);

    private static ApplicationDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Task<int> Run(ApplicationDbContext context) =>
        new GuestCleanup(context, Options.Create(new GuestCleanupSettings { MinAgeHours = 24 }),
                new Mock<ILogger<GuestCleanup>>().Object)
            .RunAsync(Now);

    private static User Guest(ApplicationDbContext context, double hoursOld)
    {
        var user = new User
        {
            Email = GuestAccounts.NewEmail(),
            First_name = "Guest",
            Last_name = GuestAccounts.NewCode(),
            Role = UserRoles.Customer,
            Created_at = Now.AddHours(-hoursOld),
        };
        context.Users.Add(user);
        context.SaveChanges();
        return user;
    }

    private static DiningSession Session(ApplicationDbContext context, bool ended)
    {
        var session = new DiningSession { Started_At = Now.AddDays(-2), Ended_At = ended ? Now.AddDays(-2).AddHours(2) : null };
        context.DiningSessions.Add(session);
        context.SaveChanges();
        return session;
    }

    [Fact]
    public async Task OldGuestWithNothing_IsRemoved()
    {
        using var context = NewContext();
        Guest(context, hoursOld: 48);

        (await Run(context)).Should().Be(1);
        context.Users.Should().BeEmpty();
    }

    [Fact]
    public async Task NewGuest_IsKept()
    {
        using var context = NewContext();
        Guest(context, hoursOld: 2);

        (await Run(context)).Should().Be(0);
    }

    [Fact]
    public async Task GuestWhoOrdered_IsKept()
    {
        using var context = NewContext();
        var guest = Guest(context, hoursOld: 48);
        context.SessionOrders.Add(new SessionOrder { session_id = 1, Bill_Id = 1, User_Id = guest.User_id, Created_At = Now.AddDays(-2) });
        context.SaveChanges();

        (await Run(context)).Should().Be(0);
    }

    [Fact]
    public async Task GuestWhoCalledAServer_IsKept()
    {
        using var context = NewContext();
        var guest = Guest(context, hoursOld: 48);
        context.ServiceRequests.Add(new ServiceRequest { Session_Id = 1, Table_Id = 1, Request_By = guest.User_id });
        context.SaveChanges();

        (await Run(context)).Should().Be(0);
    }

    [Fact]
    public async Task GuestAtATableThatIsStillOpen_IsKept()
    {
        using var context = NewContext();
        var guest = Guest(context, hoursOld: 48);
        var session = Session(context, ended: false);
        context.SessionParticipants.Add(new SessionParticipant { Session_Id = session.Session_Id, User_Id = guest.User_id, User_Name = "Guest AAAA" });
        context.SaveChanges();

        (await Run(context)).Should().Be(0);
    }

    [Fact]
    public async Task GuestWhoOnlySatAtAClosedTable_IsRemoved_AndTheSeatIsKeptUnlinked()
    {
        using var context = NewContext();
        var guest = Guest(context, hoursOld: 48);
        var session = Session(context, ended: true);
        context.SessionParticipants.Add(new SessionParticipant { Session_Id = session.Session_Id, User_Id = guest.User_id, User_Name = "Guest AAAA" });
        context.SaveChanges();

        (await Run(context)).Should().Be(1);
        var seat = context.SessionParticipants.Single();
        seat.User_Id.Should().BeNull();
        seat.User_Name.Should().Be("Guest AAAA");
    }

    [Fact]
    public async Task RegisteredCustomerWithNoOrders_IsKept()
    {
        using var context = NewContext();
        context.Users.Add(new User { Email = "real@example.com", First_name = "A", Last_name = "B", Role = UserRoles.Customer, Created_at = Now.AddYears(-1) });
        context.SaveChanges();

        (await Run(context)).Should().Be(0);
    }

    [Theory]
    [InlineData(9, 10, 1)]    // 09:00, runs at 10:00 → 1 hour
    [InlineData(10, 10, 24)]  // exactly 10:00 → tomorrow
    [InlineData(23, 10, 11)]  // 23:00 → 10:00 tomorrow
    public void DelayUntilNextRun_PicksTheNextRunHour(int nowHour, int runAt, int expectedHours)
    {
        var now = new DateTime(2026, 11, 7, nowHour, 0, 0, DateTimeKind.Utc);
        GuestCleanupWorker.DelayUntilNextRun(now, runAt).Should().Be(TimeSpan.FromHours(expectedHours));
    }
}
