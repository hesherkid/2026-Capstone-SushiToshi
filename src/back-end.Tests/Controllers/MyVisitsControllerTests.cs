using System.Security.Claims;
using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using back_end.Controllers;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.DTO.PastVisitDTOs;
using back_end.Helpers;
using back_end.Services;

namespace back_end.Tests.Controllers;

// Prices: adults $40, children $20; 5% GST; 18% gratuity from 6 guests
public class MyVisitsControllerTests : IDisposable
{
    private const int Customer = 50, Friend = 51, Guest = 52;
    private const int South = 1;
    private const int Salmon = 10, Miso = 11, HotStone = 12;

    private readonly ApplicationDbContext _context;
    private readonly MyVisitsController _controller;
    private int _nextOrderId = 1;

    public MyVisitsControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        var pricing = new Mock<IPricingService>();
        pricing.Setup(p => p.GetPricingAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new PricingInfo
               {
                   PricingType = "Weekday",
                   AdultBasePrice = 40m,
                   ChildBasePrice = 20m,
                   TaxRate = 0.05m,
                   GratuityRate = 0.18m,
                   GratuityMinPartySize = 6,
               });

        _controller = new MyVisitsController(_context, pricing.Object, new Mock<ILogger<MyVisitsController>>().Object);

        _context.Locations.Add(new Locations { Location_Id = South, Name = "Sushi Toshi South" });
        _context.Tables.AddRange(
            new TableEntity { Table_Id = 1, table_number = 14, Location_Id = South, is_active = true },
            new TableEntity { Table_Id = 2, table_number = 15, Location_Id = South, is_active = true },
            new TableEntity { Table_Id = 3, table_number = 16, Location_Id = South, is_active = true });
        _context.TableGroups.Add(new TableGroup { TableGroup_Id = 1, Location_Id = South });
        _context.MenuItems.AddRange(
            new Menu_Item { item_id = Salmon, Name = "Salmon Roll", Description = "", Category_id = 1 },
            new Menu_Item { item_id = Miso, Name = "Miso Soup", Description = "", Category_id = 1 },
            new Menu_Item { item_id = HotStone, Name = "Hot Stone", Description = "", Category_id = 1 });
        _context.SaveChanges();

        SignIn(Customer);
    }

    public void Dispose() => _context.Dispose();

    private void SignIn(int userId, bool guest = false)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, "Customer"),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, guest ? GuestAccounts.NewEmail() : $"user{userId}@example.com"),
        };
        if (guest) claims.Add(new Claim(GuestAccounts.TokenClaim, "true"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) }
        };
    }

    // A visit at table 14 that `daysAgo` started, ended unless `ended` is false, with these people seated
    private void AddVisit(int sessionId, double daysAgo, bool ended = true, params int[] seated)
    {
        var started = DateTime.UtcNow.AddDays(-daysAgo);
        _context.DiningSessions.Add(new DiningSession
        {
            Session_Id = sessionId,
            Menu_Id = 1,
            Location_Id = South,
            Table_Id = 1,
            Started_At = started,
            Ended_At = ended ? started.AddHours(1.5) : null,
        });
        foreach (var userId in seated)
        {
            _context.SessionParticipants.Add(new SessionParticipant { Session_Id = sessionId, User_Id = userId, Joined_At = started });
        }
        _context.SaveChanges();
    }

    private Billing AddBill(int billId, int sessionId, int adults, int children = 0, BillStatus status = BillStatus.Closed)
    {
        var bill = new Billing
        {
            Bill_Id = billId,
            Session_Id = sessionId,
            Bill_Name = $"Bill {billId}",
            Adult_Count = adults,
            Child_Count = children,
            Total_Count = adults + children,
            Status = status,
        };
        _context.Bills.Add(bill);
        _context.SaveChanges();
        return bill;
    }

    private void AddOrder(int userId, int sessionId, int billId, OrderStatus status, params (int itemId, int quantity, decimal price, OrderStatus itemStatus)[] lines)
    {
        var order = new SessionOrder
        {
            Order_Id = _nextOrderId++,
            session_id = sessionId,
            Bill_Id = billId,
            User_Id = userId,
            Status = status,
            Created_At = DateTime.UtcNow,
        };
        foreach (var (itemId, quantity, price, itemStatus) in lines)
        {
            order.OrderItems.Add(new OrderItems
            {
                Menu_Id = 1,
                Item_Id = itemId,
                Quantity = quantity,
                Price_At_Time = price,
                Order_Item_Status = itemStatus,
            });
        }
        _context.SessionOrders.Add(order);
        _context.SaveChanges();
    }

    private async Task<List<PastVisitDTO>> Visits()
    {
        var result = await _controller.GetPastVisits();
        return (List<PastVisitDTO>)result.Should().BeOfType<OkObjectResult>().Subject.Value!;
    }

    [Fact]
    public async Task GetPastVisits_ListsEndedVisitsNewestFirst_LeavingOutTheCurrentOneAndOtherPeoples()
    {
        // Arrange
        AddVisit(1, daysAgo: 30, seated: Customer);
        AddVisit(2, daysAgo: 2, seated: Customer);
        AddVisit(3, daysAgo: 0, ended: false, seated: Customer);
        AddVisit(4, daysAgo: 1, seated: Friend);

        // Act
        var visits = await Visits();

        // Assert
        visits.Select(v => v.SessionId).Should().Equal(2, 1);
        visits[0].LocationName.Should().Be("Sushi Toshi South");
        visits[0].TableNumbers.Should().Equal(14);
        visits[0].EndedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetPastVisits_TableGroup_ListsEveryTableNumber()
    {
        // Arrange
        AddVisit(1, daysAgo: 1, seated: Customer);
        var session = _context.DiningSessions.Single();
        session.Table_Id = null;
        session.TableGroup_Id = 1;
        _context.Tables.Where(t => t.Table_Id != 1).ToList().ForEach(t => t.TableGroup_Id = 1);
        _context.SaveChanges();

        // Act
        var visit = (await Visits()).Single();

        // Assert
        visit.TableNumbers.Should().Equal(15, 16);
    }

    [Fact]
    public async Task GetPastVisits_Items_AreOnlyMine_AddedUp_WithoutCancelledOnes()
    {
        // Arrange
        AddVisit(1, daysAgo: 1, seated: [Customer, Friend]);
        AddBill(100, 1, adults: 2);
        AddOrder(Customer, 1, 100, OrderStatus.Delivered,
            (Salmon, 2, 0m, OrderStatus.Delivered), (Miso, 1, 0m, OrderStatus.Cancelled));
        AddOrder(Customer, 1, 100, OrderStatus.Delivered, (Salmon, 3, 0m, OrderStatus.Delivered));
        AddOrder(Customer, 1, 100, OrderStatus.Cancelled, (HotStone, 1, 0m, OrderStatus.Pending));
        AddOrder(Friend, 1, 100, OrderStatus.Delivered, (Miso, 4, 0m, OrderStatus.Delivered));

        // Act
        var items = (await Visits()).Single().Items;

        // Assert
        items.Should().ContainSingle();
        items[0].ItemId.Should().Be(Salmon);
        items[0].Name.Should().Be("Salmon Roll");
        items[0].Quantity.Should().Be(5);
    }

    [Fact]
    public async Task GetPastVisits_BillTotal_MatchesTheBill_WithAddOnsAndGst()
    {
        // Arrange: 2 adults ($80) + 2 extras at $6 ($12) = $92, under the gratuity's party size; 5% GST
        AddVisit(1, daysAgo: 1, seated: Customer);
        AddBill(100, 1, adults: 2);
        AddOrder(Customer, 1, 100, OrderStatus.Delivered,
            (Salmon, 4, 0m, OrderStatus.Delivered), (HotStone, 2, 6m, OrderStatus.Delivered), (Miso, 1, 6m, OrderStatus.Cancelled));

        // Act
        var bill = (await Visits()).Single().Bills.Single();

        // Assert
        bill.BillId.Should().Be(100);
        bill.BillName.Should().Be("Bill 100");
        bill.GuestCount.Should().Be(2);
        bill.Total.Should().Be(96.60m);
    }

    [Fact]
    public async Task GetPastVisits_ClosedBillWithFrozenPrices_UsesThemNotTodaysPrices()
    {
        // Arrange: frozen at $30 a head with no tax, so today's $40 + GST mustn't show
        AddVisit(1, daysAgo: 1, seated: Customer);
        var bill = AddBill(100, 1, adults: 2);
        BillPriceSnapshot.Freeze(bill, new PricingInfo { PricingType = "Weekday", AdultBasePrice = 30m }, 0m);
        _context.SaveChanges();
        AddOrder(Customer, 1, 100, OrderStatus.Delivered, (Salmon, 1, 0m, OrderStatus.Delivered));

        // Act
        var total = (await Visits()).Single().Bills.Single().Total;

        // Assert
        total.Should().Be(60m);
    }

    [Fact]
    public async Task GetPastVisits_Bills_AreTheOnesMyOrdersWentOn()
    {
        // Arrange: two parties at one table; I ordered on bill 101
        AddVisit(1, daysAgo: 1, seated: [Customer, Friend]);
        AddBill(100, 1, adults: 2);
        AddBill(101, 1, adults: 1);
        AddOrder(Customer, 1, 101, OrderStatus.Delivered, (Salmon, 1, 0m, OrderStatus.Delivered));
        AddOrder(Friend, 1, 100, OrderStatus.Delivered, (Salmon, 1, 0m, OrderStatus.Delivered));

        // Act
        var bills = (await Visits()).Single().Bills;

        // Assert
        bills.Select(b => b.BillId).Should().Equal(101);
    }

    [Fact]
    public async Task GetPastVisits_NothingOrdered_ShowsTheTablesBills_ButNotCancelledOnes()
    {
        // Arrange: someone else ordered for the table
        AddVisit(1, daysAgo: 1, seated: [Customer, Friend]);
        AddBill(100, 1, adults: 2);
        AddBill(101, 1, adults: 1, status: BillStatus.Cancelled);
        AddOrder(Friend, 1, 100, OrderStatus.Delivered, (Salmon, 1, 0m, OrderStatus.Delivered));

        // Act
        var visit = (await Visits()).Single();

        // Assert
        visit.Items.Should().BeEmpty();
        visit.Bills.Select(b => b.BillId).Should().Equal(100);
    }

    [Fact]
    public async Task GetPastVisits_KeepsToTheMostRecentVisits()
    {
        // Arrange
        for (var id = 1; id <= MyVisitsController.VisitLimit + 3; id++)
        {
            AddVisit(id, daysAgo: id, seated: Customer);
        }

        // Act
        var visits = await Visits();

        // Assert
        visits.Should().HaveCount(MyVisitsController.VisitLimit);
        visits[0].SessionId.Should().Be(1);
    }

    [Fact]
    public async Task GetPastVisits_Guest_IsTurnedAway()
    {
        // Arrange
        AddVisit(1, daysAgo: 1, seated: Guest);
        SignIn(Guest, guest: true);

        // Act
        var result = await _controller.GetPastVisits();

        // Assert
        (result as IStatusCodeActionResult)?.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }
}
