using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.domain.Seeders;
using back_end.Helpers;
using back_end.Services;

namespace back_end.Tests.Seeders;

// Tests for ensuring the seeded data follows the same rules as the API enforces for participants, bills, orders, and items.
public class SeederRulesTests
{
    private const int Runs = 5;
    private const int LocationId = 1;
    private const int UnavailableItemId = 99;

    // Gratuity from 6 guests, so the freeze test can check it follows the table's party size
    private static readonly PricingInfo SeedPricing = new()
    {
        PricingType = "Weekday",
        PricingSource = "Default",
        AdultBasePrice = 30m,
        SeniorBasePrice = 25m,
        ChildBasePrice = 15m,
        ToddlerBasePrice = 0m,
        TaxRate = 0.05m,
        GratuityRate = 0.18m,
        GratuityMinPartySize = 6,
    };

    private static ApplicationDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ApplicationDbContext Seeded()
    {
        var context = NewContext();
        var now = DateTime.UtcNow;

        context.Locations.Add(new Locations { Location_Id = LocationId, Name = "Downtown" });
        context.Menus.Add(new Menu { Menu_id = 1, Name = "Dinner" });

        // Nigiri: 3 combined per order, 4 per adult per session (Block). Hot Stone: 1 per adult, then charged.
        context.Categories.AddRange(
            new Category
            {
                Category_id = 1,
                Category_name = "Nigiri",
                per_order_limit = 3,
                adult_limit = 4,
                child_limit = 2,
                senior_limit = 4,
                over_limit_behavior = OverLimitBehavior.Block
            },
            new Category
            {
                Category_id = 2,
                Category_name = "Hot Stone",
                adult_limit = 1,
                child_limit = 1,
                senior_limit = 1,
                over_limit_behavior = OverLimitBehavior.Charge
            });

        for (var id = 1; id <= 6; id++)
        {
            context.MenuItems.Add(new Menu_Item { item_id = id, Name = $"Nigiri {id}", Category_id = 1 });
            context.MenuItemAssignments.Add(new MenuItemAssignment
            {
                Menu_Id = 1,
                Item_Id = id,
                Status = MenuItemStatus.Available,
                Per_Order_Limit = 1
            });
        }
        for (var id = 7; id <= 8; id++)
        {
            context.MenuItems.Add(new Menu_Item { item_id = id, Name = $"Hot Stone {id}", Category_id = 2 });
            context.MenuItemAssignments.Add(new MenuItemAssignment
            {
                Menu_Id = 1,
                Item_Id = id,
                Status = MenuItemStatus.Available,
                Price = 5m,
                Over_Limit_Behavior = OverLimitBehavior.Charge
            });
        }
        context.MenuItems.Add(new Menu_Item { item_id = UnavailableItemId, Name = "Sold out", Category_id = 1 });
        context.MenuItemAssignments.Add(new MenuItemAssignment
        {
            Menu_Id = 1,
            Item_Id = UnavailableItemId,
            Status = MenuItemStatus.Unavailable
        });

        context.Users.AddRange(
            new User { User_id = 1, Email = "admin@sushitoshi.ca", Role = UserRoles.Admin, Password_hash = "x" },
            new User { User_id = 2, Email = "staff@sushitoshi.ca", Role = UserRoles.Staff, Location_id = LocationId, Password_hash = "x" },
            new User { User_id = 3, Email = "emma@email.com", First_name = "Emma", Last_name = "Wilson", Role = UserRoles.Customer, Password_hash = "x" },
            new User { User_id = 4, Email = "liam@email.com", First_name = "Liam", Last_name = "Brown", Role = UserRoles.Customer, Password_hash = "x" },
            new User { User_id = 5, Email = "olivia@email.com", First_name = "Olivia", Last_name = "Taylor", Role = UserRoles.Customer, Password_hash = "x" });

        context.Tables.AddRange(
            new TableEntity { Table_Id = 1, table_number = 1, seat_count = 4, Location_Id = LocationId, is_active = true },
            new TableEntity { Table_Id = 2, table_number = 2, seat_count = 4, Location_Id = LocationId, is_active = true },
            new TableEntity { Table_Id = 3, table_number = 3, seat_count = 6, Location_Id = LocationId, is_active = true });

        context.DiningSessions.AddRange(
            // Open tables
            new DiningSession { Session_Id = 1, Menu_Id = 1, Location_Id = LocationId, Table_Id = 1, Started_At = now.AddHours(-2) },
            new DiningSession { Session_Id = 2, Menu_Id = 1, Location_Id = LocationId, Table_Id = 3, Started_At = now.AddMinutes(-20) },
            // Past tables, two of them at the same time so customers could overlap
            new DiningSession { Session_Id = 3, Menu_Id = 1, Location_Id = LocationId, Table_Id = 2, Started_At = now.AddHours(-6), Ended_At = now.AddHours(-5) },
            new DiningSession { Session_Id = 4, Menu_Id = 1, Location_Id = LocationId, Table_Id = 1, Started_At = now.AddHours(-6), Ended_At = now.AddHours(-4) },
            new DiningSession { Session_Id = 5, Menu_Id = 1, Location_Id = LocationId, Table_Id = 2, Started_At = now.AddHours(-4), Ended_At = now.AddHours(-3) });
        context.SaveChanges();

        var pool = context.Users.ToList().ToDictionary(u => u.User_id, u => ($"{u.First_name} {u.Last_name}".Trim(), u.First_name, u.Last_name));

        var participants = new SessionParticipantSeeder(context, new Mock<ILogger<SessionParticipantSeeder>>().Object);
        participants.SetUserSeedData(pool);
        participants.Seed();
        context.SaveChanges();

        var pricing = new Mock<IPricingService>();
        pricing.Setup(p => p.GetPricingAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(SeedPricing);

        var bills = new BillSeeder(context, new Mock<ILogger<BillSeeder>>().Object, pricing.Object);
        bills.SetUserSeedData(pool);
        bills.Seed();
        context.SaveChanges();
        bills.FreezeClosedBillsAsync().GetAwaiter().GetResult();
        context.SaveChanges();

        var orders = new SessionOrderSeeder(context, new Mock<ILogger<SessionOrderSeeder>>().Object);
        orders.SetUserSeedData(pool);
        orders.Seed();
        context.SaveChanges();

        new OrderItemSeeder(context, new Mock<ILogger<OrderItemSeeder>>().Object).Seed();
        context.SaveChanges();

        var requests = new ServiceRequestSeeder(context, new Mock<ILogger<ServiceRequestSeeder>>().Object);
        requests.SetUserSeedData(pool);
        requests.Seed();
        context.SaveChanges();

        return context;
    }

    [Fact]
    public void OpenTables_AreSeatedOnlyByGuestAccountsNobodyCanSignInAs()
    {
        for (var run = 0; run < Runs; run++)
        {
            using var context = Seeded();

            var openSeats = context.SessionParticipants
                .Include(p => p.User)
                .Where(p => p.Left_At == null && p.DiningSession.Ended_At == null)
                .ToList();

            openSeats.Should().NotBeEmpty();
            openSeats.Should().OnlyContain(p => GuestAccounts.IsGuest(p.User!.Email) && p.User.Password_hash == null,
                "a registered customer seated by the seed would look seated when they open the menu");
        }
    }

    [Fact]
    public void Participants_AreDinersWithOneSeatAtATime()
    {
        for (var run = 0; run < Runs; run++)
        {
            using var context = Seeded();
            var seats = context.SessionParticipants.Include(p => p.User).Include(p => p.DiningSession).ToList();

            seats.Should().OnlyContain(p => p.User!.Role == UserRoles.Customer, "staff seat tables but don't join them");
            context.DiningSessions.ToList().Should().OnlyContain(s => seats.Any(p => p.Session_Id == s.Session_Id));

            // Never two open seats, and a customer's past visits never overlap
            seats.Where(p => p.Left_At == null).GroupBy(p => p.User_Id).Should().OnlyContain(g => g.Count() == 1);
            foreach (var visits in seats.GroupBy(p => p.User_Id))
            {
                var ordered = visits.OrderBy(p => p.DiningSession.Started_At).ToList();
                for (var i = 1; i < ordered.Count; i++)
                {
                    ordered[i].DiningSession.Started_At.Should()
                        .BeOnOrAfter(ordered[i - 1].DiningSession.Ended_At ?? DateTime.MaxValue);
                }
            }
        }
    }

    [Fact]
    public void Orders_ArePlacedBySeatedDinersWhileTheBillIsOpen()
    {
        for (var run = 0; run < Runs; run++)
        {
            using var context = Seeded();
            var now = DateTime.UtcNow;
            var seats = context.SessionParticipants.ToList();
            var orders = context.SessionOrders.Include(o => o.Bill).ToList();

            orders.Should().NotBeEmpty();
            foreach (var order in orders)
            {
                var seat = seats.SingleOrDefault(p => p.Session_Id == order.session_id && p.User_Id == order.User_Id);
                seat.Should().NotBeNull("only someone seated at the table can order onto it");
                order.Created_At.Should().BeOnOrAfter(seat!.Joined_At);

                order.Created_At.Should().BeOnOrAfter(order.Bill.Created_At);
                order.Created_At.Should().BeOnOrBefore(order.Bill.Closed_At ?? now);
                (order.Approved_At ?? order.Created_At).Should().BeOnOrBefore(now);
                (order.Completed_At ?? order.Created_At).Should().BeOnOrBefore(now);

                if (order.Bill.Status != BillStatus.Open)
                    order.Status.Should().BeOneOf(OrderStatus.Delivered, OrderStatus.Cancelled);
            }
        }
    }

    [Fact]
    public void Items_StayWithinTheOrderLimits()
    {
        for (var run = 0; run < Runs; run++)
        {
            using var context = Seeded();
            var lines = context.OrderItems.Include(oi => oi.SessionOrder).Include(oi => oi.MenuItem).ToList();
            var orders = context.SessionOrders.ToList();

            lines.Should().NotBeEmpty();
            lines.Should().NotContain(oi => oi.Item_Id == UnavailableItemId);

            // Per order: Nigiri 3 combined, 1 of each
            foreach (var order in lines.GroupBy(oi => oi.Order_Key))
            {
                order.Where(oi => oi.MenuItem.Category_id == 1).Sum(oi => oi.Quantity).Should().BeLessThanOrEqualTo(3);
                order.Where(oi => oi.MenuItem.Category_id == 1).GroupBy(oi => oi.Item_Id)
                     .Should().OnlyContain(g => g.Sum(oi => oi.Quantity) <= 1);
            }

            // Per session: Nigiri blocks at 4 per adult (2 per child) of the non-cancelled bills
            foreach (var session in context.DiningSessions.ToList())
            {
                var bills = context.Bills.Where(b => b.Session_Id == session.Session_Id && b.Status != BillStatus.Cancelled).ToList();
                var adults = bills.Sum(b => b.Adult_Count) + bills.Sum(b => b.Senior_Count);
                var children = bills.Sum(b => b.Child_Count) + bills.Sum(b => b.Tot_Count);
                var cap = adults + children == 0 ? 4 : 4 * adults + 2 * children;

                lines.Where(oi => oi.SessionOrder.session_id == session.Session_Id &&
                                  oi.SessionOrder.Status != OrderStatus.Cancelled &&
                                  oi.MenuItem.Category_id == 1)
                     .Sum(oi => oi.Quantity)
                     .Should().BeLessThanOrEqualTo(cap);
            }

            // Charge limits: the extra is billed at Price, never free
            lines.Where(oi => oi.Charge_Reason == ChargeReason.OverLimit).Should().OnlyContain(oi => oi.Price_At_Time == 5m);

            // Staff can't approve an empty order
            orders.Where(o => o.Status is OrderStatus.Processing or OrderStatus.Delivered)
                  .Should().OnlyContain(o => lines.Any(oi => oi.Order_Key == o.Order_Id));
        }
    }

    [Fact]
    public void ServiceRequests_AreAskedBySeatedDinersAndHandledByStaff()
    {
        for (var run = 0; run < Runs; run++)
        {
            using var context = Seeded();
            var now = DateTime.UtcNow;
            var seats = context.SessionParticipants.ToList();
            var staffIds = context.Users.Where(u => u.Role == UserRoles.Staff || u.Role == UserRoles.Admin)
                                        .Select(u => u.User_id).ToList();
            var requests = context.ServiceRequests.Include(r => r.DiningSession).ToList();

            foreach (var request in requests)
            {
                seats.Should().Contain(p => p.Session_Id == request.Session_Id &&
                                            p.User_Id == request.Request_By &&
                                            p.Joined_At <= request.Created_At &&
                                            request.Created_At <= (p.Left_At ?? now),
                    "only a diner seated at the table can ask for service");

                request.Table_Id.Should().Be(request.DiningSession.Table_Id!.Value);
                request.Created_At.Should().BeOnOrBefore(now);
                request.Status.Should().BeOneOf(ServiceRequestStatus.Pending, ServiceRequestStatus.Completed);
                IsFormNotes(request.Notes).Should().BeTrue($"\"{request.Notes}\" should look like the customer form wrote it");

                if (request.Status == ServiceRequestStatus.Pending)
                {
                    request.DiningSession.Ended_At.Should().BeNull("an ended table has nothing still waiting");
                    request.Claimed_By.Should().BeNull();
                    request.Claimed_At.Should().BeNull();
                    request.Completed_At.Should().BeNull();
                }
                else
                {
                    staffIds.Should().Contain(request.Claimed_By!.Value);
                    request.Completed_At.Should().NotBeNull();
                    request.Claimed_At.Should().Be(request.Completed_At);
                    request.Completed_At!.Value.Should().BeOnOrAfter(request.Created_At);
                    request.Completed_At.Value.Should().BeOnOrBefore(request.DiningSession.Ended_At ?? now);
                }
            }
        }
    }

    private static bool IsFormNotes(string notes)
    {
        var tiles = ServiceRequestSeeder.CommonRequests.Select(t => t.ToLowerInvariant()).ToHashSet();
        if (!notes.StartsWith("Need "))
            return ServiceRequestSeeder.TypedNotes.Contains(notes);

        var split = notes.IndexOf(". ", StringComparison.Ordinal);
        var picked = (split < 0 ? notes[5..] : notes[5..split]).Split(", ");
        var typed = split < 0 ? null : notes[(split + 2)..];

        return picked.Length > 0 &&
               picked.All(tiles.Contains) &&
               picked.Distinct().Count() == picked.Length &&
               (typed == null || ServiceRequestSeeder.TypedNotes.Contains(typed));
    }

    [Theory]
    [InlineData(new[] { "Water refill", "Napkins" }, null, "Need water refill, napkins")]
    [InlineData(new[] { "Wasabi" }, "  Could we get a high chair?  ", "Need wasabi. Could we get a high chair?")]
    [InlineData(new string[0], "Could we get a high chair?", "Could we get a high chair?")]
    public void BuildNotes_MatchesTheCustomerForm(string[] picked, string? note, string expected)
    {
        ServiceRequestSeeder.BuildNotes(picked, note).Should().Be(expected);
    }

    [Fact]
    public void ClosedBills_HaveFrozenPrices_OpenAndCancelledBillsDoNot()
    {
        for (var run = 0; run < Runs; run++)
        {
            using var context = Seeded();
            var bills = context.Bills.ToList();

            bills.Where(b => b.Status == BillStatus.Closed)
                 .Should().OnlyContain(b => b.Pricing_Type == PricingDayType.Weekday && b.Adult_Price == SeedPricing.AdultBasePrice);
            bills.Where(b => b.Status != BillStatus.Closed)
                 .Should().OnlyContain(b => b.Pricing_Type == null);
        }
    }

    [Fact]
    public void ClosedBills_FreezeTheGratuityTheTableEarned()
    {
        for (var run = 0; run < Runs; run++)
        {
            using var context = Seeded();
            var bills = context.Bills.ToList();

            foreach (var bill in bills.Where(b => b.Status == BillStatus.Closed))
            {
                var partySize = bills
                    .Where(b => b.Session_Id == bill.Session_Id && b.Status != BillStatus.Cancelled)
                    .Sum(b => b.Adult_Count + b.Senior_Count + b.Child_Count + b.Tot_Count);

                bill.Gratuity_Rate.Should().Be(partySize >= SeedPricing.GratuityMinPartySize ? SeedPricing.GratuityRate : 0m);
            }
        }
    }
}