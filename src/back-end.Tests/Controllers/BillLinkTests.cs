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
using back_end.DTO.DashBoardDTOs;
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

    // Guests always confirm their bill themselves, even when the table has only one
    [Fact]
    public async Task Joining_ATableWithOneBill_LeavesTheChoiceToTheGuest()
    {
        _context.Bills.Add(new Billing { Bill_Id = 91, Session_Id = OtherTable + 1, Bill_Name = "Solo" });
        _context.DiningSessions.Add(new DiningSession { Session_Id = OtherTable + 1, Menu_Id = 1, Location_Id = 1, Table_Id = 103, Started_At = DateTime.UtcNow });
        _context.SaveChanges();

        await Sessions(Dee).JoinTableSession(new JoinTableSessionDTO { Table_Number = 3, Location_Id = 1 });

        SeatBill(Dee).Should().BeNull();
    }

    [Fact]
    public async Task Joining_ATableWithSeveralBills_LeavesTheChoiceToTheGuest()
    {
        await Sessions(Dee).JoinTableSession(new JoinTableSessionDTO { Table_Number = 1, Location_Id = 1 });

        SeatBill(Dee).Should().BeNull();
    }

    [Theory]
    [InlineData(BillStatus.Closed)]
    [InlineData(BillStatus.Cancelled)]
    public async Task Rejoining_AfterTheirBillEnded_AsksThemToChooseAgain(BillStatus status)
    {
        // Ana is still seated on Lee when it ends, then rescans the QR code
        (await _context.Bills.FindAsync(Lee))!.Status = status;
        _context.SaveChanges();

        await Sessions(Ana).JoinTableSession(new JoinTableSessionDTO { Table_Number = 1, Location_Id = 1 });

        SeatBill(Ana).Should().BeNull();
        Body(await Seats(Ana).GetMyBill(Table)).My_Bill.Should().BeNull();
    }

    [Fact]
    public async Task Rejoining_KeepsThemOnTheirOpenBill()
    {
        await Sessions(Ana).JoinTableSession(new JoinTableSessionDTO { Table_Number = 1, Location_Id = 1 });

        SeatBill(Ana).Should().Be(Lee);
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
        var result = await Bills(Ben).CreateBill(Table, new CreateBill { Bill_Name = "Ben", Adult_Count = 3 });

        var bill = (result as OkObjectResult)!.Value as BillResponse;
        SeatBill(Ben).Should().Be(bill!.Bill_Id);
        (await _context.Bills.FindAsync(bill.Bill_Id))!.Started_By_Guest.Should().BeTrue();
    }

    [Fact]
    public async Task StaffStartingABill_IsntFlagged()
    {
        var result = await Bills(Server, "Staff").CreateBill(Table, new CreateBill { Bill_Name = "Kim", Adult_Count = 2 });

        var bill = (result as OkObjectResult)!.Value as BillResponse;
        (await _context.Bills.FindAsync(bill!.Bill_Id))!.Started_By_Guest.Should().BeFalse();
    }

    [Fact]
    public async Task GuestWhoOrderedOnAnotherBill_CantStartANewOne()
    {
        AnaOrderedOnLee();

        var result = await Bills(Ana).CreateBill(Table, new CreateBill { Bill_Name = "Ana", Adult_Count = 1 });

        result.Should().BeOfType<BadRequestObjectResult>();
        (await _context.Bills.CountAsync(b => b.Session_Id == Table)).Should().Be(2);
    }

    [Fact]
    public async Task GuestAtAnotherTable_CantStartABillHere()
    {
        var result = await Bills(Cy).CreateBill(Table, new CreateBill { Bill_Name = "Cy", Adult_Count = 1 });

        (result as ObjectResult)!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        SeatBill(Cy).Should().Be(OtherTableBill);
    }

    [Fact]
    public async Task GuestBills_StillCountAgainstTheTablesSeats()
    {
        // 4 already on bills at an 8-seat table
        var result = await Bills(Ben).CreateBill(Table, new CreateBill { Bill_Name = "Ben", Adult_Count = 5 });

        result.Should().BeOfType<BadRequestObjectResult>();
        SeatBill(Ben).Should().BeNull();
    }

    [Fact]
    public async Task GuestBills_OnlyCountOpenBillsAgainstTheSeats()
    {
        (await _context.Bills.FindAsync(Park))!.Status = BillStatus.Closed;
        _context.SaveChanges();

        // Lee's 2 + Ben's 5 fit the 8 seats once Park's bill is closed
        var result = await Bills(Ben).CreateBill(Table, new CreateBill { Bill_Name = "Ben", Adult_Count = 5 });

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task StaffBills_CanGoPastTheTablesSeats()
    {
        // 4 already on bills at an 8-seat table; the party pulled up extra chairs
        var result = await Bills(Server, "Staff").CreateBill(Table, new CreateBill { Bill_Name = "Kim", Adult_Count = 5 });

        result.Should().BeOfType<OkObjectResult>();
    }

    // ---- Changing their bill's guest count ----

    private static UpdateGuestCountsDTO Counts(int adults, int seniors = 0, int children = 0, int toddlers = 0) =>
        new() { Adult_Count = adults, Senior_Count = seniors, Child_Count = children, Tot_Count = toddlers };

    [Fact]
    public async Task MyBill_ShowsHowManyPeopleAreOnEachBill()
    {
        await Seats(Ben).ChooseBill(Table, new ChooseBillDTO { Bill_Id = Lee });

        var body = Body(await Seats(Ana).GetMyBill(Table));

        body.My_Bill!.People_Count.Should().Be(2);
        body.My_Bill.Adult_Count.Should().Be(2);
        body.Open_Bills.Single(b => b.Bill_Id == Park).People_Count.Should().Be(0);
    }

    [Fact]
    public async Task UpdateGuestCounts_SavesThem_AndAsksStaffToCheck()
    {
        var body = Body(await Seats(Ana).UpdateGuestCounts(Table, Counts(adults: 3, children: 1)));

        body.My_Bill!.Total_Count.Should().Be(4);
        var lee = await _context.Bills.AsNoTracking().SingleAsync(b => b.Bill_Id == Lee);
        lee.Adult_Count.Should().Be(3);
        lee.Child_Count.Should().Be(1);
        lee.Total_Count.Should().Be(4);
        lee.Counts_Changed_By_Guest.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateGuestCounts_WithTheSameCounts_DoesntFlagTheBill()
    {
        await Seats(Ana).UpdateGuestCounts(Table, Counts(adults: 2));

        (await _context.Bills.AsNoTracking().SingleAsync(b => b.Bill_Id == Lee)).Counts_Changed_By_Guest.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateGuestCounts_NeedsABill()
    {
        var result = await Seats(Ben).UpdateGuestCounts(Table, Counts(adults: 1));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateGuestCounts_RefusesGuestsAtAnotherTable()
    {
        var result = await Seats(Cy).UpdateGuestCounts(Table, Counts(adults: 1));

        (result as ObjectResult)!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task UpdateGuestCounts_NeedsAtLeastOneGuest()
    {
        var result = await Seats(Ana).UpdateGuestCounts(Table, Counts(adults: 0));

        result.Should().BeOfType<BadRequestObjectResult>();
        (await _context.Bills.AsNoTracking().SingleAsync(b => b.Bill_Id == Lee)).Total_Count.Should().Be(2);
    }

    [Fact]
    public async Task UpdateGuestCounts_CantSeatMoreThanTheTableHas()
    {
        // Table 1 seats 8 and Park's bill has 2
        var result = await Seats(Ana).UpdateGuestCounts(Table, Counts(adults: 7));

        result.Should().BeOfType<BadRequestObjectResult>();
        (await Seats(Ana).UpdateGuestCounts(Table, Counts(adults: 6))).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpdateGuestCounts_CanLowerTheCount_WhenStaffWentPastTheSeats()
    {
        // Staff record 9 on Lee's bill at an 8-seat table (Park's has 2)
        (await Bills(Server, "Staff").UpdateGuestCounts(Table, Lee, Counts(adults: 9))).Should().BeOfType<OkObjectResult>();

        (await Seats(Ana).UpdateGuestCounts(Table, Counts(adults: 10))).Should().BeOfType<BadRequestObjectResult>();
        (await Seats(Ana).UpdateGuestCounts(Table, Counts(adults: 8))).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpdateGuestCounts_RefusesBillsThatAskedForTheBillOrClosed()
    {
        (await _context.Bills.FindAsync(Lee))!.Requested_At = DateTime.UtcNow;
        (await _context.Bills.FindAsync(OtherTableBill))!.Status = BillStatus.Closed;
        _context.SaveChanges();

        (await Seats(Ana).UpdateGuestCounts(Table, Counts(adults: 3))).Should().BeOfType<BadRequestObjectResult>();
        (await Seats(Cy).UpdateGuestCounts(OtherTable, Counts(adults: 3))).Should().BeOfType<BadRequestObjectResult>();
    }

    // ---- Staff fixing guest counts ----

    private static string? StaffPolicy(Type controller, string method) =>
        controller.GetMethod(method)!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .Single().Policy;

    [Fact]
    public void StaffOnly_EditCountsAndMoveGuests()
    {
        StaffPolicy(typeof(BillController), nameof(BillController.UpdateGuestCounts)).Should().Be("staffOnly");
        StaffPolicy(typeof(SessionParticipantController), nameof(SessionParticipantController.MoveGuest)).Should().Be("staffOnly");
        StaffPolicy(typeof(BillController), nameof(BillController.MergeBill)).Should().Be("staffOnly");
        StaffPolicy(typeof(BillController), nameof(BillController.LockGuestCounts)).Should().Be("staffOnly");
    }

    // ---- Staff locking guest counts ----

    private async Task<IActionResult> LockCounts(int billId, bool locked) =>
        await Bills(Server, "Staff").LockGuestCounts(Table, billId, new LockGuestCountsDTO { Locked = locked });

    [Fact]
    public async Task LockedCounts_CantBeChangedByGuests_UntilStaffUnlockThem()
    {
        (await LockCounts(Lee, true)).Should().BeOfType<OkObjectResult>();

        (await Seats(Ana).UpdateGuestCounts(Table, Counts(adults: 3))).Should().BeOfType<BadRequestObjectResult>();
        (await _context.Bills.AsNoTracking().SingleAsync(b => b.Bill_Id == Lee)).Total_Count.Should().Be(2);

        (await LockCounts(Lee, false)).Should().BeOfType<OkObjectResult>();
        (await Seats(Ana).UpdateGuestCounts(Table, Counts(adults: 3))).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task LockingCounts_MeansStaffCheckedThem_SoClearsTheGuestFlags()
    {
        await Seats(Ana).UpdateGuestCounts(Table, Counts(adults: 3));

        await LockCounts(Lee, true);

        var lee = await _context.Bills.AsNoTracking().SingleAsync(b => b.Bill_Id == Lee);
        lee.Counts_Locked.Should().BeTrue();
        lee.Counts_Changed_By_Guest.Should().BeFalse();
        lee.Started_By_Guest.Should().BeFalse();
    }

    [Fact]
    public async Task LockedBill_KeepsItsGuests_UntilStaffMoveThem()
    {
        await LockCounts(Lee, true);

        (await Seats(Ana).ChooseBill(Table, new ChooseBillDTO { Bill_Id = Park })).Should().BeOfType<BadRequestObjectResult>();
        (await Bills(Ana).CreateBill(Table, new CreateBill { Bill_Name = "Ana", Adult_Count = 1 })).Should().BeOfType<BadRequestObjectResult>();
        Body(await Seats(Ana).GetMyBill(Table)).Change_Blocked_Reason.Should().Contain("confirmed your party");
        SeatBill(Ana).Should().Be(Lee);

        // Staying on it is fine, and staff can still move them
        (await Seats(Ana).ChooseBill(Table, new ChooseBillDTO { Bill_Id = Lee })).Should().BeOfType<OkObjectResult>();
        await LockCounts(Lee, false);
        (await Seats(Ana).ChooseBill(Table, new ChooseBillDTO { Bill_Id = Park })).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task LockedCounts_CanStillBeChangedByStaff_AndStayLocked()
    {
        await LockCounts(Lee, true);

        (await Bills(Server, "Staff").UpdateGuestCounts(Table, Lee, Counts(adults: 4))).Should().BeOfType<OkObjectResult>();

        var lee = await _context.Bills.AsNoTracking().SingleAsync(b => b.Bill_Id == Lee);
        lee.Total_Count.Should().Be(4);
        lee.Counts_Locked.Should().BeTrue();
    }

    [Fact]
    public async Task LockedCounts_ShowToTheGuestAndOnTheDashboard()
    {
        await LockCounts(Lee, true);

        Body(await Seats(Ana).GetMyBill(Table)).My_Bill!.Counts_Locked.Should().BeTrue();
        (await DashboardBill(Lee)).Counts_Locked.Should().BeTrue();
        (await DashboardBill(Park)).Counts_Locked.Should().BeFalse();
    }

    [Fact]
    public async Task LockingCounts_OnlyOnOpenBillsAtTheTable()
    {
        (await _context.Bills.FindAsync(Park))!.Status = BillStatus.Closed;
        _context.SaveChanges();

        (await LockCounts(Park, true)).Should().BeOfType<BadRequestObjectResult>();
        (await LockCounts(OtherTableBill, true)).Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task StaffEditingCounts_SavesThem_AndClearsTheGuestFlags()
    {
        var lee = await _context.Bills.FindAsync(Lee);
        lee!.Started_By_Guest = true;
        lee.Counts_Changed_By_Guest = true;
        _context.SaveChanges();

        var result = await Bills(Server, "Staff").UpdateGuestCounts(Table, Lee, Counts(adults: 2, seniors: 1, toddlers: 1));

        result.Should().BeOfType<OkObjectResult>();
        var saved = await _context.Bills.AsNoTracking().SingleAsync(b => b.Bill_Id == Lee);
        saved.Total_Count.Should().Be(4);
        saved.Senior_Count.Should().Be(1);
        saved.Tot_Count.Should().Be(1);
        saved.Started_By_Guest.Should().BeFalse();
        saved.Counts_Changed_By_Guest.Should().BeFalse();
    }

    [Fact]
    public async Task StaffEditingCounts_CanGoPastTheTablesSeats()
    {
        // Table 1 seats 8 and Park's bill has 2: the party pulled up extra chairs
        var result = await Bills(Server, "Staff").UpdateGuestCounts(Table, Lee, Counts(adults: 7));

        result.Should().BeOfType<OkObjectResult>();
        (await _context.Bills.AsNoTracking().SingleAsync(b => b.Bill_Id == Lee)).Total_Count.Should().Be(7);
    }

    [Fact]
    public async Task StaffEditingCounts_WorksAfterTheGuestsAskedForTheBill()
    {
        (await _context.Bills.FindAsync(Lee))!.Requested_At = DateTime.UtcNow;
        _context.SaveChanges();

        (await Bills(Server, "Staff").UpdateGuestCounts(Table, Lee, Counts(adults: 3))).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task StaffEditingCounts_NeedsAtLeastOneGuestAndAnOpenBill()
    {
        (await Bills(Server, "Staff").UpdateGuestCounts(Table, Lee, Counts(adults: 0))).Should().BeOfType<BadRequestObjectResult>();

        (await _context.Bills.FindAsync(Park))!.Status = BillStatus.Closed;
        _context.SaveChanges();
        (await Bills(Server, "Staff").UpdateGuestCounts(Table, Park, Counts(adults: 1))).Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task StaffEditingCounts_NeedsTheBillAtThatSession()
    {
        var result = await Bills(Server, "Staff").UpdateGuestCounts(Table, OtherTableBill, Counts(adults: 1));

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // ---- Staff moving guests between bills ----

    private SessionParticipantController StaffSeats() =>
        new(_context, new Mock<ILogger<SessionParticipantController>>().Object) { ControllerContext = As(Server, "Staff") };

    private int SeatOf(int userId) =>
        _context.SessionParticipants.AsNoTracking().Single(sp => sp.User_Id == userId && sp.Left_At == null).Participant_Id;

    private int OrderBill(int orderId) =>
        _context.SessionOrders.AsNoTracking().Single(o => o.Order_Id == orderId).Bill_Id;

    [Fact]
    public async Task StaffMovingAGuest_TakesTheirOrdersWithThem_ButNotCancelledOnes()
    {
        AnaOrderedOnLee();
        _context.SessionOrders.AddRange(
            new SessionOrder { Order_Id = 501, session_id = Table, Bill_Id = Lee, User_Id = Ana, Status = OrderStatus.Cancelled, Created_At = DateTime.UtcNow },
            // Staff ordered this one for the table, so it isn't Ana's to take
            new SessionOrder { Order_Id = 502, session_id = Table, Bill_Id = Lee, User_Id = Server, Status = OrderStatus.Pending, Created_At = DateTime.UtcNow });
        _context.SaveChanges();

        var result = await StaffSeats().MoveGuest(Table, SeatOf(Ana), new MoveGuestDTO { Bill_Id = Park, Move_Orders = true });

        result.Should().BeOfType<OkObjectResult>();
        SeatBill(Ana).Should().Be(Park);
        OrderBill(500).Should().Be(Park);
        OrderBill(501).Should().Be(Lee);
        OrderBill(502).Should().Be(Lee);
    }

    [Fact]
    public async Task StaffMovingAGuest_CanLeaveTheirOrdersBehind()
    {
        AnaOrderedOnLee();

        await StaffSeats().MoveGuest(Table, SeatOf(Ana), new MoveGuestDTO { Bill_Id = Park, Move_Orders = false });

        SeatBill(Ana).Should().Be(Park);
        OrderBill(500).Should().Be(Lee);
    }

    [Fact]
    public async Task StaffCanPutSomeoneWithNoBillOnOne()
    {
        await StaffSeats().MoveGuest(Table, SeatOf(Ben), new MoveGuestDTO { Bill_Id = Park });

        SeatBill(Ben).Should().Be(Park);
    }

    [Fact]
    public async Task StaffMovingAGuest_OnlyOntoAnOpenBillAtTheirTable()
    {
        (await _context.Bills.FindAsync(Park))!.Status = BillStatus.Closed;
        _context.SaveChanges();

        (await StaffSeats().MoveGuest(Table, SeatOf(Ana), new MoveGuestDTO { Bill_Id = Park })).Should().BeOfType<BadRequestObjectResult>();
        (await StaffSeats().MoveGuest(Table, SeatOf(Ana), new MoveGuestDTO { Bill_Id = OtherTableBill })).Should().BeOfType<BadRequestObjectResult>();
        (await StaffSeats().MoveGuest(Table, SeatOf(Cy), new MoveGuestDTO { Bill_Id = Lee })).Should().BeOfType<NotFoundObjectResult>();
        SeatBill(Ana).Should().Be(Lee);
    }

    [Fact]
    public async Task StaffMovingAGuest_CantTakeOrdersOffAClosedBill()
    {
        AnaOrderedOnLee();
        (await _context.Bills.FindAsync(Lee))!.Status = BillStatus.Closed;
        _context.SaveChanges();

        var result = await StaffSeats().MoveGuest(Table, SeatOf(Ana), new MoveGuestDTO { Bill_Id = Park, Move_Orders = true });

        result.Should().BeOfType<BadRequestObjectResult>();
        OrderBill(500).Should().Be(Lee);
        SeatBill(Ana).Should().Be(Lee);
    }

    private Billing SavedBill(int billId) => _context.Bills.AsNoTracking().Single(b => b.Bill_Id == billId);

    [Fact]
    public async Task StaffMovingAGuest_MovesThemFromOneBillsCountToTheOthers()
    {
        var result = await StaffSeats().MoveGuest(Table, SeatOf(Ana), new MoveGuestDTO { Bill_Id = Park, Guest_Type = "adult" });

        result.Should().BeOfType<OkObjectResult>();
        SavedBill(Lee).Adult_Count.Should().Be(1);
        SavedBill(Lee).Total_Count.Should().Be(1);
        SavedBill(Park).Adult_Count.Should().Be(3);
        SavedBill(Park).Total_Count.Should().Be(3);
    }

    [Fact]
    public async Task StaffMovingAGuest_CanMoveSeveralGuestsForOnePhone()
    {
        var lee = (await _context.Bills.FindAsync(Lee))!;
        lee.Adult_Count = 3;
        lee.Child_Count = 2;
        lee.Total_Count = 5;
        _context.SaveChanges();

        // Ana's phone stands for her and her two children
        await StaffSeats().MoveGuest(Table, SeatOf(Ana), new MoveGuestDTO { Bill_Id = Park, Guest_Type = "child", Guest_Count = 2 });

        SavedBill(Lee).Child_Count.Should().Be(0);
        SavedBill(Lee).Total_Count.Should().Be(3);
        SavedBill(Park).Child_Count.Should().Be(2);
        SavedBill(Park).Total_Count.Should().Be(4);

        (await StaffSeats().MoveGuest(Table, SeatOf(Ben), new MoveGuestDTO { Bill_Id = Lee }))
            .Should().BeOfType<OkObjectResult>();
        // Lee has 3 adults; moving 3 off would leave it empty
        var result = await StaffSeats().MoveGuest(Table, SeatOf(Ben), new MoveGuestDTO { Bill_Id = Park, Guest_Type = "adult", Guest_Count = 3 });
        result.Should().BeOfType<BadRequestObjectResult>();
        SavedBill(Lee).Total_Count.Should().Be(3);
    }

    [Fact]
    public async Task StaffMovingAGuest_LeavesTheCountsAloneWithoutAGuestType()
    {
        await StaffSeats().MoveGuest(Table, SeatOf(Ana), new MoveGuestDTO { Bill_Id = Park });

        SavedBill(Lee).Total_Count.Should().Be(2);
        SavedBill(Park).Total_Count.Should().Be(2);
    }

    [Fact]
    public async Task StaffMovingAGuest_NeedsThatKindOfGuestOnTheOldBill()
    {
        // Lee is two adults, no seniors
        var result = await StaffSeats().MoveGuest(Table, SeatOf(Ana), new MoveGuestDTO { Bill_Id = Park, Guest_Type = "senior" });

        result.Should().BeOfType<BadRequestObjectResult>();
        SeatBill(Ana).Should().Be(Lee);
        SavedBill(Park).Total_Count.Should().Be(2);
    }

    [Fact]
    public async Task StaffMovingAGuest_CantTakeTheLastGuestOffABillsCount()
    {
        var lee = (await _context.Bills.FindAsync(Lee))!;
        lee.Adult_Count = 1;
        lee.Total_Count = 1;
        _context.SaveChanges();

        var result = await StaffSeats().MoveGuest(Table, SeatOf(Ana), new MoveGuestDTO { Bill_Id = Park, Guest_Type = "adult" });

        result.Should().BeOfType<BadRequestObjectResult>();
        SeatBill(Ana).Should().Be(Lee);
        SavedBill(Lee).Total_Count.Should().Be(1);
    }

    // ---- Staff merging bills ----

    [Fact]
    public async Task StaffMergingBills_MovesEveryoneTheirOrdersAndTheGuestCounts()
    {
        AnaOrderedOnLee();
        _context.SessionOrders.AddRange(
            // Staff ordered this one for the table
            new SessionOrder { Order_Id = 502, session_id = Table, Bill_Id = Lee, User_Id = Server, Status = OrderStatus.Delivered, Created_At = DateTime.UtcNow },
            new SessionOrder { Order_Id = 503, session_id = Table, Bill_Id = Lee, User_Id = Ana, Status = OrderStatus.Cancelled, Created_At = DateTime.UtcNow });
        // Someone on Lee who has since left
        _context.SessionParticipants.Add(new SessionParticipant { Session_Id = Table, User_Id = Dee, User_Name = "Dee", Bill_Id = Lee, Left_At = DateTime.UtcNow });
        var lee = (await _context.Bills.FindAsync(Lee))!;
        lee.Child_Count = 1;
        lee.Total_Count = 3;
        lee.Leftover_Charge = 5m;
        _context.SaveChanges();

        var result = await Bills(Server, "Staff").MergeBill(Table, Lee, new MergeBillDTO { Into_Bill_Id = Park });

        result.Should().BeOfType<OkObjectResult>();
        SeatBill(Ana).Should().Be(Park);
        _context.SessionParticipants.AsNoTracking().Single(sp => sp.User_Id == Dee).Bill_Id.Should().Be(Park);
        OrderBill(500).Should().Be(Park);
        OrderBill(502).Should().Be(Park);
        OrderBill(503).Should().Be(Lee);

        // Lee's 2 adults and a child who never joined on a phone
        var park = SavedBill(Park);
        park.Adult_Count.Should().Be(4);
        park.Child_Count.Should().Be(1);
        park.Total_Count.Should().Be(5);
        park.Leftover_Charge.Should().Be(5m);
        SavedBill(Lee).Status.Should().Be(BillStatus.Cancelled);
        SavedBill(Lee).Leftover_Charge.Should().Be(0m);
    }

    [Fact]
    public async Task StaffMergingBills_OnlyOpenBillsAtTheSameTable()
    {
        var staff = Bills(Server, "Staff");
        (await staff.MergeBill(Table, Lee, new MergeBillDTO { Into_Bill_Id = Lee })).Should().BeOfType<BadRequestObjectResult>();
        (await staff.MergeBill(Table, Lee, new MergeBillDTO { Into_Bill_Id = OtherTableBill })).Should().BeOfType<BadRequestObjectResult>();
        (await staff.MergeBill(Table, OtherTableBill, new MergeBillDTO { Into_Bill_Id = Lee })).Should().BeOfType<NotFoundObjectResult>();

        (await _context.Bills.FindAsync(Park))!.Status = BillStatus.Closed;
        _context.SaveChanges();
        (await staff.MergeBill(Table, Lee, new MergeBillDTO { Into_Bill_Id = Park })).Should().BeOfType<BadRequestObjectResult>();
        (await staff.MergeBill(Table, Park, new MergeBillDTO { Into_Bill_Id = Lee })).Should().BeOfType<BadRequestObjectResult>();

        SavedBill(Lee).Status.Should().Be(BillStatus.Open);
        SeatBill(Ana).Should().Be(Lee);
    }

    // ---- What staff see ----

    private async Task<DashBoardBillDTO> DashboardBill(int billId)
    {
        var dashboard = new DashBoardController(_context, new Mock<IConfiguration>().Object,
            new Mock<ILogger<DashBoardController>>().Object, new LocationAccessService(_context))
        { ControllerContext = As(Server, "Staff") };
        var sessions = ((await dashboard.GetDashBoardSessions(null)) as OkObjectResult)!.Value as List<DashBoardSessionsDTO>;
        return sessions!.SelectMany(s => s.Bills).Single(b => b.Bill_Id == billId);
    }

    [Fact]
    public async Task Dashboard_ShowsHowManyPeopleAreOnABill_AndWhenAGuestChangedItsCount()
    {
        await Seats(Ben).ChooseBill(Table, new ChooseBillDTO { Bill_Id = Lee });
        await Seats(Ana).UpdateGuestCounts(Table, Counts(adults: 1));

        var lee = await DashboardBill(Lee);

        lee.People_On_Bill.Should().Be(2);
        lee.Total_Guests.Should().Be(1);
        lee.Counts_Changed_By_Guest.Should().BeTrue();
    }

    [Fact]
    public async Task Dashboard_ListsWhoIsOnWhichBill()
    {
        var dashboard = new DashBoardController(_context, new Mock<IConfiguration>().Object,
            new Mock<ILogger<DashBoardController>>().Object, new LocationAccessService(_context))
        { ControllerContext = As(Server, "Staff") };
        var sessions = ((await dashboard.GetDashBoardSessions(null)) as OkObjectResult)!.Value as List<DashBoardSessionsDTO>;

        var people = sessions!.Single(s => s.Session_Id == Table).People;
        people.Should().HaveCount(2);
        people.Single(p => p.User_Name == "Ana").Bill_Id.Should().Be(Lee);
        people.Single(p => p.User_Name == "Ben").Bill_Id.Should().BeNull();
    }

    [Fact]
    public async Task Dashboard_ShowsTheTablesSeats_SoStaffCanBeWarnedBeforeGoingPastThem()
    {
        var dashboard = new DashBoardController(_context, new Mock<IConfiguration>().Object,
            new Mock<ILogger<DashBoardController>>().Object, new LocationAccessService(_context))
        { ControllerContext = As(Server, "Staff") };
        var sessions = ((await dashboard.GetDashBoardSessions(null)) as OkObjectResult)!.Value as List<DashBoardSessionsDTO>;

        sessions!.Single(s => s.Session_Id == Table).Seat_Count.Should().Be(8);
    }

    // ---- Only their own bill ----

    [Fact]
    public async Task Guests_OnlySeeTheirOwnBill()
    {
        var bills = ((await Bills(Ana).GetBills(Table)) as OkObjectResult)!.Value as List<BillResponse>;

        bills!.Select(b => b.Bill_Id).Should().Equal(Lee);
    }

    [Fact]
    public async Task Staff_SeeEveryBillAtTheTable()
    {
        var bills = ((await Bills(Server, "Staff").GetBills(Table)) as OkObjectResult)!.Value as List<BillResponse>;

        bills!.Should().HaveCount(2);
    }

    [Fact]
    public async Task Guests_CantOpenOrAskForAnotherPartysBill()
    {
        (await Bills(Ana).GetBill(Table, Park)).Should().BeOfType<ForbidResult>();
        (await Bills(Ana).GetBillSummary(Table, Park)).Should().BeOfType<ForbidResult>();
        (await Bills(Ana).RequestBill(Table, Park)).Should().BeOfType<ForbidResult>();
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
