using System.Security.Claims;
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
using back_end.DTO.OrdersDTOs;
using back_end.DTO.bill;
using back_end.Services;

namespace back_end.Tests.Controllers;

// Order-time limits (AddOrderItems) and bill totals (GetBillSummary)
// Table: 2 adults. Menu items mirror the real menu rules.
public class OrderLimitEnforcementTests : IDisposable
{
    private const int SessionId = 500;
    private const int BillId = 600;
    private const int StaffUser = 30;

    // Item IDs
    private const int SalmonNigiri = 1;     // 1 per order (item)
    private const int SalmonSashimi = 2;    // Sashimi: 30 per order (category, combined)
    private const int TunaSashimi = 3;
    private const int HotStoneSteak = 4;    // Hot Stone: 1 per person (category), Charge at $5
    private const int ChopChop = 5;         // 2 per person (item, Block) and 1 per order
    private const int Toro = 6;             // Add-on, $1

    private readonly ApplicationDbContext _context;
    private int _nextOrderId = 900;

    public OrderLimitEnforcementTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _context.Menus.Add(new Menu { Menu_id = 1, Name = "Dinner", Description = "Dinner", Is_active = true });
        _context.Tables.Add(new TableEntity { Table_Id = 6, table_number = 6, Location_Id = 1, is_active = true });
        _context.DiningSessions.Add(new DiningSession { Session_Id = SessionId, Menu_Id = 1, Location_Id = 1, Table_Id = 6, Started_At = DateTime.UtcNow });
        _context.Bills.Add(new Billing { Bill_Id = BillId, Session_Id = SessionId, Bill_Name = "Lee", Adult_Count = 2, Total_Count = 2 });

        _context.Categories.AddRange(
            new Category { Category_id = 1, Category_name = "Nigiri", Description = "Nigiri" },
            new Category { Category_id = 2, Category_name = "Sashimi", Description = "Sashimi", per_order_limit = 30 },
            new Category
            {
                Category_id = 3,
                Category_name = "Hot Stone",
                Description = "Hot Stone",
                adult_limit = 1,
                child_limit = 1,
                senior_limit = 1,
                over_limit_behavior = OverLimitBehavior.Charge
            },
            new Category { Category_id = 4, Category_name = "Add On", Description = "Add On" });

        AddItem(SalmonNigiri, "Salmon Nigiri", categoryId: 1, a => a.Per_Order_Limit = 1);
        AddItem(SalmonSashimi, "Salmon Sashimi", categoryId: 2);
        AddItem(TunaSashimi, "Tuna Sashimi", categoryId: 2);
        AddItem(HotStoneSteak, "Hot Stone Steak", categoryId: 3, a => a.Full_Price = 5.00m);
        AddItem(ChopChop, "Chop Chop Nigiri", categoryId: 1, a =>
        {
            a.Adult_Limit = 2; a.Child_limit = 2; a.Senior_limit = 2; a.Per_Order_Limit = 1;
        });
        AddItem(Toro, "Toro Nigiri", categoryId: 4, a => { a.Is_Add_On = true; a.Price = 1.00m; a.Full_Price = 1.00m; });

        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    private void AddItem(int id, string name, int categoryId, Action<MenuItemAssignment>? configure = null)
    {
        _context.MenuItems.Add(new Menu_Item { item_id = id, Name = name, Description = name, Category_id = categoryId });
        var assignment = new MenuItemAssignment { Assignment_Id = id, Menu_Id = 1, Item_Id = id, Status = MenuItemStatus.Available };
        configure?.Invoke(assignment);
        _context.MenuItemAssignments.Add(assignment);
    }

    private int NewOrder()
    {
        var id = _nextOrderId++;
        _context.SessionOrders.Add(new SessionOrder
        {
            Order_Id = id,
            session_id = SessionId,
            Bill_Id = BillId,
            Status = OrderStatus.Pending,
            Created_At = DateTime.UtcNow
        });
        _context.SaveChanges();
        _context.ChangeTracker.Clear();
        return id;
    }

    private static ControllerContext AsStaff() => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, StaffUser.ToString()),
                new Claim(ClaimTypes.Role, "Staff"),
            }, "TestAuth")),
        },
    };

    private OrderController Orders() =>
        new(_context, new Mock<ILogger<OrderController>>().Object) { ControllerContext = AsStaff() };

    private BillController Bills()
    {
        var pricing = new Mock<IPricingService>();
        pricing.Setup(p => p.GetPricingForDate(It.IsAny<DateTime>()))
               .Returns(new PricingInfo { PricingType = "Test", TaxRate = 0m });   // base prices 0, no tax: totals are just charges
        return new BillController(_context, new Mock<IConfiguration>().Object, new Mock<ILogger<BillController>>().Object, pricing.Object)
        { ControllerContext = AsStaff() };
    }

    private async Task<IActionResult> Add(int orderId, params (int ItemId, int Quantity)[] items)
    {
        var result = await Orders().AddOrderItems(orderId,
            items.Select(i => new OrderItemCreateDTO { Order_Id = orderId, Menu_Id = 1, Item_Id = i.ItemId, Quantity = i.Quantity }).ToList());
        _context.ChangeTracker.Clear();
        return result;
    }

    private Task<List<OrderItems>> LinesFor(int orderId) =>
        _context.OrderItems.Where(oi => oi.Order_Key == orderId).OrderBy(oi => oi.Order_Item_Id).ToListAsync();

    // Validation

    [Fact]
    public async Task QuantityZero_IsRejected()
    {
        var order = NewOrder();

        (await Add(order, (SalmonNigiri, 0))).Should().BeOfType<BadRequestObjectResult>();
    }

    // Per-order limits (always block)

    [Fact]
    public async Task Nigiri_TwoInOneOrder_IsBlocked_AndNothingIsAdded()
    {
        var order = NewOrder();

        (await Add(order, (SalmonNigiri, 2), (Toro, 1))).Should().BeOfType<BadRequestObjectResult>();

        (await LinesFor(order)).Should().BeEmpty();   // all-or-nothing: Toro wasn't added either
    }

    [Fact]
    public async Task Nigiri_SecondAddToTheSameOrder_IsBlocked()
    {
        var order = NewOrder();

        (await Add(order, (SalmonNigiri, 1))).Should().BeOfType<OkObjectResult>();
        (await Add(order, (SalmonNigiri, 1))).Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Nigiri_DuplicateIdsInOneRequest_AreCombined()
    {
        var order = NewOrder();

        (await Add(order, (SalmonNigiri, 1), (SalmonNigiri, 1))).Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Nigiri_NewOrder_ResetsThePerOrderLimit()
    {
        (await Add(NewOrder(), (SalmonNigiri, 1))).Should().BeOfType<OkObjectResult>();
        (await Add(NewOrder(), (SalmonNigiri, 1))).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Sashimi_31CombinedInOneOrder_IsBlocked()
    {
        var order = NewOrder();

        (await Add(order, (SalmonSashimi, 20), (TunaSashimi, 11))).Should().BeOfType<BadRequestObjectResult>();
        (await Add(order, (SalmonSashimi, 20), (TunaSashimi, 10))).Should().BeOfType<OkObjectResult>();
    }

    // Per-person limits

    [Fact]
    public async Task HotStone_OverTheLimit_IsSplitAndCharged()
    {
        var order = NewOrder();

        (await Add(order, (HotStoneSteak, 3))).Should().BeOfType<OkObjectResult>();

        var lines = await LinesFor(order);
        lines.Should().HaveCount(2);
        lines.Should().ContainSingle(l => l.Quantity == 2 && l.Price_At_Time == 0m && l.Charge_Reason == ChargeReason.None);
        lines.Should().ContainSingle(l => l.Quantity == 1 && l.Price_At_Time == 5.00m && l.Charge_Reason == ChargeReason.OverLimit);
    }

    [Fact]
    public async Task HotStone_LimitCarriesAcrossOrders_InTheSession()
    {
        (await Add(NewOrder(), (HotStoneSteak, 2))).Should().BeOfType<OkObjectResult>();   // uses the 2 free

        var second = NewOrder();
        (await Add(second, (HotStoneSteak, 1))).Should().BeOfType<OkObjectResult>();

        (await LinesFor(second)).Should().ContainSingle(l => l.Charge_Reason == ChargeReason.OverLimit && l.Price_At_Time == 5.00m);
    }

    [Fact]
    public async Task ChopChop_FifthOne_IsBlocked()
    {
        // 2 per person x 2 adults = 4 per session, 1 per order
        for (var i = 0; i < 4; i++)
        {
            (await Add(NewOrder(), (ChopChop, 1))).Should().BeOfType<OkObjectResult>();
        }

        (await Add(NewOrder(), (ChopChop, 1))).Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CancelledOrders_DontCountTowardLimits()
    {
        var first = NewOrder();
        (await Add(first, (HotStoneSteak, 2))).Should().BeOfType<OkObjectResult>();

        var cancelled = await _context.SessionOrders.Include(o => o.OrderItems).SingleAsync(o => o.Order_Id == first);
        cancelled.Status = OrderStatus.Cancelled;
        foreach (var item in cancelled.OrderItems) item.Order_Item_Status = OrderStatus.Cancelled;
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var second = NewOrder();
        (await Add(second, (HotStoneSteak, 2))).Should().BeOfType<OkObjectResult>();

        (await LinesFor(second)).Should().OnlyContain(l => l.Charge_Reason == ChargeReason.None);   // both free again
    }

    // Bill totals

    [Fact]
    public async Task BillSummary_IncludesAddOnsAndOverLimitCharges()
    {
        var order = NewOrder();
        (await Add(order, (HotStoneSteak, 3), (Toro, 2))).Should().BeOfType<OkObjectResult>();

        var result = await Bills().GetBillSummary(SessionId, BillId);

        var summary = (result as OkObjectResult)!.Value as BillSummaryResponse;
        summary.Should().NotBeNull();
        summary!.AddOn_Subtotal.Should().Be(2.00m);       // 2 x Toro $1
        summary.OverLimit_Subtotal.Should().Be(5.00m);    // 1 extra hot stone x $5
        summary.OverLimit_Items.Should().ContainSingle(i => i.Item_Name == "Hot Stone Steak" && i.Quantity == 1);
        summary.Subtotal.Should().Be(7.00m);              // base prices are 0 in this test
    }

    [Fact]
    public async Task BillSummary_ExcludesCancelledItems()
    {
        var order = NewOrder();
        (await Add(order, (Toro, 2))).Should().BeOfType<OkObjectResult>();

        var line = await _context.OrderItems.SingleAsync(oi => oi.Order_Key == order);
        line.Order_Item_Status = OrderStatus.Cancelled;
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var summary = ((await Bills().GetBillSummary(SessionId, BillId)) as OkObjectResult)!.Value as BillSummaryResponse;

        summary!.AddOn_Subtotal.Should().Be(0m);
    }
}