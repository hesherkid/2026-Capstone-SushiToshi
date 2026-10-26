using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.domain.Seeders;

namespace back_end.Tests.Seeders;

// The sign-in-only customer: an account to log in with, and nothing else
public class NewCustomerSeedTests
{
    private static ApplicationDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public void UserSeeder_AddsTheNewCustomerAsASignInReadyCustomer()
    {
        using var context = NewContext();

        new UserSeeder(context, new Mock<ILogger<UserSeeder>>().Object).Seed();
        context.SaveChanges();

        var noah = context.Users.Single(u => u.Email == UserSeeder.NewCustomerEmail);
        noah.Role.Should().Be(UserRoles.Customer);
        noah.Status.Should().Be(UserStatus.Active);
        noah.Is_email_confirmed.Should().BeTrue();
        noah.Password_hash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ServiceRequestSeeder_NeverRequestsAsTheNewCustomer()
    {
        // Requests come only from diners seated at the table, and the new customer is never
        // seated by the seed. The seeder is random, so seed a few times.
        var seeded = 0;
        for (var run = 0; run < 10; run++)
        {
            using var context = NewContext();

            // The new customer comes first, where FirstOrDefault used to pick it
            context.Users.AddRange(
                new User { User_id = 1, Email = UserSeeder.NewCustomerEmail, Role = UserRoles.Customer },
                new User { User_id = 2, Email = "emma.wilson@email.com", Role = UserRoles.Customer },
                new User { User_id = 3, Email = "staff@sushitoshi.ca", Role = UserRoles.Staff, Location_id = 1 });
            context.Locations.Add(new Locations { Location_Id = 1, Name = "Downtown" });
            context.Menus.Add(new Menu { Menu_id = 1, Name = "Dinner" });
            context.Tables.Add(new TableEntity { Table_Id = 1, table_number = 1, Location_Id = 1, is_active = true });

            // Several past visits by Emma, so the seeder has plenty of chances to pick the wrong user
            for (var id = 1; id <= 20; id++)
            {
                var start = DateTime.UtcNow.AddDays(-id);
                context.DiningSessions.Add(new DiningSession
                {
                    Session_Id = id,
                    Location_Id = 1,
                    Table_Id = 1,
                    Menu_Id = 1,
                    Started_At = start,
                    Ended_At = start.AddHours(1)
                });
                context.SessionParticipants.Add(new SessionParticipant
                {
                    Session_Id = id,
                    User_Id = 2,
                    User_Name = "Emma Wilson",
                    Joined_At = start.AddMinutes(5),
                    Left_At = start.AddHours(1)
                });
            }
            context.SaveChanges();

            new ServiceRequestSeeder(context, new Mock<ILogger<ServiceRequestSeeder>>().Object).Seed();
            context.SaveChanges();

            context.ServiceRequests.Should().OnlyContain(r => r.Request_By == 2);
            seeded += context.ServiceRequests.Count();
        }

        // Some run must have made requests, or the check above proved nothing.
        seeded.Should().BePositive();
    }
}