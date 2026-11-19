using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using back_end.Controllers;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.Dtos;
using back_end.Helpers;
using back_end.Services;
using System.Security.Claims;

namespace back_end.Tests.Controllers;

// Tests for BillController.
// Already covered elsewhere, so not repeated here:
//   BillAccessTests        - customers only see their own table's bills (Get_Bills)
//   BillRequestTests       - Request_Bill ("Ready for Bill")
//   BillGratuityTests      - gratuity and party size on the summary
//   BillPriceSnapshotTests - prices frozen on close, closing a cancelled bill
//   LeftoverFeeTests       - applying the leftover fee

public class BillControllerTests : IDisposable
{
    private const int StaffHere = 30;        // works at location 1
    private const int StaffElsewhere = 31;   // works at location 2
    private const int SeatedGuest = 40;      // at session 100
    private const int GuestWhoLeft = 41;     // was at session 100

    private const int TableSession = 100;
    private const int GroupSession = 200;
    private const int EndedSession = 300;
    private const int NoTableSession = 400;
    private const int OtherLocationSession = 500;

    private const int LeeBill = 901;
    private const int OtherLocationBill = 905;

    private readonly ApplicationDbContext _context;
    private readonly Mock<IPricingService> _pricing = new();

    private static readonly PricingInfo Weekday = new()
    {
        PricingType = "Weekday",
        PricingSource = "Default",
        AdultBasePrice = 30m,
        SeniorBasePrice = 25m,
        ChildBasePrice = 15m,
        ToddlerBasePrice = 0m,
        TaxRate = 0.05m,
        GratuityRate = 0.18m,
        GratuityMinPartySize = 0,
        LeftoverFee = 10m,
    };

    public BillControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _pricing.Setup(p => p.GetPricingAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(Weekday);
        _pricing.Setup(p => p.GetPricingAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Weekday);
        _pricing.Setup(p => p.GetCurrentPricingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Weekday);

        _context.TableGroups.Add(new TableGroup { TableGroup_Id = 1, Group_Name = "Patio", Location_Id = 1 });
        _context.Tables.AddRange(
            new TableEntity { Table_Id = 6, table_number = 6, seat_count = 4, Location_Id = 1, is_active = true },
            new TableEntity { Table_Id = 10, table_number = 10, seat_count = 4, Location_Id = 1, is_active = true, TableGroup_Id = 1 },
            new TableEntity { Table_Id = 11, table_number = 11, seat_count = 4, Location_Id = 1, is_active = true, TableGroup_Id = 1 },
            new TableEntity { Table_Id = 20, table_number = 20, seat_count = 4, Location_Id = 2, is_active = true });

        _context.DiningSessions.AddRange(
            new DiningSession { Session_Id = TableSession, Menu_Id = 1, Location_Id = 1, Table_Id = 6, Started_At = DateTime.UtcNow.AddHours(-1) },
            new DiningSession { Session_Id = GroupSession, Menu_Id = 1, Location_Id = 1, TableGroup_Id = 1, Started_At = DateTime.UtcNow.AddHours(-1) },
            new DiningSession { Session_Id = EndedSession, Menu_Id = 1, Location_Id = 1, Table_Id = 6, Started_At = DateTime.UtcNow.AddHours(-3), Ended_At = DateTime.UtcNow.AddHours(-2) },
            new DiningSession { Session_Id = NoTableSession, Menu_Id = 1, Location_Id = 1, Started_At = DateTime.UtcNow },
            new DiningSession { Session_Id = OtherLocationSession, Menu_Id = 1, Location_Id = 2, Table_Id = 20, Started_At = DateTime.UtcNow });

        _context.Users.AddRange(
            new User { User_id = StaffHere, Email = "staff1@example.com", Role = UserRoles.Staff, Location_id = 1 },
            new User { User_id = StaffElsewhere, Email = "staff2@example.com", Role = UserRoles.Staff, Location_id = 2 });

        _context.SessionParticipants.AddRange(
            new SessionParticipant { Session_Id = TableSession, User_Id = SeatedGuest, User_Name = "Guest K7P2", Bill_Id = LeeBill },
            new SessionParticipant { Session_Id = TableSession, User_Id = GuestWhoLeft, User_Name = "Guest 4MXA", Left_At = DateTime.UtcNow });

        _context.Bills.AddRange(
            new Billing { Bill_Id = LeeBill, Session_Id = TableSession, Bill_Name = "Lee", Adult_Count = 2, Total_Count = 2, Created_At = DateTime.UtcNow.AddMinutes(-30) },
            new Billing { Bill_Id = OtherLocationBill, Session_Id = OtherLocationSession, Bill_Name = "Park", Adult_Count = 2, Total_Count = 2 });

        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    private BillController Bills(int userId = StaffHere, string role = "Staff") =>
        BillsAs(new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role),
        }, "TestAuth")));

    private BillController BillsAs(ClaimsPrincipal user) =>
        new(_context, new Mock<IConfiguration>().Object, new Mock<ILogger<BillController>>().Object, _pricing.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } },
        };

    private static T OkValue<T>(IActionResult result) =>
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<T>().Which;

    private static void ShouldBeOtherLocation(IActionResult result) =>
        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);

    private static CreateBill Guests(int adults = 0, int seniors = 0, int children = 0, int tots = 0, string? name = "Kim") =>
        new() { BillName = name, AdultCount = adults, SeniorCount = seniors, ChildCount = children, TotCount = tots };

    private Billing Bill(int billId) => _context.Bills.Single(b => b.Bill_Id == billId);

    private void AddOrder(int orderId, int billId, int sessionId, OrderStatus status)
    {
        _context.SessionOrders.Add(new SessionOrder
        {
            Order_Id = orderId,
            session_id = sessionId,
            Bill_Id = billId,
            User_Name = "Lee",
            Status = status,
            Created_At = DateTime.UtcNow,
        });
        _context.SaveChanges();
    }

    [Fact]
    public async Task CreateBill_ValidRequest_CreatesAnOpenBill()
    {
        var bill = OkValue<BillResponse>(await Bills().CreateBill(TableSession, Guests(adults: 1, children: 1)));

        bill.SessionId.Should().Be(TableSession);
        bill.BillName.Should().Be("Kim");
        bill.TotalCount.Should().Be(2);
        bill.Status.Should().Be("Open");
        bill.TableNumbers.Should().Equal(6);
        _context.Bills.Should().Contain(b => b.Bill_Id == bill.BillId && b.Status == BillStatus.Open);
    }

    [Fact]
    public async Task CreateBill_TableGroup_UsesEverySeat()
    {
        // 6 guests fit into two 4-seat tables but not one
        var bill = OkValue<BillResponse>(await Bills().CreateBill(GroupSession, Guests(adults: 6)));

        bill.TableNumbers.Should().BeEquivalentTo(new[] { 10, 11 });
    }

    [Fact]
    public async Task CreateBill_UnknownSession_Returns404()
    {
        var result = await Bills().CreateBill(999, Guests(adults: 1));

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task CreateBill_StaffAtAnotherLocation_Returns403()
    {
        var result = await Bills(StaffElsewhere).CreateBill(TableSession, Guests(adults: 1));

        ShouldBeOtherLocation(result);
    }

    [Fact]
    public async Task CreateBill_EndedSession_Returns400()
    {
        var result = await Bills().CreateBill(EndedSession, Guests(adults: 1));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateBill_SessionWithNoTable_Returns400()
    {
        var result = await Bills().CreateBill(NoTableSession, Guests(adults: 1));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateBill_NoGuests_Returns400()
    {
        var result = await Bills().CreateBill(TableSession, Guests());

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateBill_MissingName_Returns400(string? name)
    {
        var result = await Bills().CreateBill(TableSession, Guests(adults: 1, name: name));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateBill_StaffCanGoOverTableCapacity()
    {
        // Table 6 seats 4 and bill already has 2: the party pulled up extra chairs
        var result = await Bills().CreateBill(TableSession, Guests(adults: 3));

        result.Should().BeOfType<OkObjectResult>();
        _context.Bills.Count(b => b.Session_Id == TableSession).Should().Be(2);
    }

    [Fact]
    public async Task GetBills_UnknownSession_Returns404()
    {
        var result = await Bills().GetBills(999);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetBills_TableInTheGroup_IsAccepted()
    {
        var result = await Bills().GetBills(GroupSession, tableId: 11);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetBills_TableNotInTheSession_Returns400()
    {
        var result = await Bills().GetBills(TableSession, tableId: 11);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetBill_ReturnsTheBill()
    {
        var bill = OkValue<BillResponse>(await Bills().GetBill(TableSession, LeeBill));

        bill.BillId.Should().Be(LeeBill);
        bill.BillName.Should().Be("Lee");
        bill.TableNumbers.Should().Equal(6);
    }

    [Fact]
    public async Task GetBill_SeatedGuest_CanSeeIt()
    {
        var result = await Bills(SeatedGuest, "Customer").GetBill(TableSession, LeeBill);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetBill_GuestWhoLeftTheTable_IsForbidden()
    {
        var result = await Bills(GuestWhoLeft, "Customer").GetBill(TableSession, LeeBill);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetBill_BillFromAnotherSession_Returns404()
    {
        // Bill exists, but not in session 200
        var result = await Bills().GetBill(GroupSession, LeeBill);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task CloseBill_NoPendingOrders_ClosesIt()
    {
        AddOrder(801, LeeBill, TableSession, OrderStatus.Delivered);

        var bill = OkValue<BillResponse>(await Bills().CloseBill(TableSession, LeeBill));

        bill.Status.Should().Be("Closed");
        bill.ClosedAt.Should().NotBeNull();
        Bill(LeeBill).Status.Should().Be(BillStatus.Closed);
    }

    [Fact]
    public async Task CloseBill_WithPendingOrders_Returns400_AndStaysOpen()
    {
        AddOrder(801, LeeBill, TableSession, OrderStatus.Pending);

        var result = await Bills().CloseBill(TableSession, LeeBill);

        result.Should().BeOfType<BadRequestObjectResult>();
        Bill(LeeBill).Status.Should().Be(BillStatus.Open);
        Bill(LeeBill).Closed_At.Should().BeNull();
    }

    // ---- Free ice cream: guests say yes on their bill; staff send it for the bills that are ready ----

    private const int ParkBill = 902; // a second party at the same table

    private void AddParkBill(int guests = 3)
    {
        _context.Bills.Add(new Billing { Bill_Id = ParkBill, Session_Id = TableSession, Bill_Name = "Park", Adult_Count = guests, Total_Count = guests });
        _context.SaveChanges();
    }

    private async Task GuestsWantIceCream() =>
        await Bills(SeatedGuest, "Customer").RequestBill(TableSession, LeeBill, iceCream: true);

    private static SendIceCreamDto Scoops(int[] billIds, params (string Flavour, int Quantity)[] flavours) => new()
    {
        BillIds = billIds.ToList(),
        Flavours = flavours.Select(f => new IceCreamFlavourDto { Flavour = f.Flavour, Quantity = f.Quantity }).ToList(),
    };

    private static SendIceCreamDto ForLee(params (string Flavour, int Quantity)[] flavours) => Scoops(new[] { LeeBill }, flavours);

    private Task<IActionResult> Send(SendIceCreamDto request, int sessionId = TableSession) =>
        Bills().SendIceCream(sessionId, request);

    private List<SessionOrder> IceCreamOrders() =>
        _context.SessionOrders.Include(o => o.OrderItems).ThenInclude(oi => oi.MenuItem).ThenInclude(mi => mi.Category)
            .Where(o => o.session_id == TableSession).OrderBy(o => o.Order_Id).ToList();

    [Fact]
    public async Task SendIceCream_AddsAFreePendingOrderWithALinePerFlavour()
    {
        await GuestsWantIceCream();

        // Vanilla listed first, but the kitchen gets them in the usual flavour order
        (await Send(ForLee(("Vanilla", 1), ("mango", 1), ("Chocolate", 0)))).Should().BeOfType<OkObjectResult>();

        var order = IceCreamOrders().Should().ContainSingle().Subject;
        order.Status.Should().Be(OrderStatus.Pending);
        order.Bill_Id.Should().Be(LeeBill);
        order.User_Id.Should().Be(StaffHere);
        order.OrderItems.Select(oi => (oi.MenuItem.Name, oi.Quantity)).Should().Equal(
            ("Mango Ice Cream", 1), ("Vanilla Ice Cream", 1));
        order.OrderItems.Should().OnlyContain(oi =>
            oi.Price_At_Time == 0m // the bill only charges lines above $0
            && oi.Order_Item_Status == OrderStatus.Pending
            && oi.MenuItem.Category.Category_name == FreeIceCream.CategoryName);
        Bill(LeeBill).Ice_Cream_Sent_At.Should().NotBeNull();
    }

    // Lee says yes first; Park asks for their bill later and still gets a turn
    [Fact]
    public async Task SendIceCream_ABillThatSaysYesLater_GetsItsOwnTurn()
    {
        AddParkBill();
        await GuestsWantIceCream();
        await Send(ForLee(("Mango", 2)));

        Bill(ParkBill).Wants_Ice_Cream = true;
        _context.SaveChanges();
        (await Send(Scoops(new[] { ParkBill }, ("Vanilla", 3)))).Should().BeOfType<OkObjectResult>();

        IceCreamOrders().Select(o => o.Bill_Id).Should().Equal(LeeBill, ParkBill);
        Bill(ParkBill).Ice_Cream_Sent_At.Should().NotBeNull();
    }

    // Staff can serve a party that hasn't asked yet at the same visit; then it's done for them too
    [Fact]
    public async Task SendIceCream_ForSeveralBills_SharesTheCapAndMarksThemAll()
    {
        AddParkBill(guests: 3);
        await GuestsWantIceCream(); // Lee (2 guests) said yes; Park hasn't asked

        // Lee 2 + Park 3 = 5
        (await Send(Scoops(new[] { LeeBill, ParkBill }, ("Mango", 6)))).Should().BeOfType<BadRequestObjectResult>();
        (await Send(Scoops(new[] { LeeBill, ParkBill }, ("Mango", 3), ("Strawberry", 2)))).Should().BeOfType<OkObjectResult>();

        var order = IceCreamOrders().Should().ContainSingle().Subject;
        order.Bill_Id.Should().Be(LeeBill); // the bill that said yes
        order.OrderItems.Sum(oi => oi.Quantity).Should().Be(5);
        Bill(LeeBill).Ice_Cream_Sent_At.Should().NotBeNull();
        Bill(ParkBill).Ice_Cream_Sent_At.Should().NotBeNull();
    }

    [Fact]
    public async Task SendIceCream_GoesOnTheBillThatAskedForIt()
    {
        AddParkBill();
        Bill(ParkBill).Wants_Ice_Cream = true;
        Bill(ParkBill).Requested_At = DateTime.UtcNow;
        _context.SaveChanges();

        await Send(Scoops(new[] { LeeBill, ParkBill }, ("Vanilla", 2)));

        IceCreamOrders().Single().Bill_Id.Should().Be(ParkBill);
    }

    [Fact]
    public async Task SendIceCream_SendingAgainForTheSameBills_DoesNothing()
    {
        await GuestsWantIceCream();

        await Send(ForLee(("Mango", 2)));
        var again = await Send(ForLee(("Vanilla", 1)));

        again.Should().BeOfType<OkObjectResult>();
        IceCreamOrders().Should().ContainSingle()
            .Which.OrderItems.Should().ContainSingle().Which.MenuItem.Name.Should().Be("Mango Ice Cream");
    }

    [Fact]
    public async Task SendIceCream_IncludingABillThatAlreadyHadIt_Returns400()
    {
        AddParkBill();
        await GuestsWantIceCream();
        await Send(ForLee(("Mango", 2)));

        (await Send(Scoops(new[] { LeeBill, ParkBill }, ("Vanilla", 1)))).Should().BeOfType<BadRequestObjectResult>();

        IceCreamOrders().Should().ContainSingle();
        Bill(ParkBill).Ice_Cream_Sent_At.Should().BeNull();
    }

    [Fact]
    public async Task SendIceCream_MakesEachFlavourItsOwnItem_OnNoMenu()
    {
        await GuestsWantIceCream();
        await Send(ForLee(("Strawberry", 1)));

        var items = _context.MenuItems.Where(i => i.Category.Category_name == FreeIceCream.CategoryName).ToList();
        items.Select(i => i.Name).Should().BeEquivalentTo(
            "Strawberry Ice Cream", "Chocolate Ice Cream", "Mango Ice Cream", "Vanilla Ice Cream");
        // Guests can't order them themselves
        _context.MenuItemAssignments.Any(a => items.Select(i => i.item_id).Contains(a.Item_Id)).Should().BeFalse();
    }

    [Fact]
    public async Task SendIceCream_AfterTheBillIsClosed_StillGoesToPending()
    {
        await GuestsWantIceCream();
        OkValue<BillResponse>(await Bills().CloseBill(TableSession, LeeBill));

        (await Send(ForLee(("Chocolate", 2)))).Should().BeOfType<OkObjectResult>();

        IceCreamOrders().Single().Status.Should().Be(OrderStatus.Pending);
        Bill(LeeBill).Status.Should().Be(BillStatus.Closed);
    }

    [Theory]
    [InlineData("Pistachio", 1)] // not a flavour we have
    [InlineData("Mango", -1)]
    [InlineData("Mango", 0)]     // nothing chosen
    [InlineData("Mango", 3)]     // the Lee party is 2: one each at most
    public async Task SendIceCream_RefusesBadChoices(string flavour, int quantity)
    {
        await GuestsWantIceCream();

        (await Send(ForLee((flavour, quantity)))).Should().BeOfType<BadRequestObjectResult>();

        _context.SessionOrders.Any().Should().BeFalse();
        Bill(LeeBill).Ice_Cream_Sent_At.Should().BeNull();
    }

    [Fact]
    public async Task SendIceCream_RefusesNoBills_BillsFromAnotherTable_AndCancelledBills()
    {
        (await Send(Scoops(Array.Empty<int>(), ("Mango", 1)))).Should().BeOfType<BadRequestObjectResult>();
        (await Send(Scoops(new[] { OtherLocationBill }, ("Mango", 1)))).Should().BeOfType<BadRequestObjectResult>();

        Bill(LeeBill).Status = BillStatus.Cancelled;
        _context.SaveChanges();
        (await Send(ForLee(("Mango", 1)))).Should().BeOfType<BadRequestObjectResult>();

        _context.SessionOrders.Any().Should().BeFalse();
    }

    [Fact]
    public async Task SendIceCream_AfterTheTableLeft_Returns400()
    {
        _context.Bills.Add(new Billing { Bill_Id = 903, Session_Id = EndedSession, Bill_Name = "Late", Total_Count = 2, Status = BillStatus.Closed });
        _context.SaveChanges();

        (await Send(Scoops(new[] { 903 }, ("Mango", 1)), EndedSession)).Should().BeOfType<BadRequestObjectResult>();
        _context.SessionOrders.Any().Should().BeFalse();
    }

    [Fact]
    public async Task SendIceCream_StaffAtAnotherLocation_Returns403()
    {
        ShouldBeOtherLocation(await Send(Scoops(new[] { OtherLocationBill }, ("Mango", 1)), OtherLocationSession));
        _context.SessionOrders.Any().Should().BeFalse();
    }

    [Fact]
    public async Task CloseBill_AlreadyClosed_Returns400()
    {
        Bill(LeeBill).Status = BillStatus.Closed;
        _context.SaveChanges();

        var result = await Bills().CloseBill(TableSession, LeeBill);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CloseBill_UnknownBill_Returns404()
    {
        var result = await Bills().CloseBill(TableSession, 999);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task CloseBill_StaffAtAnotherLocation_Returns403()
    {
        var result = await Bills().CloseBill(OtherLocationSession, OtherLocationBill);

        ShouldBeOtherLocation(result);
        Bill(OtherLocationBill).Status.Should().Be(BillStatus.Open);
    }

    [Fact]
    public async Task CancelBill_CancelsTheBill_AndOnlyItsPendingOrders()
    {
        _context.Bills.Add(new Billing { Bill_Id = 902, Session_Id = TableSession, Bill_Name = "Choi", Adult_Count = 1, Total_Count = 1 });
        _context.SaveChanges();
        AddOrder(811, LeeBill, TableSession, OrderStatus.Pending);
        AddOrder(813, 902, TableSession, OrderStatus.Pending);

        var bill = OkValue<BillResponse>(await Bills().CancelBill(TableSession, LeeBill));

        bill.Status.Should().Be("Cancelled");
        bill.ClosedAt.Should().NotBeNull();
        _context.SessionOrders.Single(o => o.Order_Id == 811).Status.Should().Be(OrderStatus.Cancelled);
        _context.SessionOrders.Single(o => o.Order_Id == 813).Status.Should().Be(OrderStatus.Pending);
    }

    [Theory]
    [InlineData(OrderStatus.Processing)]
    [InlineData(OrderStatus.Delivered)]
    public async Task CancelBill_WithOrdersBeingMadeOrServed_Returns400(OrderStatus status)
    {
        // Cancelling would leave this food unpaid, since a cancelled bill is never charged
        AddOrder(811, LeeBill, TableSession, OrderStatus.Pending);
        AddOrder(812, LeeBill, TableSession, status);

        var result = await Bills().CancelBill(TableSession, LeeBill);

        result.Should().BeOfType<BadRequestObjectResult>();
        Bill(LeeBill).Status.Should().Be(BillStatus.Open);
        _context.SessionOrders.Single(o => o.Order_Id == 811).Status.Should().Be(OrderStatus.Pending);
    }

    [Theory]
    [InlineData(BillStatus.Closed)]
    [InlineData(BillStatus.Paid)]
    [InlineData(BillStatus.Cancelled)]
    public async Task CancelBill_NotOpen_Returns400(BillStatus status)
    {
        Bill(LeeBill).Status = status;
        _context.SaveChanges();

        var result = await Bills().CancelBill(TableSession, LeeBill);

        result.Should().BeOfType<BadRequestObjectResult>();
        Bill(LeeBill).Status.Should().Be(status);
    }

    [Fact]
    public async Task CancelBill_UnknownBill_Returns404()
    {
        var result = await Bills().CancelBill(TableSession, 999);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task CancelBill_StaffAtAnotherLocation_Returns403()
    {
        var result = await Bills().CancelBill(OtherLocationSession, OtherLocationBill);

        ShouldBeOtherLocation(result);
    }

    [Fact]
    public async Task GetActiveBills_SeatedGuest_GetsTheirTablesBills()
    {
        var result = await Bills(SeatedGuest, "Customer").GetActiveBills();

        var bills = result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<List<BillResponse>>().Which;
        bills.Should().ContainSingle().Which.BillId.Should().Be(LeeBill);
    }

    [Fact]
    public async Task GetActiveBills_GuestWhoLeft_Returns404()
    {
        var result = await Bills(GuestWhoLeft, "Customer").GetActiveBills();

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetActiveBills_NoUserId_Returns401()
    {
        var result = await BillsAs(new ClaimsPrincipal(new ClaimsIdentity())).GetActiveBills();

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task GetBillSummary_AddsUpAllCharges()
    {
        _context.MenuItems.AddRange(
            new Menu_Item { item_id = 701, Name = "Salmon Nigiri", Category_id = 1 },
            new Menu_Item { item_id = 702, Name = "Sapporo", Category_id = 2 },
            new Menu_Item { item_id = 703, Name = "Hot Stone Wagyu", Category_id = 3 });
        AddOrder(801, LeeBill, TableSession, OrderStatus.Delivered);
        AddOrder(802, LeeBill, TableSession, OrderStatus.Cancelled);
        _context.OrderItems.AddRange(
            // AYCE item, free: left off the bill
            new OrderItems { Order_Item_Id = 1, Order_Key = 801, Menu_Id = 1, Item_Id = 701, Quantity = 3, Price_At_Time = 0m, Order_Item_Status = OrderStatus.Delivered },
            // Add-on: 2 x $7
            new OrderItems { Order_Item_Id = 2, Order_Key = 801, Menu_Id = 1, Item_Id = 702, Quantity = 2, Price_At_Time = 7m, Order_Item_Status = OrderStatus.Delivered },
            // Over the limit: 1 x $12
            new OrderItems { Order_Item_Id = 3, Order_Key = 801, Menu_Id = 1, Item_Id = 703, Quantity = 1, Price_At_Time = 12m, Order_Item_Status = OrderStatus.Delivered, Charge_Reason = ChargeReason.OverLimit },
            // Cancelled line: left off
            new OrderItems { Order_Item_Id = 4, Order_Key = 801, Menu_Id = 1, Item_Id = 702, Quantity = 1, Price_At_Time = 7m, Order_Item_Status = OrderStatus.Cancelled },
            // Line on a cancelled order: left off
            new OrderItems { Order_Item_Id = 5, Order_Key = 802, Menu_Id = 1, Item_Id = 702, Quantity = 3, Price_At_Time = 7m, Order_Item_Status = OrderStatus.Delivered });
        Bill(LeeBill).Leftover_Charge = 5m;
        _context.SaveChanges();

        var summary = OkValue<BillSummaryResponse>(await Bills().GetBillSummary(TableSession, LeeBill));

        summary.BaseChargesSubtotal.Should().Be(60m);                // 2 adults x $30
        summary.AddOnItems.Should().ContainSingle().Which.LineTotal.Should().Be(14m);
        summary.OverLimitItems.Should().ContainSingle().Which.ItemName.Should().Be("Hot Stone Wagyu");
        summary.OverLimitSubtotal.Should().Be(12m);
        summary.LeftoverCharge.Should().Be(5m);
        summary.Subtotal.Should().Be(91m);                             // 60 + 14 + 12 + 5
        summary.GratuityAmount.Should().Be(0m);
        summary.TaxAmount.Should().Be(4.55m);                         // 5% GST
        summary.TotalAmount.Should().Be(95.55m);
        summary.PricesLocked.Should().BeFalse();
    }

    [Fact]
    public async Task GetBillSummary_UnknownBill_Returns404()
    {
        var result = await Bills().GetBillSummary(TableSession, 999);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetBillSummary_GuestWhoLeft_IsForbidden()
    {
        var result = await Bills(GuestWhoLeft, "Customer").GetBillSummary(TableSession, LeeBill);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task CreateBill_NegativeGuestCount_ReturnsBadRequest()
    {
        // The old check let this through, negative counts should be rejected
        var result = await Bills().CreateBill(TableSession, Guests(adults: -1, children: 2));

        result.Should().BeOfType<BadRequestObjectResult>();
        _context.Bills.Count(b => b.Session_Id == TableSession).Should().Be(1);
    }
}