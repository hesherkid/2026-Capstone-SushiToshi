using System.Security.Claims;
using back_end.Services;
using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using back_end.controllers;
using back_end.Controllers;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.DTO.bill;
using back_end.DTO.DiningSessionDTOs;
using back_end.DTO.OrdersDTOs;
using back_end.DTO.SessionParticipantDTOs;

namespace back_end.Tests.Controllers;

// Guests are on one bill at their table: they pick or start it, and only see and order on that bill
public class BillLinkTests : IDisposable
{
    private const int Table = 7;          // session at table 1
    private const int OtherTable = 9;     // session at table 2
    private const int Lee = 70;           // Lee's bill at table 1
    private const int Park = 71;          // Park's bill at table 1
    private const int OtherTableBill = 90;

    private const int Ana = 10;           // on Lee's bill
    private const int Ben = 11;           // seated, no bill yet
    private const int Cy = 12;            // at the other table
    private const int Dee = 13;           // has an account, not seated yet
    private const int Server = 1;

    private readonly ApplicationDbContext _context;

    public BillLinkTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _context.Locations.Add(new Locations { Location_Id = 1, Name = "Downtown" });
        _context.Users.AddRange(
            new User { User_id = Ana, Email = "ana@example.com", First_name = "Ana", Role = UserRoles.Customer },
            new User { User_id = Ben, Email = "ben@example.com", First_name = "Ben", Role = UserRoles.Customer },
            new User { User_id = Cy, Email = "cy@example.com", First_name = "Cy", Role = UserRoles.Customer },
            new User { User_id = Dee, Email = "dee@example.com", First_name = "Dee", Role = UserRoles.Customer },
            new User { User_id = Server, Email = "staff@example.com", Role = UserRoles.Staff, Location_id = 1 });
        _context.Tables.AddRange(
            new TableEntity { Table_Id = 101, table_number = 1, seat_count = 8, Location_Id = 1, is_active = true },
            new TableEntity { Table_Id = 102, table_number = 2, seat_count = 4, Location_Id = 1, is_active = true },
            new TableEntity { Table_Id = 103, table_number = 3, seat_count = 4, Location_Id = 1, is_active = true });
        _context.DiningSessions.AddRange(
            new DiningSession { Session_Id = Table, Menu_Id = 1, Location_Id = 1, Table_Id = 101, Started_At = DateTime.UtcNow },
            new DiningSession { Session_Id = OtherTable, Menu_Id = 1, Location_Id = 1, Table_Id = 102, Started_At = DateTime.UtcNow });
        _context.Bills.AddRange(
            new Billing { Bill_Id = Lee, Session_Id = Table, Bill_Name = "Lee", Adult_Count = 2, Total_Count = 2, Created_At = DateTime.UtcNow.AddMinutes(-20) },
            new Billing { Bill_Id = Park, Session_Id = Table, Bill_Name = "Park", Adult_Count = 2, Total_Count = 2, Created_At = DateTime.UtcNow.AddMinutes(-10) },
            new Billing { Bill_Id = OtherTableBill, Session_Id = OtherTable, Bill_Name = "Wu", Adult_Count = 1, Total_Count = 1 });
        _context.SessionParticipants.AddRange(
            new SessionParticipant { Session_Id = Table, User_Id = Ana, User_Name = "Ana", Bill_Id = Lee },
            new SessionParticipant { Session_Id = Table, User_Id = Ben, User_Name = "Ben" },
            new SessionParticipant { Session_Id = OtherTable, User_Id = Cy, User_Name = "Cy", Bill_Id = OtherTableBill });
        _context.Categories.Add(new Category { Category_id = 1, Category_name = "Nigiri", Description = "Nigiri" });
        _context.MenuItems.Add(new Menu_Item { item_id = 1, Name = "Salmon Nigiri", Description = "Salmon", Category_id = 1 });
        _context.MenuItemAssignments.Add(new MenuItemAssignment { Assignment_Id = 1, Menu_Id = 1, Item_Id = 1, Status = MenuItemStatus.Available });
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    private static ControllerContext As(int userId, string role = "Customer") => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role),
            }, "TestAuth")),
        },
    };

    private SessionParticipantController Seats(int userId) =>
        new(_context, new Mock<ILogger<SessionParticipantController>>().Object) { ControllerContext = As(userId) };

    private BillController Bills(int userId, string role = "Customer") =>
        new(_context, new Mock<IConfiguration>().Object, new Mock<ILogger<BillController>>().Object, new Mock<IPricingService>().Object)
        { ControllerContext = As(userId, role) };

    private OrderController Orders(int userId) =>
        new(_context, new Mock<ILogger<OrderController>>().Object, new LocationAccessService(_context)) { ControllerContext = As(userId) };

    private DiningSessionController Sessions(int userId) =>
        new(_context, new Mock<IConfiguration>().Object, new Mock<ILogger<DashBoardController>>().Object, new LocationAccessService(_context))
        { ControllerContext = As(userId) };

    private static MyBillResponseDTO Body(IActionResult result) =>
        ((result as OkObjectResult)!.Value as MyBillResponseDTO)!;

    private int? SeatBill(int userId) =>
        _context.SessionParticipants.AsNoTracking().Single(sp => sp.User_Id == userId && sp.Left_At == null).Bill_Id;

    private static List<OrderItemCreateDTO> OneItem() =>
        new() { new() { Menu_Id = 1, Item_Id = 1, Quantity = 1 } };

    private void AnaOrderedOnLee()
    {
        _context.SessionOrders.Add(new SessionOrder { Order_Id = 500, session_id = Table, Bill_Id = Lee, User_Id = Ana, Status = OrderStatus.Pending, Created_At = DateTime.UtcNow });
        _context.SaveChanges();
    }

    // ---- Joining the table ----

    [Fact]
    public async Task Joining_ATableWithOneBill_PutsTheGuestOnIt()
    {
        _context.Bills.Add(new Billing { Bill_Id = 91, Session_Id = OtherTable + 1, Bill_Name = "Solo" });
        _context.DiningSessions.Add(new DiningSession { Session_Id = OtherTable + 1, Menu_Id = 1, Location_Id = 1, Table_Id = 103, Started_At = DateTime.UtcNow });
        _context.SaveChanges();

        await Sessions(Dee).JoinTableSession(new JoinTableSessionDTO { Table_Number = 3, Location_Id = 1 });

        SeatBill(Dee).Should().Be(91);
    }

    [Fact]
    public async Task Joining_ATableWithSeveralBills_LeavesTheChoiceToTheGuest()
    {
        await Sessions(Dee).JoinTableSession(new JoinTableSessionDTO { Table_Number = 1, Location_Id = 1 });

        SeatBill(Dee).Should().BeNull();
    }

    [Fact]
    public async Task Joining_ATableWhoseOnlyOtherBillAskedForTheBill_StillPutsTheGuestOnTheOpenOne()
    {
        (await _context.Bills.FindAsync(Lee))!.Requested_At = DateTime.UtcNow;
        _context.SaveChanges();

        await Sessions(Dee).JoinTableSession(new JoinTableSessionDTO { Table_Number = 1, Location_Id = 1 });

        SeatBill(Dee).Should().Be(Park);
    }

    // ---- Seeing and choosing a bill ----

    [Fact]
    public async Task MyBill_ShowsTheGuestsBillAndTheTablesOpenBills()
    {
        var body = Body(await Seats(Ana).GetMyBill(Table));

        body.My_Bill!.Bill_Id.Should().Be(Lee);
        body.Open_Bills.Select(b => b.Bill_Name).Should().Equal("Lee", "Park");
        body.Change_Blocked_Reason.Should().BeNull();
    }

    [Fact]
    public async Task MyBill_IsEmptyUntilTheGuestPicksOne()
    {
        var body = Body(await Seats(Ben).GetMyBill(Table));

        body.My_Bill.Should().BeNull();
        body.Open_Bills.Should().HaveCount(2);
    }

    [Fact]
    public async Task MyBill_RefusesGuestsAtAnotherTable()
    {
        var result = await Seats(Cy).GetMyBill(Table);

        (result as ObjectResult)!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task ChooseBill_PutsTheGuestOnIt()
    {
        var body = Body(await Seats(Ben).ChooseBill(Table, new ChooseBillDTO { Bill_Id = Park }));

        body.My_Bill!.Bill_Id.Should().Be(Park);
        SeatBill(Ben).Should().Be(Park);
    }

    [Fact]
    public async Task ChooseBill_LetsAGuestSwitchBeforeTheyOrder()
    {
        await Seats(Ana).ChooseBill(Table, new ChooseBillDTO { Bill_Id = Park });

        SeatBill(Ana).Should().Be(Park);
    }

    [Fact]
    public async Task ChooseBill_AfterOrdering_OnlyStaffCanMoveThem()
    {
        AnaOrderedOnLee();

        var result = await Seats(Ana).ChooseBill(Table, new ChooseBillDTO { Bill_Id = Park });

        result.Should().BeOfType<BadRequestObjectResult>();
        SeatBill(Ana).Should().Be(Lee);
        Body(await Seats(Ana).GetMyBill(Table)).Change_Blocked_Reason.Should().Contain("Lee");
    }

    [Fact]
    public async Task ChooseBill_ACancelledOrderDoesntHoldThemOnTheBill()
    {
        AnaOrderedOnLee();
        (await _context.SessionOrders.FindAsync(500))!.Status = OrderStatus.Cancelled;
        _context.SaveChanges();

        await Seats(Ana).ChooseBill(Table, new ChooseBillDTO { Bill_Id = Park });

        SeatBill(Ana).Should().Be(Park);
    }

    [Theory]
    [InlineData(OtherTableBill)] // another table's bill
    [InlineData(999)]            // no such bill
    public async Task ChooseBill_RefusesBillsThatArentAtTheTable(int billId)
    {
        var result = await Seats(Ben).ChooseBill(Table, new ChooseBillDTO { Bill_Id = billId });

        result.Should().BeOfType<BadRequestObjectResult>();
        SeatBill(Ben).Should().BeNull();
    }

    [Fact]
    public async Task ChooseBill_RefusesClosedAndRequestedBills()
    {
        (await _context.Bills.FindAsync(Lee))!.Status = BillStatus.Closed;
        (await _context.Bills.FindAsync(Park))!.Requested_At = DateTime.UtcNow;
        _context.SaveChanges();

        (await Seats(Ben).ChooseBill(Table, new ChooseBillDTO { Bill_Id = Lee })).Should().BeOfType<BadRequestObjectResult>();
        (await Seats(Ben).ChooseBill(Table, new ChooseBillDTO { Bill_Id = Park })).Should().BeOfType<BadRequestObjectResult>();
    }

    // ---- Starting a bill ----

    [Fact]
    public async Task GuestStartingABill_IsPutOnIt_AndStaffAreAskedToCheckTheCounts()
    {
        var result = await Bills(Ben).Create_Bill(Table, new CreateBill { Bill_Name = "Ben", Adult_Count = 3 });

        var bill = (result as OkObjectResult)!.Value as BillResponse;
        SeatBill(Ben).Should().Be(bill!.Bill_Id);
        (await _context.Bills.FindAsync(bill.Bill_Id))!.Started_By_Guest.Should().BeTrue();
    }

    [Fact]
    public async Task StaffStartingABill_IsntFlagged()
    {
        var result = await Bills(Server, "Staff").Create_Bill(Table, new CreateBill { Bill_Name = "Kim", Adult_Count = 2 });

        var bill = (result as OkObjectResult)!.Value as BillResponse;
        (await _context.Bills.FindAsync(bill!.Bill_Id))!.Started_By_Guest.Should().BeFalse();
    }

    [Fact]
    public async Task GuestWhoOrderedOnAnotherBill_CantStartANewOne()
    {
        AnaOrderedOnLee();

        var result = await Bills(Ana).Create_Bill(Table, new CreateBill { Bill_Name = "Ana", Adult_Count = 1 });

        result.Should().BeOfType<BadRequestObjectResult>();
        (await _context.Bills.CountAsync(b => b.Session_Id == Table)).Should().Be(2);
    }

    [Fact]
    public async Task GuestAtAnotherTable_CantStartABillHere()
    {
        var result = await Bills(Cy).Create_Bill(Table, new CreateBill { Bill_Name = "Cy", Adult_Count = 1 });

        (result as ObjectResult)!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        SeatBill(Cy).Should().Be(OtherTableBill);
    }

    [Fact]
    public async Task GuestBills_StillCountAgainstTheTablesSeats()
    {
        // 4 already on bills at an 8-seat table
        var result = await Bills(Ben).Create_Bill(Table, new CreateBill { Bill_Name = "Ben", Adult_Count = 5 });

        result.Should().BeOfType<BadRequestObjectResult>();
        SeatBill(Ben).Should().BeNull();
    }

    // ---- Only their own bill ----

    [Fact]
    public async Task Guests_OnlySeeTheirOwnBill()
    {
        var bills = ((await Bills(Ana).Get_Bills(Table)) as OkObjectResult)!.Value as List<BillResponse>;

        bills!.Select(b => b.Bill_Id).Should().Equal(Lee);
    }

    [Fact]
    public async Task Staff_SeeEveryBillAtTheTable()
    {
        var bills = ((await Bills(Server, "Staff").Get_Bills(Table)) as OkObjectResult)!.Value as List<BillResponse>;

        bills!.Should().HaveCount(2);
    }

    [Fact]
    public async Task Guests_CantOpenOrAskForAnotherPartysBill()
    {
        (await Bills(Ana).Get_Bill(Table, Park)).Should().BeOfType<ForbidResult>();
        (await Bills(Ana).GetBillSummary(Table, Park)).Should().BeOfType<ForbidResult>();
        (await Bills(Ana).Request_Bill(Table, Park)).Should().BeOfType<ForbidResult>();
        (await _context.Bills.FindAsync(Park))!.Requested_At.Should().BeNull();
    }

    [Fact]
    public async Task Guests_CantOrderOntoAnotherPartysBill()
    {
        var result = await Orders(Ana).CreateOrder(new OrderCreateDTO { Session_Id = Table, Bill_Id = Park, Items = OneItem() });

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        (await _context.SessionOrders.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Guests_CantOrderBeforeTheyHaveABill()
    {
        var result = await Orders(Ben).CreateOrder(new OrderCreateDTO { Session_Id = Table, Bill_Id = Lee, Items = OneItem() });

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Orders_CantGoOnABillFromAnotherTable()
    {
        // Seated at table 1, sending table 2's bill
        var result = await Orders(Ana).CreateOrder(new OrderCreateDTO { Session_Id = Table, Bill_Id = OtherTableBill, Items = OneItem() });

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Guests_CanOrderOnTheirOwnBill()
    {
        var result = await Orders(Ana).CreateOrder(new OrderCreateDTO { Session_Id = Table, Bill_Id = Lee, Items = OneItem() });

        result.Result.Should().BeOfType<OkObjectResult>();
    }
}
