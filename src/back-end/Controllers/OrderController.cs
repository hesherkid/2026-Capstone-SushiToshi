using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.Dtos;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using back_end.Helpers;
using back_end.Services;

namespace back_end.Controllers
{
    [ApiController]
    [Route("api/[controller]")]

    public class OrderController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<OrderController> _logger;
        private readonly ILocationAccessService _locationAccess;

        private const string BillRequestedMessage =
            "You've asked for this bill, so no more orders can be added to it. Please ask your server if you'd like anything else.";

        private const string BillNotOpenMessage =
            "This bill has been closed, so no more orders can be added to it. Please ask your server if you'd like anything else.";

        // A customer ordering onto a bill they aren't on (or before they've picked one)
        private const string NotYourBillMessage =
            "You can only order on your own bill. Choose your bill and try again.";

        // Once guests press "Ready for Bill" they can't add to that bill; staff still can
        private bool BillOrderingClosed(Billing? bill) =>
            bill?.Requested_At != null && !User.IsInRole("Admin") && !User.IsInRole("Staff");

        // The order's bill, looked up before taking the bill lock; null if there's no such order
        private Task<int?> OrderBillIdAsync(int orderId) =>
            _context.SessionOrders
                .Where(o => o.Order_Id == orderId)
                .Select(o => (int?)o.Bill_Id)
                .FirstOrDefaultAsync();

        public OrderController(ApplicationDbContext context, ILogger<OrderController> logger, ILocationAccessService locationAccess)
        {
            _context = context;
            _logger = logger;
            _locationAccess = locationAccess;
        }

        // Headcount for per-person limits; all non-cancelled bills in the session
        private async Task<OrderLimitRules.Headcount> SessionHeadcountAsync(int sessionId)
        {
            var bills = await _context.Bills
                .Where(b => b.Session_Id == sessionId && b.Status != BillStatus.Cancelled)
                .ToListAsync();

            var heads = new OrderLimitRules.Headcount(
                bills.Sum(b => b.Adult_Count),
                bills.Sum(b => b.Child_Count),
                bills.Sum(b => b.Senior_Count),
                bills.Sum(b => b.Tot_Count));
            return heads.IsEmpty ? OrderLimitRules.Headcount.Fallback : heads;
        }

        private sealed record UsedUnits(int OrderKey, int ItemId, int CategoryId, int Quantity);

        // Units already ordered this session (cancelled orders/items don't count)
        private Task<List<UsedUnits>> SessionUsageAsync(int sessionId) =>
            _context.OrderItems
                .Where(oi => oi.SessionOrder.session_id == sessionId
                        && oi.SessionOrder.Status != OrderStatus.Cancelled
                        && oi.Order_Item_Status != OrderStatus.Cancelled)
                .Select(oi => new UsedUnits(oi.Order_Key, oi.Item_Id, oi.MenuItem.Category_id, oi.Quantity))
                .ToListAsync();

        // Checks the items sent before anything is looked up; null when they're fine
        private static string? ItemsPayloadError(List<OrderItemCreateDto>? items)
        {
            if (items == null || !items.Any())
            {
                return "No items provided.";
            }
            if (items.Any(i => i.Quantity <= 0 || i.Quantity > 99))
            {
                return "Each quantity must be between 1 and 99.";
            }
            return null;
        }

        private sealed record LinePlan(List<OrderItems> Lines, Dictionary<int, string> NamesById, List<string> Errors);

        // Prices the requested items and applies the limits, without saving anything.
        // If any item fails, Errors is set and none of the lines should be saved (all or nothing).
        // orderId is the order being added to, or null for a new order.
        private async Task<LinePlan> PlanOrderLinesAsync(DiningSession session, int? orderId, List<OrderItemCreateDto> items)
        {
            // Combine duplicate item IDs so per-order limits can't be bypassed by splitting a request
            var requested = items
                .GroupBy(i => i.ItemId)
                .Select(g => (ItemId: g.Key, Quantity: g.Sum(i => i.Quantity)))
                .ToList();
            var itemIds = requested.Select(r => r.ItemId).ToList();

            // Effective assignments location override first, with category limits
            var menuRows = await _context.MenuItemAssignments
                .ForLocation(session.Menu_Id, session.Location_Id)
                .Where(a => itemIds.Contains(a.Item_Id))
                .Include(a => a.MenuItem)
                    .ThenInclude(mi => mi.Category)
                .ToListAsync();

            var heads = await SessionHeadcountAsync(session.Session_Id);
            var used = await SessionUsageAsync(session.Session_Id);

            var sessionItemUsed = used.GroupBy(u => u.ItemId).ToDictionary(g => g.Key, g => g.Sum(u => u.Quantity));
            var sessionCategoryUsed = used.GroupBy(u => u.CategoryId).ToDictionary(g => g.Key, g => g.Sum(u => u.Quantity));
            var orderItemUsed = used.Where(u => u.OrderKey == orderId)
                .GroupBy(u => u.ItemId).ToDictionary(g => g.Key, g => g.Sum(u => u.Quantity));
            var orderCategoryUsed = used.Where(u => u.OrderKey == orderId)
                .GroupBy(u => u.CategoryId).ToDictionary(g => g.Key, g => g.Sum(u => u.Quantity));

            var errors = new List<string>();
            var newLines = new List<OrderItems>();

            foreach (var (itemId, quantity) in requested)
            {
                var assignment = menuRows.EffectiveFor(session.Menu_Id, itemId, session.Location_Id);
                if (assignment == null || assignment.Status != MenuItemStatus.Available)
                {
                    errors.Add($"Item {itemId} is not available at this location.");
                    continue;
                }

                var category = assignment.MenuItem.Category;
                var categoryId = category.Category_id;

                var plan = OrderLimitRules.ApplyLimits(
                    itemName: assignment.MenuItem.Name,
                    categoryName: category.Category_name,
                    requested: quantity,
                    category: new OrderLimitRules.Limits(category.adult_limit, category.child_limit,
                        category.senior_limit, category.toddler_limit, category.per_order_limit, category.over_limit_behavior,
                        category.per_order_per_guest),
                    item: new OrderLimitRules.Limits(assignment.Adult_Limit, assignment.Child_limit,
                        assignment.Senior_limit, assignment.Tot_Limit, assignment.Per_Order_Limit, assignment.Over_Limit_Behavior),
                    heads: heads,
                    used: new OrderLimitRules.Usage(
                        orderCategoryUsed.GetValueOrDefault(categoryId),
                        orderItemUsed.GetValueOrDefault(itemId),
                        sessionCategoryUsed.GetValueOrDefault(categoryId),
                        sessionItemUsed.GetValueOrDefault(itemId)),
                    isAddOn: assignment.Is_Add_On,
                    price: assignment.Price);

                if (!plan.IsValid)
                {
                    errors.Add(plan.Error!);
                    continue;
                }

                foreach (var line in plan.Lines)
                {
                    newLines.Add(new OrderItems
                    {
                        Menu_Id = session.Menu_Id,
                        Item_Id = itemId,
                        Quantity = line.Quantity,
                        Price_At_Time = line.Price,          // server decides, client price is ignored
                        Charge_Reason = line.Reason,
                        Order_Item_Status = OrderStatus.Pending
                    });
                }

                // Count this requests units so later items in the same request see them
                orderItemUsed[itemId] = orderItemUsed.GetValueOrDefault(itemId) + quantity;
                orderCategoryUsed[categoryId] = orderCategoryUsed.GetValueOrDefault(categoryId) + quantity;
                sessionItemUsed[itemId] = sessionItemUsed.GetValueOrDefault(itemId) + quantity;
                sessionCategoryUsed[categoryId] = sessionCategoryUsed.GetValueOrDefault(categoryId) + quantity;
            }

            var namesById = menuRows.GroupBy(a => a.Item_Id).ToDictionary(g => g.Key, g => g.First().MenuItem.Name);
            return new LinePlan(newLines, namesById, errors);
        }

        /// <summary>
        /// How much of each menu item and category the table can still order before a limit
        /// that blocks would reject it, so the menu can stop guests before they hit an error.
        /// </summary>
        /// <param name="sessionId">The dining session</param>
        /// <remarks>
        /// Uses the same headcount and usage as adding items to an order. Per-order limits are
        /// for a new order. Limits set to Charge don't count in session_left: going over them is
        /// allowed and billed at extra_price. free_left says how many are left before that.
        /// </remarks>
        /// <response code="200">The remaining amounts; null means no blocking limit</response>
        /// <response code="401">If the user is not authenticated</response>
        /// <response code="403">If a customer asks about a table they aren't seated at</response>
        /// <response code="404">If the session doesn't exist</response>
        [Authorize]
        [HttpGet("session/{sessionId}/limits")]
        [ProducesResponseType(typeof(SessionOrderLimitsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetSessionOrderLimits(int sessionId)
        {
            if (!await SessionAccess.CanViewSessionAsync(_context, User, sessionId))
            {
                return Forbid();
            }

            var session = await _context.DiningSessions.FirstOrDefaultAsync(s => s.Session_Id == sessionId);
            if (session == null)
            {
                return NotFound(new { message = "Session was not found" });
            }

            var assignments = (await _context.MenuItemAssignments
                    .ForLocation(session.Menu_Id, session.Location_Id)
                    .Include(a => a.MenuItem)
                        .ThenInclude(mi => mi.Category)
                    .ToListAsync())
                .Effective();

            var heads = await SessionHeadcountAsync(sessionId);
            var used = await SessionUsageAsync(sessionId);
            var itemUsed = used.GroupBy(u => u.ItemId).ToDictionary(g => g.Key, g => g.Sum(u => u.Quantity));
            var categoryUsed = used.GroupBy(u => u.CategoryId).ToDictionary(g => g.Key, g => g.Sum(u => u.Quantity));

            static OrderLimitRules.Limits CategoryLimits(Category c) => new(c.adult_limit, c.child_limit,
                c.senior_limit, c.toddler_limit, c.per_order_limit, c.over_limit_behavior, c.per_order_per_guest);
            static OrderLimitRules.Limits ItemLimits(MenuItemAssignment a) => new(a.Adult_Limit, a.Child_limit,
                a.Senior_limit, a.Tot_Limit, a.Per_Order_Limit, a.Over_Limit_Behavior);

            var result = new SessionOrderLimitsDto
            {
                Items = assignments.Select(a =>
                {
                    var limits = ItemLimits(a);
                    return new ItemOrderLimitDto
                    {
                        ItemId = a.Item_Id,
                        CategoryId = a.MenuItem.Category_id,
                        PerOrder = OrderLimitRules.PerOrderCap(limits, heads),
                        SessionLeft = OrderLimitRules.SessionLeftBeforeBlock(limits, heads, itemUsed.GetValueOrDefault(a.Item_Id)),
                        FreeLeft = OrderLimitRules.SessionLeftBeforeCharge(limits, heads, itemUsed.GetValueOrDefault(a.Item_Id)),
                        ExtraPrice = a.Price,
                    };
                }).ToList(),
                Categories = assignments
                    .Select(a => a.MenuItem.Category)
                    .DistinctBy(c => c.Category_id)
                    .Select(c =>
                    {
                        var limits = CategoryLimits(c);
                        return new CategoryOrderLimitDto
                        {
                            CategoryId = c.Category_id,
                            PerOrder = OrderLimitRules.PerOrderCap(limits, heads),
                            SessionLeft = OrderLimitRules.SessionLeftBeforeBlock(limits, heads, categoryUsed.GetValueOrDefault(c.Category_id)),
                            FreeLeft = OrderLimitRules.SessionLeftBeforeCharge(limits, heads, categoryUsed.GetValueOrDefault(c.Category_id)),
                        };
                    }).ToList(),
            };

            return Ok(result);
        }

        /// <summary>
        /// Creates a new order for a dining session, with its items.
        /// </summary>
        /// <param name="orderData">The order creation data including session ID, bill ID and items</param>
        /// <remarks>
        /// Sample request:
        ///
        ///     POST /api/order
        ///     {
        ///         "session_id": 123,
        ///         "bill_id": 456,
        ///         "items": [
        ///             { "item_id": 10, "quantity": 1 },
        ///             { "item_id": 66, "quantity": 2 }
        ///         ]
        ///     }
        ///
        /// This endpoint requires authentication.
        /// The order and its items are saved together: if any item fails, nothing is saved.
        /// Items follow the same limits and pricing as POST /api/order/{orderId}/items.
        /// The order is automatically created with 'Pending' status.
        /// The authenticated user is automatically assigned as the order owner.
        /// That includes staff ordering for a table; the bill says who pays.
        /// The bill must be in 'Open' status to create an order.
        /// </remarks>
        /// <response code="200">Returns the newly created order and its items</response>
        /// <response code="400">If there are no items, an item can't be ordered, or the bill isn't open</response>
        /// <response code="401">If the caller isn't signed in</response>
        /// <response code="403">If the caller isn't staff or seated at the session's table</response>
        /// <response code="404">If the dining session is not found or the bill is not found/open</response>
        /// <response code="500">If an internal error occurs while creating the order</response>
        [HttpPost]
        [Authorize]
        [ProducesResponseType(typeof(OrderResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<OrderResponseDto>> CreateOrder(OrderCreateDto orderData)
        {
            try
            {
                var payloadError = ItemsPayloadError(orderData.Items);
                if (payloadError != null)
                {
                    return BadRequest(new { message = payloadError });
                }

                // Waits for a close of this bill in progress, so the bill read below is current
                await using var billLock = await BillLock.LockAsync(_context, orderData.BillId);

                var session = await _context.DiningSessions.FirstOrDefaultAsync(ds => ds.Session_Id == orderData.SessionId);
                if (session is null)
                {
                    return NotFound("Dinning Session was not found in the current context");
                }

                // Staff can order at any table; customers only at the table they're seated at
                if (!await SessionAccess.CanViewSessionAsync(_context, User, session.Session_Id))
                {
                    return Forbid();
                }

                var bill = await _context.Bills.FirstOrDefaultAsync(b => b.Bill_Id == orderData.BillId && b.Session_Id == session.Session_Id);
                if (bill is null)
                {
                    return NotFound("Bill Was not found for the current session order Create Bill");
                }

                // Customers only order onto the bill they're on
                if (!await SessionAccess.CanUseBillAsync(_context, User, session.Session_Id, bill.Bill_Id))
                {
                    return BadRequest(new { message = NotYourBillMessage });
                }
                if (bill.Status != BillStatus.Open)
                {
                    return BadRequest(new { message = BillNotOpenMessage });
                }

                if (BillOrderingClosed(bill))
                {
                    return BadRequest(new { message = BillRequestedMessage });
                }

                var userIdString = ClaimsHelpers.GetUserId(User);
                var userName = ClaimsHelpers.GetUserDisplayName(User);

                if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out var userId))
                {
                    return Unauthorized(new { message = "User ID not found in claims." });
                }

                // A new order has nothing ordered on it yet
                var plan = await PlanOrderLinesAsync(session, null, orderData.Items);
                if (plan.Errors.Any())
                {
                    return BadRequest(new { message = $"Nothing was added. {string.Join(" ", plan.Errors)}", errors = plan.Errors });
                }

                var now = DateTime.UtcNow;

                // First order of the session only; later orders leave it unchanged
                session.First_Order_At ??= now;

                var newOrder = new SessionOrder
                {
                    session_id = orderData.SessionId,
                    Bill_Id = orderData.BillId,
                    User_Id = userId,
                    User_Name = userName,
                    Status = OrderStatus.Pending,
                    Created_At = now,
                    OrderItems = plan.Lines
                };
                _context.SessionOrders.Add(newOrder);
                // One save, so the order, its items and First_Order_At are stored together or not at all
                await _context.SaveChangesAsync();
                if (billLock != null) await billLock.CommitAsync();

                return Ok(new OrderResponseDto
                {
                    OrderId = newOrder.Order_Id,
                    SessionId = newOrder.session_id,
                    BillId = newOrder.Bill_Id,
                    UserId = newOrder.User_Id,
                    UserName = newOrder.User_Name,
                    Status = newOrder.Status,
                    CreatedAt = newOrder.Created_At,
                    OrderItems = plan.Lines.Select(l => new OrderItemResponseDto
                    {
                        OrderItemId = l.Order_Item_Id,
                        OrderId = newOrder.Order_Id,
                        MenuId = l.Menu_Id,
                        ItemId = l.Item_Id,
                        Quantity = l.Quantity,
                        PriceAtTime = l.Price_At_Time,
                        Status = l.Order_Item_Status,
                        Name = plan.NamesById.GetValueOrDefault(l.Item_Id),
                        ChargeReason = l.Charge_Reason.ToString()
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while creating the order.");
                return StatusCode(500, new { message = "An error occurred while creating the order" });
            }
        }


        /// <summary>
        /// Approves a pending order and transitions to Approved/Processing status.
        /// </summary>
        /// <param name="orderId">The order to approve</param>
        /// <remarks>
        /// Sample request:
        ///
        ///     PATCH /api/order/123/approve
        ///
        /// Records who approved it and when. Items already cancelled stay cancelled, with their
        /// quantity set to 0.
        /// </remarks>
        /// <response code="200">The approved order and its items.</response>
        /// <response code="400">The order isn't pending, or has no items.</response>
        /// <response code="401">Not signed in.</response>
        /// <response code="403">The table is at another location, or the caller isn't staff.</response>
        /// <response code="404">The order doesn't exist.</response>
        /// <response code="500">The order couldn't be approved.</response>
        [HttpPatch("{orderId}/approve")]
        [Authorize(Policy = "staffOnly")]
        [ProducesResponseType(typeof(OrderResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ApproveOrder(int orderId)
        {
            try
            {
                var billId = await OrderBillIdAsync(orderId);
                if (billId is null)
                {
                    return NotFound("Order not found");
                }

                // Waits for items being added to this order, so they're approved along with the rest
                await using var billLock = await BillLock.LockAsync(_context, billId.Value);

                var order = await _context.SessionOrders
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.MenuItem)
                    .FirstOrDefaultAsync(o => o.Order_Id == orderId);

                if (order is null)
                {
                    return NotFound("Order not found");
                }

                if (!await SessionAccess.CanViewSessionAsync(_context, User, order.session_id))
                {
                    return LocationResults.OtherLocation();
                }
                // Use authenticated user info
                var userIdString = ClaimsHelpers.GetUserId(User);
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
                if (string.IsNullOrEmpty(userIdString))
                {
                    return Unauthorized("User ID not found in claims.");
                }

                if (order.Status != OrderStatus.Pending)
                {
                    return BadRequest("Only pending orders can be approved");
                }

                if (!order.OrderItems.Any())
                {
                    return BadRequest("Cannot approve order with no items");
                }

                order.Status = OrderStatus.Processing;
                order.Approved_At = DateTime.UtcNow;
                if (int.TryParse(userIdString, out var userId)) order.Approved_By = userId;

                foreach (var item in order.OrderItems)
                {
                    if (item.Order_Item_Status == OrderStatus.Cancelled)
                    {
                        item.Quantity = 0; // Set quantity to 0 for cancelled items
                        // Status remains Cancelled
                    }
                    else
                    {
                        item.Order_Item_Status = OrderStatus.Processing;
                    }
                }

                _context.SessionOrders.Update(order);
                await _context.SaveChangesAsync();
                if (billLock != null) await billLock.CommitAsync();

                return Ok(new OrderResponseDto
                {
                    OrderId = order.Order_Id,
                    SessionId = order.session_id,
                    BillId = order.Bill_Id,
                    UserId = order.User_Id,
                    UserName = order.User_Name,
                    Status = order.Status,
                    CreatedAt = order.Created_At,
                    OrderItems = order.OrderItems.Select(oi => new OrderItemResponseDto
                    {
                        OrderItemId = oi.Order_Item_Id,
                        OrderId = oi.Order_Key,
                        MenuId = oi.Menu_Id,
                        ItemId = oi.Item_Id,
                        Quantity = oi.Quantity,
                        PriceAtTime = oi.Price_At_Time,
                        Status = oi.Order_Item_Status,
                        Name = oi.MenuItem?.Name
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving order");
                return StatusCode(500, "Internal Server Error");
            }
        }

        /// <summary>
        /// Marks an order item as completed/delivered.
        /// </summary>
        /// <param name="orderId">The order the item is on</param>
        /// <param name="itemId">The unique identifier of the order item to complete</param>
        /// <remarks>
        /// Sample request:
        ///
        ///     PATCH /api/order/items/123/complete
        ///
        /// This endpoint requires Admin or Staff role authorization.
        /// The order item status will be updated to 'Delivered' and the completion timestamp will be set.
        /// </remarks>
        /// <response code="200">The delivered item.</response>
        /// <response code="400">The item isn't approved (Processing).</response>
        /// <response code="401">Not signed in.</response>
        /// <response code="403">The table is at another location, or the caller isn't staff.</response>
        /// <response code="404">The order doesn't exist, or the item isn't on it.</response>
        /// <response code="500">The item couldn't be updated.</response>
        [HttpPatch("{orderId}/items/{itemId}/complete")]
        [Authorize(Policy = "staffOnly")]
        [ProducesResponseType(typeof(OrderItemResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CompleteOrderItem(int orderId, int itemId)
        {
            try
            {
                var order = await _context.SessionOrders
                    .Include(o => o.OrderItems)
                    .FirstOrDefaultAsync(o => o.Order_Id == orderId);
                if (order is null) return NotFound("Order not found");

                if (!await SessionAccess.CanViewSessionAsync(_context, User, order.session_id))
                {
                    return LocationResults.OtherLocation();
                }

                var item = order.OrderItems.FirstOrDefault(oi => oi.Order_Item_Id == itemId);
                if (item is null) return NotFound("Item not found in this order");

                if (item.Order_Item_Status != OrderStatus.Processing)
                    return BadRequest("Only approved (Processing) items can be delivered");

                var now = DateTime.UtcNow;
                item.Order_Item_Status = OrderStatus.Delivered;
                item.Completed_At = now;

                // Last outstanding item delivered -> the order is done
                if (!order.OrderItems.Any(oi => oi.Order_Item_Status == OrderStatus.Processing))
                {
                    order.Status = OrderStatus.Delivered;
                    order.Completed_At = now;
                }

                await _context.SaveChangesAsync();
                var response = new OrderItemResponseDto
                {
                    OrderItemId = item.Order_Item_Id,
                    OrderId = item.Order_Key,
                    MenuId = item.Menu_Id,
                    ItemId = item.Item_Id,
                    Quantity = item.Quantity,
                    PriceAtTime = item.Price_At_Time,
                    Status = item.Order_Item_Status,
                    CompletedAt = item.Completed_At,
                };
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing order item {ItemId}", itemId);
                return StatusCode(500, "Internal Server Error");
            }
        }

        /// <summary>
        /// Marks the whole order as delivered/completed.
        /// </summary>
        /// <param name="orderId">The order to complete</param>
        /// <remarks>
        /// Sample request:
        ///
        ///     PATCH /api/order/123/complete
        ///
        /// Items already delivered or cancelled keep their status and time.
        /// </remarks>
        /// <response code="200">The completed order and its items.</response>
        /// <response code="400">The order isn't approved (Processing).</response>
        /// <response code="401">Not signed in.</response>
        /// <response code="403">The table is at another location, or the caller isn't staff.</response>
        /// <response code="404">The order doesn't exist.</response>
        /// <response code="500">The order couldn't be completed.</response>
        [HttpPatch("{orderId}/complete")]
        [Authorize(Policy = "staffOnly")]
        [ProducesResponseType(typeof(OrderResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CompleteOrder(int orderId)
        {
            try
            {
                var order = await _context.SessionOrders
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.MenuItem)
                    .FirstOrDefaultAsync(o => o.Order_Id == orderId);

                if (order is null)
                {
                    return NotFound("Order not found");
                }

                if (!await SessionAccess.CanViewSessionAsync(_context, User, order.session_id))
                {
                    return LocationResults.OtherLocation();
                }

                if (order.Status != OrderStatus.Processing)
                {
                    return BadRequest("Only approved orders can be marked as completed");
                }

                var now = DateTime.UtcNow;

                // Only items still outstanding
                foreach (var item in order.OrderItems.Where(i => i.Order_Item_Status == OrderStatus.Processing))
                {
                    item.Order_Item_Status = OrderStatus.Delivered;
                    item.Completed_At = now;
                }

                order.Status = OrderStatus.Delivered;
                order.Completed_At = now;

                _context.SessionOrders.Update(order);
                await _context.SaveChangesAsync();

                return Ok(new OrderResponseDto
                {
                    OrderId = order.Order_Id,
                    SessionId = order.session_id,
                    BillId = order.Bill_Id,
                    UserId = order.User_Id,
                    Status = order.Status,
                    CreatedAt = order.Created_At,
                    CompletedAt = order.Completed_At,
                    OrderItems = order.OrderItems.Select(oi => new OrderItemResponseDto
                    {
                        OrderItemId = oi.Order_Item_Id,
                        OrderId = oi.Order_Key,
                        MenuId = oi.Menu_Id,
                        ItemId = oi.Item_Id,
                        Quantity = oi.Quantity,
                        PriceAtTime = oi.Price_At_Time,
                        Status = oi.Order_Item_Status,
                        CompletedAt = oi.Completed_At,
                        Name = oi.MenuItem?.Name
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing order");
                return StatusCode(500, "Internal Server Error");
            }
        }

        /// <summary>
        /// Cancels a pending order.
        /// </summary>
        /// <param name="orderId">The unique identifier of the order to cancel</param>
        /// <remarks>
        /// Sample request:
        ///
        ///     PATCH /api/order/123/cancel
        ///
        /// This endpoint requires authentication.
        /// Only orders with 'Pending' status can be cancelled.
        /// Orders that are approved or delivered cannot be cancelled.
        /// Staff can cancel any order at their location. Customers (guests included) can cancel
        /// orders they placed, while they're seated at that table.
        /// </remarks>
        /// <response code="200">Returns the cancelled order</response>
        /// <response code="400">If the order has already been delivered and cannot be cancelled</response>
        /// <response code="401">Not signed in.</response>
        /// <response code="403">Staff at another location, or a customer cancelling an order they didn't
        /// place or at a table they've left.</response>
        /// <response code="404">If the order is not found</response>
        /// <response code="409">If the order is not in pending status</response>
        /// <response code="500">If an internal error occurs while cancelling the order</response>
        [HttpPatch("{orderId}/cancel")]
        [Authorize]
        [ProducesResponseType(typeof(OrderResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CancelOrder(
            int orderId
        )
        {
            try
            {
                var order = await _context.SessionOrders.Include(o => o.OrderItems).FirstOrDefaultAsync(so => so.Order_Id == orderId);
                if (order is null)
                {
                    return NotFound("Order was not found to cancel");
                }

                var isStaff = User.IsInRole("Admin") || User.IsInRole("Staff");
                if (isStaff)
                {
                    if (!await SessionAccess.CanViewSessionAsync(_context, User, order.session_id))
                    {
                        return LocationResults.OtherLocation();
                    }
                }
                // Customers only their own orders, and only while seated at that table
                else if (order.User_Id?.ToString() != ClaimsHelpers.GetUserId(User)
                         || !await SessionAccess.CanViewSessionAsync(_context, User, order.session_id))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, "You can only cancel your own orders");
                }
                if (order.Status == OrderStatus.Delivered)
                {
                    return BadRequest("Cannot cancel a delivered order");
                }
                if (order.Status != OrderStatus.Pending)
                {
                    return Conflict("Can only cancel pending orders");
                }
                order.Status = OrderStatus.Cancelled;
                order.Completed_At = DateTime.UtcNow;
                foreach (var item in order.OrderItems)
                {
                    item.Order_Item_Status = OrderStatus.Cancelled;
                    item.Completed_At = DateTime.UtcNow;
                }

                _context.SessionOrders.Update(order);
                await _context.SaveChangesAsync();
                return Ok(new OrderResponseDto
                {
                    OrderId = order.Order_Id,
                    SessionId = order.session_id,
                    BillId = order.Bill_Id,
                    UserId = order.User_Id,
                    UserName = order.User_Name,
                    Status = order.Status,
                    CreatedAt = order.Created_At,
                    CompletedAt = order.Completed_At,
                    OrderItems = order.OrderItems.Select(oi => new OrderItemResponseDto
                    {
                        OrderItemId = oi.Order_Item_Id,
                        OrderId = oi.Order_Key,
                        MenuId = oi.Menu_Id,
                        ItemId = oi.Item_Id,
                        Quantity = oi.Quantity,
                        PriceAtTime = oi.Price_At_Time,
                        Status = oi.Order_Item_Status,
                        CompletedAt = oi.Completed_At,
                        Name = oi.MenuItem?.Name
                    }).ToList()
                });

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Orders");
                return StatusCode(500, "Internal Server Error");
            }
        }

        /// <summary>
        /// Adds items to an order, applying category and item limits.
        /// </summary>
        /// <param name="orderId">The order to add items to (from POST api/Order)</param>
        /// <param name="items">The list of items to add to the order. Duplicate item IDs are combined</param>
        /// <remarks>
        /// Sample request:
        ///
        ///     POST /api/order/123/items
        ///     [
        ///         { "item_id": 10, "quantity": 1 },
        ///         { "item_id": 66, "quantity": 2 }
        ///     ]
        ///
        /// Limits (category and item both apply; 0 = unlimited, toddler 0 = same as child):
        /// - Per order (whole table, resets each order): always blocks. 
        ///     A per guest cap is the limit * everyone on the session's bills.
        /// - Per person (per dining session, limit x headcount from the session's bills):
        ///   Block rejects the request; Charge adds the excess as a separate line at full price.
        /// Cancelled orders and cancelled items don't count toward limits.
        /// The request is all-or-nothing: if any item fails, nothing is added.
        /// Prices are set by the server.
        /// Staff edits to pending orders (PATCH) are not limit-checked, so staff can override.
        /// Guests can't add items once the bill has been requested ("Ready for Bill"); staff can.
        /// </remarks>
        /// <response code="200">Items added; response lists each line and any over-limit charges</response>
        /// <response code="400">Invalid request or a limit was exceeded; errors lists every problem</response>
        /// <response code="401">If the caller isn't signed in</response>
        /// <response code="403">If the caller isn't staff or seated at the order's table</response>
        /// <response code="404">If the order is not found</response>
        /// <response code="500">If an internal error occurs while adding items</response>
        [HttpPost("{orderId}/items")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AddOrderItems(int orderId, [FromBody] List<OrderItemCreateDto> items)
        {
            try
            {
                var payloadError = ItemsPayloadError(items);
                if (payloadError != null)
                {
                    return BadRequest(new { message = payloadError });
                }

                var billId = await OrderBillIdAsync(orderId);
                if (billId is null)
                {
                    return NotFound(new { message = $"Order {orderId} not found" });
                }

                // Waits for an approval or a bill close in progress, so the status checks below are current
                await using var billLock = await BillLock.LockAsync(_context, billId.Value);

                var order = await _context.SessionOrders
                    .Include(o => o.OrderItems)
                    .Include(o => o.DiningSession)
                    .Include(o => o.Bill)
                    .FirstOrDefaultAsync(o => o.Order_Id == orderId);

                if (order == null)
                {
                    return NotFound(new { message = $"Order {orderId} not found" });
                }

                // Staff can add to any order; customers only to orders on the bill they're on
                if (!await SessionAccess.CanViewSessionAsync(_context, User, order.session_id))
                {
                    return Forbid();
                }
                if (!await SessionAccess.CanUseBillAsync(_context, User, order.session_id, order.Bill_Id))
                {
                    return BadRequest(new { message = NotYourBillMessage });
                }

                // Guests can't add to a bill after pressing "Ready for Bill", but staff can
                if (order.Bill?.Status != BillStatus.Open)
                {
                    return BadRequest(new { message = BillNotOpenMessage });
                }
                if (BillOrderingClosed(order.Bill))
                {
                    return BadRequest(new { message = BillRequestedMessage });
                }
                if (order.Status != OrderStatus.Pending)
                {
                    return BadRequest(new { message = "Items can only be added to pending orders." });
                }

                // Signed-in user (guests have a sign-in too), returned in the response
                var userName = ClaimsHelpers.GetUserDisplayName(User);
                int? userId = int.TryParse(ClaimsHelpers.GetUserId(User), out var parsedUserId) ? parsedUserId : null;

                var plan = await PlanOrderLinesAsync(order.DiningSession, orderId, items);
                if (plan.Errors.Any())
                {
                    return BadRequest(new { message = $"Nothing was added. {string.Join(" ", plan.Errors)}", errors = plan.Errors });
                }

                var newLines = plan.Lines;
                foreach (var line in newLines)
                {
                    order.OrderItems.Add(line);
                }
                await _context.SaveChangesAsync();
                if (billLock != null) await billLock.CommitAsync();

                var namesById = plan.NamesById;
                var overLimit = newLines.Where(l => l.Charge_Reason == ChargeReason.OverLimit).ToList();

                return Ok(new
                {
                    message = overLimit.Any()
                        ? "Items added. Some items are over the limit and will be charged."
                        : "Items added successfully",
                    count = newLines.Count,
                    order_id = orderId,
                    user_id = userId,
                    user_name = userName,
                    lines = newLines.Select(l => new
                    {
                        order_item_id = l.Order_Item_Id,
                        item_id = l.Item_Id,
                        name = namesById.GetValueOrDefault(l.Item_Id),
                        quantity = l.Quantity,
                        price_at_time = l.Price_At_Time,
                        charge_reason = l.Charge_Reason.ToString()
                    }),
                    over_limit_total = overLimit.Sum(l => l.Quantity * l.Price_At_Time)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding items to order {OrderId}", orderId);
                return StatusCode(500, new { message = "Error adding order items" });
            }
        }

        /// <summary>
        /// Updates order items before approval (pending orders only).
        /// </summary>
        /// <param name="orderId">The order the item is on</param>
        /// <param name="itemId">The order item (order_item_id) to change</param>
        /// <param name="updateData">The fields to change; leave out the ones to keep</param>
        /// <remarks>
        /// Sample request:
        ///
        ///     PATCH /api/order/123/items/456
        ///     { "quantity": 3, "price_at_time": 4.50 }
        ///
        /// - quantity: 1–99, or 0 for an item that's already cancelled.
        /// - price_at_time: 0 or more.
        /// - status: an order status name (e.g. Processing, Cancelled). Delivered and Cancelled set the
        ///   completion time; moving back to Pending or Processing clears it.
        /// - Not limit-checked, so staff can override limits.
        /// </remarks>
        /// <response code="200">The updated item.</response>
        /// <response code="400">The order isn't pending, or a quantity, price or status isn't valid.</response>
        /// <response code="401">Not signed in.</response>
        /// <response code="403">The table is at another location, or the caller isn't staff.</response>
        /// <response code="404">The order doesn't exist, or the item isn't on it.</response>
        /// <response code="500">The item couldn't be updated.</response>
        [HttpPatch("{orderId}/items/{itemId}")]
        [Authorize(Policy = "staffOnly")]
        [ProducesResponseType(typeof(OrderItemResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateOrderItem(
                   int orderId,
                   int itemId,
                   [FromBody] OrderItemUpdateDto updateData
               )
        {
            try
            {
                var order = await _context.SessionOrders
                    .FirstOrDefaultAsync(o => o.Order_Id == orderId);

                if (order is null)
                {
                    return NotFound("Order not found");
                }

                if (!await SessionAccess.CanViewSessionAsync(_context, User, order.session_id))
                {
                    return LocationResults.OtherLocation();
                }

                // Use authenticated user info
                var userIdString = ClaimsHelpers.GetUserId(User);
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

                if (string.IsNullOrEmpty(userIdString))
                {
                    return Unauthorized("User ID not found in claims.");
                }

                if (!int.TryParse(userIdString, out var userId))
                {
                    return Unauthorized("Invalid User ID format.");
                }

                // Check if user is the owner of the order
                bool isOwner = order.User_Id == userId;

                // Check if user is staff/admin
                bool isStaff = userRole.Contains("Staff") || userRole.Contains("Admin");

                if (!isOwner && !isStaff)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = "You don't have permission to modify this order" });
                }

                // Can only modify pending orders
                if (order.Status != OrderStatus.Pending)
                {
                    return BadRequest("Can only modify pending orders");
                }

                var orderItem = await _context.OrderItems
                    .Include(oi => oi.MenuItem)
                    .FirstOrDefaultAsync(oi => oi.Order_Item_Id == itemId && oi.Order_Key == orderId);

                if (orderItem is null)
                {
                    return NotFound("Order item not found");
                }

                // Update quantity if provided
                if (updateData.Quantity.HasValue)
                {
                    // Allow quantity 0 only for cancelled items
                    if (updateData.Quantity.Value < 0)
                    {
                        return BadRequest("Quantity cannot be negative");
                    }

                    if (updateData.Quantity.Value == 0)
                    {
                        // Only allow 0 quantity for cancelled items
                        if (orderItem.Order_Item_Status != OrderStatus.Cancelled)
                        {
                            return BadRequest("Quantity can only be 0 for cancelled items");
                        }
                    }
                    else
                    {
                        // For non-zero quantities, enforce positive and max limits
                        if (updateData.Quantity.Value > 99)
                        {
                            return BadRequest("Quantity cannot exceed 99");
                        }
                    }

                    orderItem.Quantity = updateData.Quantity.Value;
                }

                // Update price if provided (staff only)
                if (updateData.PriceAtTime.HasValue)
                {
                    if (!isStaff)
                    {
                        return StatusCode(StatusCodes.Status403Forbidden, new { message = "Only staff can modify prices" });
                    }
                    if (updateData.PriceAtTime.Value < 0)
                    {
                        return BadRequest("Price cannot be negative");
                    }
                    orderItem.Price_At_Time = updateData.PriceAtTime.Value;
                }

                // Update status if provided
                if (!string.IsNullOrEmpty(updateData.Status))
                {
                    if (!isStaff)
                    {
                        return StatusCode(StatusCodes.Status403Forbidden, new { message = "Only staff can modify order item status" });
                    }
                    if (!Enum.TryParse<OrderStatus>(updateData.Status, true, out var newStatus))
                    {
                        return BadRequest("Invalid status value");
                    }
                    // only set Completed_At when the status actually changes, so re-sending the same status doesn't reset the timestamp
                    var statusChanged = orderItem.Order_Item_Status != newStatus;
                    orderItem.Order_Item_Status = newStatus;
                    if (statusChanged)
                    {
                        // Completed_At = when the item was finished, either delivered or cancelled.
                        // Analytics filter on Order_Item_Status, so cancelled items never count as sales or timings.
                        if (newStatus == OrderStatus.Cancelled || newStatus == OrderStatus.Delivered)
                        {
                            orderItem.Completed_At = DateTime.UtcNow;
                        }
                        else
                        {
                            orderItem.Completed_At = null;   // reopened (Pending/Processing): not finished yet
                        }
                    }
                }

                await _context.SaveChangesAsync();

                return Ok(new OrderItemResponseDto
                {
                    OrderItemId = orderItem.Order_Item_Id,
                    OrderId = orderItem.Order_Key,
                    MenuId = orderItem.Menu_Id,
                    ItemId = orderItem.Item_Id,
                    Quantity = orderItem.Quantity,
                    PriceAtTime = orderItem.Price_At_Time,
                    Status = orderItem.Order_Item_Status,
                    CompletedAt = orderItem.Completed_At,
                    Name = orderItem.MenuItem?.Name
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating order item");
                return StatusCode(500, "Internal Server Error");
            }
        }

        /// <summary>
        /// Retrieves all orders, optionally filtered by location and delivered open sessions.
        /// </summary>
        /// <param name="locationId">Optional location filter</param>
        /// <param name="deliveredOpenSessionsOnly"> When true, delivered orders are only returned if the dining session is still open(Ended_At = null) Pending and Processing orders are always returned</param>
        /// <remarks>
        /// Sample request:
        ///
        ///     GET /api/order/all-orders?location_id=1&amp;delivered_open_sessions_only=true
        ///
        /// Returns all orders filtered by the specified location and delivered open sessions.
        /// </remarks>
        /// <response code="200">The orders (can be empty).</response>
        /// <response code="401">Not signed in.</response>
        /// <response code="403">Not staff, or a staff member with no working location.</response>
        /// <response code="500">The orders couldn't be loaded.</response>
        [HttpGet("all-orders")]
        [Authorize(Policy = "staffOnly")]
        [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<object>>> GetAllOrdersByLocation([FromQuery(Name = "location_id")] int? locationId = null, [FromQuery(Name = "delivered_open_sessions_only")] bool deliveredOpenSessionsOnly = false)
        {
            try
            {
                var scope = await _locationAccess.ResolveLocationAsync(User, locationId);
                if (scope.IsForbidden) return LocationResults.NoLocation();
                locationId = scope.LocationId;

                // Orders with no items have nothing for staff to do (e.g. left empty before orders were saved with their items)
                var query = StaffOrdersQuery().Where(o => o.OrderItems.Any());

                // Filter by location if provided
                if (locationId.HasValue)
                {
                    query = query.Where(o => o.DiningSession.Location_Id == locationId.Value);
                }

                if (deliveredOpenSessionsOnly)
                {
                    query = query.Where(o => o.Status != OrderStatus.Delivered || o.DiningSession.Ended_At == null);
                }

                var orders = await query
                    .OrderByDescending(o => o.Created_At)
                    .ToListAsync();

                var result = orders.Select(ToStaffOrder).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving staff orders");
                return StatusCode(500, "Internal Server Error");
            }
        }

        // Longest range staff can search in Order History
        public const int MaxHistoryRangeDays = 31;

        /// <summary>
        /// Looks up past orders by date range.
        /// </summary>
        /// <param name="from">First restaurant-local day to include (yyyy-MM-dd)</param>
        /// <param name="to">Last restaurant-local day to include (yyyy-MM-dd), not in the future</param>
        /// <param name="locationId">Optional location filter. Staff always get their current location</param>
        /// <param name="tableNumber">Optional table number. Matches single tables and tables in a group</param>
        /// <param name="name">Optional guest or bill name. Partial, case-insensitive match</param>
        /// <remarks>
        /// Sample request:
        ///
        ///     GET /api/order/history?from=2026-10-20&amp;to=2026-10-26&amp;table_number=14&amp;name=lee
        ///
        /// </remarks>
        /// <response code="200">The orders placed in the range (can be empty).</response>
        /// <response code="400">from or to is missing or invalid, to is in the future, or the range is over 31 days.</response>
        /// <response code="401">Not signed in.</response>
        /// <response code="403">Not staff, or a staff member with no working location.</response>
        /// <response code="500">The orders couldn't be loaded.</response>
        [HttpGet("history")]
        [Authorize(Policy = "staffOnly")]
        [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<object>>> GetOrderHistory(
            [FromQuery] string? from,
            [FromQuery] string? to,
            [FromQuery(Name = "location_id")] int? locationId = null,
            [FromQuery(Name = "table_number")] int? tableNumber = null,
            [FromQuery] string? name = null)
        {
            try
            {
                var scope = await _locationAccess.ResolveLocationAsync(User, locationId);
                if (scope.IsForbidden) return LocationResults.NoLocation();
                locationId = scope.LocationId;

                if (!AnalyticsDateRange.TryResolve("custom", from, to, DateTime.UtcNow,
                        AnalyticsDateRange.RestaurantTimeZone, out var range, out var errorKey, out var errorMessage)
                    || range == null)
                {
                    return HistoryBadRequest(errorKey, errorMessage);
                }

                if (range.ToLocal.DayNumber - range.FromLocal.DayNumber + 1 > MaxHistoryRangeDays)
                {
                    return HistoryBadRequest("to", $"Choose a range of {MaxHistoryRangeDays} days or fewer.");
                }

                var query = StaffOrdersQuery()
                    .Where(o => o.Created_At >= range.StartUtc && o.Created_At < range.EndUtc);

                if (locationId.HasValue)
                {
                    query = query.Where(o => o.DiningSession.Location_Id == locationId.Value);
                }

                if (tableNumber.HasValue)
                {
                    var number = tableNumber.Value;
                    query = query.Where(o =>
                        (o.DiningSession.Table != null && o.DiningSession.Table.table_number == number) ||
                        (o.DiningSession.TableGroup != null && o.DiningSession.TableGroup.Tables.Any(t => t.table_number == number)));
                }

                if (!string.IsNullOrWhiteSpace(name))
                {
                    var term = name.Trim().ToLower();
                    query = query.Where(o =>
                        o.Bill.Bill_Name.ToLower().Contains(term) ||
                        (o.User_Name != null && o.User_Name.ToLower().Contains(term)));
                }

                var orders = await query
                    .OrderByDescending(o => o.Created_At)
                    .ToListAsync();

                return Ok(orders.Select(ToStaffOrder).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving order history");
                return StatusCode(500, "Internal Server Error");
            }
        }

        private BadRequestObjectResult HistoryBadRequest(string key, string message)
        {
            ModelState.AddModelError(key, message);
            return BadRequest(new ValidationProblemDetails(ModelState));
        }

        // Orders with everything the staff order views need. Shared by all-orders and history
        private IQueryable<SessionOrder> StaffOrdersQuery() =>
            _context.SessionOrders
                .Include(o => o.DiningSession)
                    .ThenInclude(ds => ds.Menu)
                .Include(o => o.DiningSession)
                    .ThenInclude(ds => ds.Table)
                .Include(o => o.DiningSession)
                    .ThenInclude(ds => ds.TableGroup)
                        .ThenInclude(tg => tg!.Tables)
                .Include(o => o.Bill)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuItem)
                        .ThenInclude(mi => mi.MenuAssignments)
                .AsQueryable();

        // The staff order shape, shared by all-orders and history so the two can't drift apart
        private static object ToStaffOrder(SessionOrder o) => new
        {
            order_id = o.Order_Id,
            session_id = o.session_id,
            bill_id = o.Bill_Id,
            bill_name = o.Bill.Bill_Name,
            location_id = o.DiningSession.Location_Id,
            table_numbers = o.DiningSession.Table_Id.HasValue
                ? new[] { o.DiningSession.Table!.table_number }
                : o.DiningSession.TableGroup?.Tables?.Select(t => t.table_number).ToArray() ?? Array.Empty<int>(),
            user_name = o.User_Name,
            status = o.Status.ToString(),
            created_at = o.Created_At,
            completed_at = o.Completed_At,
            items = o.OrderItems.Select(oi =>
            {
                var assignment = oi
                    .MenuItem
                    .MenuAssignments
                    .EffectiveFor(oi.Menu_Id, oi.Item_Id, o.DiningSession.Location_Id);
                bool isAddOn = assignment?.Is_Add_On ?? false;

                return new
                {
                    order_item_id = oi.Order_Item_Id,
                    item_id = oi.Item_Id,
                    name = oi.MenuItem.Name,
                    quantity = oi.Quantity,
                    price_at_time = isAddOn || oi.Charge_Reason == ChargeReason.OverLimit ? oi.Price_At_Time : 0,
                    is_add_on = isAddOn,
                    charge_reason = oi.Charge_Reason.ToString(),
                    status = oi.Order_Item_Status.ToString()
                };
            }).ToList()
        };

        /// <summary>
        /// One order with its items and totals.
        /// </summary>
        /// <param name="id">The order</param>
        /// <remarks>
        /// Sample request:
        ///
        ///     GET /api/order/789
        ///
        /// Includes who placed it (user_id, user_name), each item with its price and line total,
        /// and the order total.
        /// </remarks>
        /// <response code="200">The order.</response>
        /// <response code="401">Not signed in.</response>
        /// <response code="403">A customer asking about an order on someone else's bill.</response>
        /// <response code="404">The order doesn't exist.</response>
        /// <response code="500">The order couldn't be loaded.</response>
        [HttpGet("{id}")]
        [Authorize]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<object>> GetOrderById(int id)
        {
            try
            {
                //Get the order from the inputted Id
                //Get the user, session, bill and ordered items
                var order = await _context.SessionOrders
                            .Where(o => o.Order_Id == id)
                            // .Include(o => o.User) // Removed for Oauth
                            .Include(o => o.DiningSession)
                            .Include(o => o.Bill)
                            .Include(o => o.OrderItems)
                                .ThenInclude(od => od.MenuItem)
                            .FirstOrDefaultAsync();

                //Check if the order is found
                if (order == null)
                {
                    return NotFound(new { message = $"Can't find Order with ID: {id}" });
                }

                // Staff see any order; customers only orders on the bill they're on
                if (!await SessionAccess.CanUseBillAsync(_context, User, order.session_id, order.Bill_Id))
                {
                    return Forbid();
                }

                //Create object to store information about the order
                //Add the customers information
                //List all the items and calculate the cost of each item
                //Calculate the cost of every item together
                var orderInfo = new
                {
                    order_id = order.Order_Id,
                    session_id = order.session_id,
                    bill_id = order.Bill_Id,
                    status = order.Status,
                    created_at = order.Created_At,
                    completed_at = order.Completed_At,

                    customer = new
                    {
                        user_id = order.User_Id,
                        user_name = order.User_Name
                    },

                    items = order.OrderItems.Select(o => new
                    {
                        item_id = o.Item_Id,
                        item_name = o.MenuItem.Name,
                        quantity = o.Quantity,
                        price_at_time = o.Price_At_Time,
                        item_status = o.Order_Item_Status,
                        item_total = o.Quantity * o.Price_At_Time
                    }).ToList(),

                    order_total = order.OrderItems.Select(i => i.Quantity * i.Price_At_Time).Sum()
                };

                return Ok(orderInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving order {OrderId}", id);
                return StatusCode(500, new { message = "An error occurred while retrieving the order" });
            }
        }

        /// <summary>
        /// The orders at a dining session, oldest first.
        /// </summary>
        /// <param name="sessionId">The dining session</param>
        /// <param name="billId">Optional bill filter for staff. Customers always get their own bill</param>
        /// <remarks>
        /// Sample request:
        ///
        ///     GET /api/order/session/456?bill_id=12
        ///
        /// - Staff see every order at the table. Customers see every order on their own bill,
        ///   including ones others placed for them, or nothing if they aren't on a bill yet.
        /// - item_total is the number of lines on the order; order_total counts only add-ons
        ///   and over-limit charges.
        /// </remarks>
        /// <response code="200">The orders (can be empty).</response>
        /// <response code="401">Not signed in.</response>
        /// <response code="403">Not seated at this table, staff at another location, or a customer asking
        /// for another bill.</response>
        /// <response code="404">The session doesn't exist.</response>
        /// <response code="500">The orders couldn't be loaded.</response>
        [HttpGet("session/{sessionId}")]
        [Authorize]
        [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<object>>> GetOrderBySession(int sessionId, [FromQuery(Name = "bill_id")] int? billId = null)
        {
            try
            {
                var session = await _context.DiningSessions.FindAsync(sessionId);
                if (session == null)
                {
                    return NotFound(new { message = $"Can't find session with the ID: {sessionId}" });
                }

                // Staff see any table; customers see every order on their own bill,
                // so when one person orders for their party the rest of it can see it too
                if (!await SessionAccess.CanViewSessionAsync(_context, User, sessionId))
                {
                    return Forbid();
                }
                if (!SessionAccess.IsStaff(User))
                {
                    var myBillId = await SessionAccess.MyBillIdAsync(_context, User, sessionId);
                    if (billId.HasValue && billId != myBillId)
                    {
                        return Forbid();
                    }
                    billId = myBillId ?? -1; // Not on a bill yet: nothing to show
                }

                var sessionOrder = await _context.SessionOrders
                    .Where(o => o.session_id == sessionId &&
                                (!billId.HasValue || o.Bill_Id == billId.Value))
                    .Include(o => o.Bill)
                    .Include(o => o.OrderItems)
                        .ThenInclude(or => or.MenuItem)
                            .ThenInclude(mi => mi.MenuAssignments)
                    .OrderBy(o => o.Created_At)
                    .ToListAsync();

                var orderAll = sessionOrder.Select(o => new
                {
                    order_id = o.Order_Id,
                    bill_id = o.Bill_Id,
                    bill_name = o.Bill?.Bill_Name,
                    bill_status = o.Bill?.Status.ToString(),
                    session_id = o.session_id,
                    user_id = o.User_Id,
                    user_name = o.User_Name,
                    status = o.Status,
                    item_total = o.OrderItems.Count,
                    order_total = o.OrderItems.Sum(or =>
                    {
                        // Get menu assignment for this item
                        var assignment = or
                            .MenuItem?
                            .MenuAssignments
                            .EffectiveFor(or.Menu_Id, or.Item_Id, session.Location_Id);

                        // Only count price if it's an add-on item
                        bool isAddOn = assignment?.Is_Add_On ?? false;
                        return isAddOn || or.Charge_Reason == ChargeReason.OverLimit ? (or.Quantity * or.Price_At_Time) : 0;
                    }),
                    created_at = o.Created_At,
                    completed_at = o.Completed_At,
                    // List of items in the order
                    items = o.OrderItems.Select(oi =>
                    {
                        // Get menu assignment for this specific item
                        var assignment = oi
                            .MenuItem?
                            .MenuAssignments
                            .EffectiveFor(oi.Menu_Id, oi.Item_Id, session.Location_Id);

                        bool isAddOn = assignment?.Is_Add_On ?? false;


                        return new
                        {
                            order_item_id = oi.Order_Item_Id,
                            item_id = oi.Item_Id,
                            name = oi.MenuItem?.Name,
                            quantity = oi.Quantity,
                            price_at_time = isAddOn || oi.Charge_Reason == ChargeReason.OverLimit ? oi.Price_At_Time : 0, // Only show price for add-ons
                            is_add_on = isAddOn,
                            charge_reason = oi.Charge_Reason.ToString(),
                            status = oi.Order_Item_Status
                        };
                    }).ToList()
                }).ToList();

                return Ok(orderAll);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving orders for session {SessionId}", sessionId);
                return StatusCode(500, new { message = "An error occurred while retrieving session orders" });
            }
        }

        /// <summary>
        /// The signed-in user's own orders at the table they're seated at now, newest first.
        /// </summary>
        /// <param name="billId">Optional: only orders on this bill</param>
        /// <remarks>
        /// Sample request:
        ///
        ///     GET /api/order/active-session/orders?bill_id=12
        ///
        /// Only orders this user placed; for everything on their bill use GET /api/order/session/{sessionId}.
        /// price_at_time is 0 except on add-ons and over-limit lines.
        /// </remarks>
        /// <response code="200">The orders (can be empty).</response>
        /// <response code="401">Not signed in.</response>
        /// <response code="404">The user isn't seated at an open table.</response>
        /// <response code="500">The orders couldn't be loaded.</response>
        [HttpGet("active-session/orders")]
        [Authorize]
        [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<object>>> GetOrdersForActiveSession([FromQuery(Name = "bill_id")] int? billId = null)
        {

            try
            {
                //Get the currently logged in user's ID
                var userIdString = ClaimsHelpers.GetUserId(User);
                if (string.IsNullOrWhiteSpace(userIdString) || !int.TryParse(userIdString, out var userId))
                {
                    return Unauthorized(new { message = $"Can't find valid user ID in claims." });
                }

                //Get the user's session that's currently active
                var activeSessionId = await (
                    from p in _context.SessionParticipants
                    join ds in _context.DiningSessions on p.Session_Id equals ds.Session_Id
                    where p.User_Id == userId
                    && p.Left_At == null
                    && ds.Ended_At == null
                    orderby ds.Started_At descending
                    select ds.Session_Id
                ).FirstOrDefaultAsync();

                if (activeSessionId == 0)
                {
                    return NotFound("No active session for this user");
                }

                var orders = await _context.SessionOrders
                                .Where(o => o.User_Id == userId && o.session_id == activeSessionId && (billId == null || o.Bill_Id == billId))
                                .Include(o => o.DiningSession)
                                    .ThenInclude(or => or.Menu)
                                .Include(o => o.Bill)
                                .Include(o => o.OrderItems)
                                    .ThenInclude(od => od.MenuItem)
                                        .ThenInclude(mi => mi.MenuAssignments)
                                .OrderByDescending(o => o.Created_At)
                                .ToListAsync();

                //Package up the user's Order and add in all the menu item's details
                var userOrder = orders.Select(o => new
                {
                    order_id = o.Order_Id,
                    session_id = o.session_id,
                    bill_id = o.Bill_Id,
                    bill_status = o.Bill?.Status.ToString(),
                    status = o.Status.ToString(),
                    created_at = o.Created_At,
                    completed_at = o.Completed_At,
                    items = o.OrderItems.Select(i =>
                    {
                        // Get menu assignment for this item at this session's location
                        var assignment = i
                            .MenuItem?
                            .MenuAssignments
                            .EffectiveFor(i.Menu_Id, i.Item_Id, o.DiningSession.Location_Id);

                        bool isAddOn = assignment?.Is_Add_On ?? false;

                        return new
                        {
                            order_item_id = i.Order_Item_Id,
                            item_id = i.Item_Id,
                            name = i.MenuItem?.Name,
                            quantity = i.Quantity,
                            price_at_time = isAddOn || i.Charge_Reason == ChargeReason.OverLimit ? i.Price_At_Time : 0, // Only show price for add-ons
                            is_add_on = isAddOn,
                            charge_reason = i.Charge_Reason.ToString(),
                            status = i.Order_Item_Status.ToString()
                        };
                    }).ToList()
                });

                return Ok(userOrder);
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, "Can't get user's active orders");
                return StatusCode(500, "Internal Server Error");
            }
        }
    }

}