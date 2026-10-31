using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using back_end.controllers;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.DTO.bill;
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
            new SessionParticipant { Session_Id = TableSession, User_Id = SeatedGuest, User_Name = "Guest K7P2" },
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
        new() { Bill_Name = name, Adult_Count = adults, Senior_Count = seniors, Child_Count = children, Tot_Count = tots };

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
        var bill = OkValue<BillResponse>(await Bills().Create_Bill(TableSession, Guests(adults: 1, children: 1)));

        bill.Session_Id.Should().Be(TableSession);
        bill.Bill_Name.Should().Be("Kim");
        bill.Total_Count.Should().Be(2);
        bill.Status.Should().Be("Open");
        bill.Table_Numbers.Should().Equal(6);
        _context.Bills.Should().Contain(b => b.Bill_Id == bill.Bill_Id && b.Status == BillStatus.Open);
    }

    [Fact]
    public async Task CreateBill_TableGroup_UsesEverySeat()
    {
        // 6 guests fit into two 4-seat tables but not one
        var bill = OkValue<BillResponse>(await Bills().Create_Bill(GroupSession, Guests(adults: 6)));

        bill.Table_Numbers.Should().BeEquivalentTo(new[] { 10, 11 });
    }

    [Fact]
    public async Task CreateBill_UnknownSession_Returns404()
    {
        var result = await Bills().Create_Bill(999, Guests(adults: 1));

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task CreateBill_StaffAtAnotherLocation_Returns403()
    {
        var result = await Bills(StaffElsewhere).Create_Bill(TableSession, Guests(adults: 1));

        ShouldBeOtherLocation(result);
    }

    [Fact]
    public async Task CreateBill_EndedSession_Returns400()
    {
        var result = await Bills().Create_Bill(EndedSession, Guests(adults: 1));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateBill_SessionWithNoTable_Returns400()
    {
        var result = await Bills().Create_Bill(NoTableSession, Guests(adults: 1));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateBill_NoGuests_Returns400()
    {
        var result = await Bills().Create_Bill(TableSession, Guests());

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateBill_MissingName_Returns400(string? name)
    {
        var result = await Bills().Create_Bill(TableSession, Guests(adults: 1, name: name));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateBill_OverTableCapacity_Returns400()
    {
        // Table 6 seats 4 and bill already has 2
        var result = await Bills().Create_Bill(TableSession, Guests(adults: 3));

        result.Should().BeOfType<BadRequestObjectResult>();
        _context.Bills.Count(b => b.Session_Id == TableSession).Should().Be(1);
    }

    [Fact]
    public async Task CreateBill_OnlyOpenBillsCountTowardCapacity()
    {
        Bill(LeeBill).Status = BillStatus.Closed;
        _context.SaveChanges();

        var result = await Bills().Create_Bill(TableSession, Guests(adults: 4));

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetBills_UnknownSession_Returns404()
    {
        var result = await Bills().Get_Bills(999);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetBills_TableInTheGroup_IsAccepted()
    {
        var result = await Bills().Get_Bills(GroupSession, _table_id: 11);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetBills_TableNotInTheSession_Returns400()
    {
        var result = await Bills().Get_Bills(TableSession, _table_id: 11);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetBill_ReturnsTheBill()
    {
        var bill = OkValue<BillResponse>(await Bills().Get_Bill(TableSession, LeeBill));

        bill.Bill_Id.Should().Be(LeeBill);
        bill.Bill_Name.Should().Be("Lee");
        bill.Table_Numbers.Should().Equal(6);
    }

    [Fact]
    public async Task GetBill_SeatedGuest_CanSeeIt()
    {
        var result = await Bills(SeatedGuest, "Customer").Get_Bill(TableSession, LeeBill);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetBill_GuestWhoLeftTheTable_IsForbidden()
    {
        var result = await Bills(GuestWhoLeft, "Customer").Get_Bill(TableSession, LeeBill);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetBill_BillFromAnotherSession_Returns404()
    {
        // Bill exists, but not in session 200
        var result = await Bills().Get_Bill(GroupSession, LeeBill);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task CloseBill_NoPendingOrders_ClosesIt()
    {
        AddOrder(801, LeeBill, TableSession, OrderStatus.Delivered);

        var bill = OkValue<BillResponse>(await Bills().Close_Bill(TableSession, LeeBill));

        bill.Status.Should().Be("Closed");
        bill.Closed_At.Should().NotBeNull();
        Bill(LeeBill).Status.Should().Be(BillStatus.Closed);
    }

    [Fact]
    public async Task CloseBill_WithPendingOrders_Returns400_AndStaysOpen()
    {
        AddOrder(801, LeeBill, TableSession, OrderStatus.Pending);

        var result = await Bills().Close_Bill(TableSession, LeeBill);

        result.Should().BeOfType<BadRequestObjectResult>();
        Bill(LeeBill).Status.Should().Be(BillStatus.Open);
        Bill(LeeBill).Closed_At.Should().BeNull();
    }

    [Fact]
    public async Task CloseBill_AlreadyClosed_Returns400()
    {
        Bill(LeeBill).Status = BillStatus.Closed;
        _context.SaveChanges();

        var result = await Bills().Close_Bill(TableSession, LeeBill);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CloseBill_UnknownBill_Returns404()
    {
        var result = await Bills().Close_Bill(TableSession, 999);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task CloseBill_StaffAtAnotherLocation_Returns403()
    {
        var result = await Bills().Close_Bill(OtherLocationSession, OtherLocationBill);

        ShouldBeOtherLocation(result);
        Bill(OtherLocationBill).Status.Should().Be(BillStatus.Open);
    }

    [Fact]
    public async Task CancelBill_CancelsTheBill_AndOnlyItsPendingOrders()
    {
        _context.Bills.Add(new Billing { Bill_Id = 902, Session_Id = TableSession, Bill_Name = "Choi", Adult_Count = 1, Total_Count = 1 });
        _context.SaveChanges();
        AddOrder(811, LeeBill, TableSession, OrderStatus.Pending);
        AddOrder(812, LeeBill, TableSession, OrderStatus.Delivered);
        AddOrder(813, 902, TableSession, OrderStatus.Pending);

        var bill = OkValue<BillResponse>(await Bills().Cancel_Bill(TableSession, LeeBill));

        bill.Status.Should().Be("Cancelled");
        bill.Closed_At.Should().NotBeNull();
        _context.SessionOrders.Single(o => o.Order_Id == 811).Status.Should().Be(OrderStatus.Cancelled);
        _context.SessionOrders.Single(o => o.Order_Id == 812).Status.Should().Be(OrderStatus.Delivered);
        _context.SessionOrders.Single(o => o.Order_Id == 813).Status.Should().Be(OrderStatus.Pending);
    }

    [Theory]
    [InlineData(BillStatus.Closed)]
    [InlineData(BillStatus.Paid)]
    [InlineData(BillStatus.Cancelled)]
    public async Task CancelBill_NotOpen_Returns400(BillStatus status)
    {
        Bill(LeeBill).Status = status;
        _context.SaveChanges();

        var result = await Bills().Cancel_Bill(TableSession, LeeBill);

        result.Should().BeOfType<BadRequestObjectResult>();
        Bill(LeeBill).Status.Should().Be(status);
    }

    [Fact]
    public async Task CancelBill_UnknownBill_Returns404()
    {
        var result = await Bills().Cancel_Bill(TableSession, 999);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task CancelBill_StaffAtAnotherLocation_Returns403()
    {
        var result = await Bills().Cancel_Bill(OtherLocationSession, OtherLocationBill);

        ShouldBeOtherLocation(result);
    }

    [Fact]
    public async Task GetActiveBills_SeatedGuest_GetsTheirTablesBills()
    {
        var result = await Bills(SeatedGuest, "Customer").GetActiveBills();

        var bills = result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<List<BillResponse>>().Which;
        bills.Should().ContainSingle().Which.Bill_Id.Should().Be(LeeBill);
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

        summary.Base_Charges_Subtotal.Should().Be(60m);                // 2 adults x $30
        summary.AddOn_Items.Should().ContainSingle().Which.Line_Total.Should().Be(14m);
        summary.OverLimit_Items.Should().ContainSingle().Which.Item_Name.Should().Be("Hot Stone Wagyu");
        summary.OverLimit_Subtotal.Should().Be(12m);
        summary.Leftover_Charge.Should().Be(5m);
        summary.Subtotal.Should().Be(91m);                             // 60 + 14 + 12 + 5
        summary.Gratuity_Amount.Should().Be(0m);
        summary.Tax_Amount.Should().Be(4.55m);                         // 5% GST
        summary.Total_Amount.Should().Be(95.55m);
        summary.Prices_Locked.Should().BeFalse();
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
}