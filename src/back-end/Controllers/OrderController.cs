using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.DTO.OrdersDTOs;
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

        // Once guests press "Ready for Bill" they can't add to that bill; staff still can
        private bool BillOrderingClosed(Billing? bill) =>
            bill?.Requested_At != null && !User.IsInRole("Admin") && !User.IsInRole("Staff");

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

        private sealed record UsedUnits(int Order_Key, int Item_Id, int Category_id, int Quantity);

        // Units already ordered this session (cancelled orders/items don't count)
        private Task<List<UsedUnits>> SessionUsageAsync(int sessionId) =>
            _context.OrderItems
                .Where(oi => oi.SessionOrder.session_id == sessionId
                        && oi.SessionOrder.Status != OrderStatus.Cancelled
                        && oi.Order_Item_Status != OrderStatus.Cancelled)
                .Select(oi => new UsedUnits(oi.Order_Key, oi.Item_Id, oi.MenuItem.Category_id, oi.Quantity))
                .ToListAsync();

        /// <summary>
        /// How much of each menu item and category the table can still order before a limit
        /// that blocks would reject it, so the menu can stop guests before they hit an error.
        /// </summary>
        /// <param name="sessionId">The dining session</param>
        /// <response code="200">The remaining amounts; null means no blocking limit</response>
        /// <response code="403">If a customer asks about a table they aren't seated at</response>
        /// <response code="404">If the session doesn't exist</response>
        /// <remarks>
        /// Uses the same headcount and usage as adding items to an order. Per-order limits are
        /// for a new order. Limits set to Charge are left out: going over them is allowed and
        /// billed at full price.
        /// </remarks>
        [Authorize]
        [HttpGet("session/{sessionId}/limits")]
        [ProducesResponseType(typeof(SessionOrderLimitsDTO), StatusCodes.Status200OK)]
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
            var itemUsed = used.GroupBy(u => u.Item_Id).ToDictionary(g => g.Key, g => g.Sum(u => u.Quantity));
            var categoryUsed = used.GroupBy(u => u.Category_id).ToDictionary(g => g.Key, g => g.Sum(u => u.Quantity));

            static OrderLimitRules.Limits CategoryLimits(Category c) => new(c.adult_limit, c.child_limit,
                c.senior_limit, c.toddler_limit, c.per_order_limit, c.over_limit_behavior);
            static OrderLimitRules.Limits ItemLimits(MenuItemAssignment a) => new(a.Adult_Limit, a.Child_limit,
                a.Senior_limit, a.Tot_Limit, a.Per_Order_Limit, a.Over_Limit_Behavior);

            var result = new SessionOrderLimitsDTO
            {
                Items = assignments.Select(a =>
                {
                    var limits = ItemLimits(a);
                    return new ItemOrderLimitDTO
                    {
                        Item_Id = a.Item_Id,
                        Category_Id = a.MenuItem.Category_id,
                        Per_Order = OrderLimitRules.PerOrderCap(limits),
                        Session_Left = OrderLimitRules.SessionLeftBeforeBlock(limits, heads, itemUsed.GetValueOrDefault(a.Item_Id)),
                    };
                }).ToList(),
                Categories = assignments
                    .Select(a => a.MenuItem.Category)
                    .DistinctBy(c => c.Category_id)
                    .Select(c =>
                    {
                        var limits = CategoryLimits(c);
                        return new CategoryOrderLimitDTO
                        {
                            Category_Id = c.Category_id,
                            Per_Order = OrderLimitRules.PerOrderCap(limits),
                            Session_Left = OrderLimitRules.SessionLeftBeforeBlock(limits, heads, categoryUsed.GetValueOrDefault(c.Category_id)),
                        };
                    }).ToList(),
            };

            return Ok(result);
        }

        /// <summary>
        /// Creates a new order for a dining session.
        /// </summary>
        /// <param name="order_data">The order creation data including session ID and bill ID</param>
        /// <returns>
        /// An <see cref="ActionResult"/> containing the created <see cref="OrderResponseDTO"/> object.
        /// Returns HTTP 200 (OK) with the created order on success.
        /// Returns HTTP 404 (Not Found) if the session or bill doesn't exist.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during creation.
        /// </returns>
        /// <response code="200">Returns the newly created order</response>
        /// <response code="401">If the caller isn't signed in</response>
        /// <response code="403">If the caller isn't staff or seated at the session's table</response>
        /// <response code="404">If the dining session is not found or the bill is not found/open</response>
        /// <response code="500">If an internal error occurs while creating the order</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     POST /api/order
        ///     {
        ///         "session_Id": 123,
        ///         "bill_Id": 456
        ///     }
        ///
        /// This endpoint requires authentication.
        /// The order is automatically created with 'Pending' status.
        /// The authenticated user is automatically assigned as the order owner.
        /// Staff may set Request_By_User_Id / Request_By_Name to order on a customer's behalf;
        /// for anyone else those fields are ignored.
        /// The bill must be in 'Open' status to create an order.
        /// </remarks>
        [HttpPost]
        [Authorize]
        [ProducesResponseType(typeof(OrderResponseDTO), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<SessionOrder>>> CreateOrder(OrderCreateDTO order_data)
        {
            try
            {
                var session = await _context.DiningSessions.FirstOrDefaultAsync(ds => ds.Session_Id == order_data.Session_Id);
                if (session is null)
                {
                    return NotFound("Dinning Session was not found in the current context");
                }

                // Staff can order at any table; customers only at the table they're seated at
                if (!await SessionAccess.CanViewSessionAsync(_context, User, session.Session_Id))
                {
                    return Forbid();
                }

                var bill = await _context.Bills.FirstOrDefaultAsync(b => b.Bill_Id == order_data.Bill_Id && b.Status == BillStatus.Open);
                if (bill is null)
                {
                    return NotFound("Bill Was not found for the current session order Create Bill");
                }

                if (BillOrderingClosed(bill))
                {
                    return BadRequest(new { message = BillRequestedMessage });
                }

                int? userId = null;
                string userName;

                // Only staff may place an order on someone else's behalf
                var isStaff = User.IsInRole("Admin") || User.IsInRole("Staff");

                if (isStaff && order_data.Request_By_User_Id.HasValue)
                {
                    userId = order_data.Request_By_User_Id;
                    userName = order_data.Request_By_Name ?? "Guest";
                }
                else
                {
                    var userIdString = ClaimsHelpers.GetUserId(User);
                    userName = ClaimsHelpers.GetUserDisplayName(User);

                    if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out var parsedUserId))
                    {
                        return Unauthorized(new { message = "User ID not found in claims." });
                    }
                    userId = parsedUserId;
                }

                var now = DateTime.UtcNow;

                // First order of the session only; later orders leave it unchanged
                session.First_Order_At ??= now;

                var newOrder = new SessionOrder
                {
                    session_id = order_data.Session_Id,
                    Bill_Id = order_data.Bill_Id,
                    User_Id = userId,
                    User_Name = userName,
                    Status = OrderStatus.Pending,
                    Created_At = now
                };
                _context.SessionOrders.Add(newOrder);
                await _context.SaveChangesAsync();

                return Ok(new OrderResponseDTO
                {
                    Order_Id = newOrder.Order_Id,
                    Session_Id = newOrder.session_id,
                    Bill_Id = newOrder.Bill_Id,
                    User_Id = newOrder.User_Id,
                    User_Name = newOrder.User_Name,
                    Status = newOrder.Status,
                    Created_At = newOrder.Created_At,
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while creating the order", error = ex.Message });
            }
        }


        /// <summary>
        /// Approves a pending order and transitions to Approved/Processing status.
        /// </summary>
        [HttpPatch("{order_id}/approve")]
        [Authorize(Policy = "staffOnly")]
        [ProducesResponseType(typeof(OrderResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ApproveOrder(int order_id)
        {
            try
            {
                var order = await _context.SessionOrders
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.MenuItem)
                    .FirstOrDefaultAsync(o => o.Order_Id == order_id);

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

                return Ok(new OrderResponseDTO
                {
                    Order_Id = order.Order_Id,
                    Session_Id = order.session_id,
                    Bill_Id = order.Bill_Id,
                    User_Id = order.User_Id,
                    User_Name = order.User_Name,
                    Status = order.Status,
                    Created_At = order.Created_At,
                    OrderItems = order.OrderItems.Select(oi => new OrderItemResponseDTO
                    {
                        Order_Item_Id = oi.Order_Item_Id,
                        Order_Id = oi.Order_Key,
                        Menu_Id = oi.Menu_Id,
                        Item_Id = oi.Item_Id,
                        Quantity = oi.Quantity,
                        Price_At_Time = oi.Price_At_Time,
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
        /// Deletes a specific item from an order.
        /// </summary>
        /// <param name="order_id">The unique identifier of the order</param>
        /// <param name="item_id">The unique identifier of the item to delete</param>
        /// <returns>
        /// An <see cref="IActionResult"/> indicating the result of the operation.
        /// Returns HTTP 200 (OK) with a success message when the item is deleted.
        /// Returns HTTP 400 (Bad Request) if the order item doesn't exist.
        /// Returns HTTP 404 (Not Found) if the order doesn't exist.
        /// Returns HTTP 409 (Conflict) if the order is not pending and the user lacks permission.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during the operation.
        /// </returns>
        /// <response code="200">Returns a success message when the item is deleted</response>
        /// <response code="400">If the order item is not found</response>
        /// <response code="404">If the order is not found</response>
        /// <response code="409">If the order is not pending and the user is not authorized to modify it</response>
        /// <response code="500">If an internal error occurs while deleting the order item</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     DELETE /api/order/123/items/456
        ///
        /// This endpoint requires authentication.
        /// Users can only delete items from their own pending orders.
        /// Staff and Admin users can delete items from approved orders.
        /// </remarks>
        [HttpDelete("{order_id}/items/{order_item_id}")]
        [Authorize(Policy = "staffOnly")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> RemoveOrderItem(
            int order_id,
            int order_item_id
        )
        {
            try
            {
                var order = await _context.SessionOrders.Include(o => o.OrderItems).FirstOrDefaultAsync(so => so.Order_Id == order_id);
                if (order is null)
                {
                    return NotFound("Order Data was not found");
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

                if (order.Status != OrderStatus.Pending)
                {
                    // TODO: Please verify new functionality
                    if (!(userRole.Contains("Staff") || userRole.Contains("Admin")) && userId != order.User_Id)
                    {
                        return Conflict("Can not Modify a Approved Order");
                    }
                }
                var item = order.OrderItems.FirstOrDefault(oi => oi.Order_Item_Id == order_item_id);
                if (item is null)
                {
                    return BadRequest("Order Item was not found");
                }
                _context.OrderItems.Remove(item);
                await _context.SaveChangesAsync();
                return Ok("Item was Deleted");

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting item {ItemId} from order {OrderId}", order_item_id, order_id);
                return StatusCode(500, new { message = "An error occurred while deleting the order item", error = ex.Message });
            }
        }


        /// <summary>
        /// Marks an order item as completed/delivered.
        /// </summary>
        /// <param name="item_id">The unique identifier of the order item to complete</param>
        /// <returns>
        /// An <see cref="IActionResult"/> containing the updated <see cref="OrderItemResponseDTO"/> object.
        /// Returns HTTP 200 (OK) with the completed order item on success.
        /// Returns HTTP 404 (Not Found) if the order item doesn't exist.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during the operation.
        /// </returns>
        /// <response code="200">Returns the completed order item</response>
        /// <response code="404">If the order item is not found</response>
        /// <response code="500">If an internal error occurs while completing the order item</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     POST /api/order/items/123/complete
        ///
        /// This endpoint requires Admin or Staff role authorization.
        /// The order item status will be updated to 'Delivered' and the completion timestamp will be set.
        /// </remarks>
        [HttpPatch("{order_id}/items/{item_id}/complete")]
        [Authorize(Policy = "staffOnly")]
        [ProducesResponseType(typeof(OrderItemResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CompleteOrderItem(int order_id, int item_id)
        {
            try
            {
                var order = await _context.SessionOrders
                    .Include(o => o.OrderItems)
                    .FirstOrDefaultAsync(o => o.Order_Id == order_id);
                if (order is null) return NotFound("Order not found");

                if (!await SessionAccess.CanViewSessionAsync(_context, User, order.session_id))
                {
                    return LocationResults.OtherLocation();
                }

                var item = order.OrderItems.FirstOrDefault(oi => oi.Order_Item_Id == item_id);
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
                var response = new OrderItemResponseDTO
                {
                    Order_Item_Id = item.Order_Item_Id,
                    Order_Id = item.Order_Key,
                    Menu_Id = item.Menu_Id,
                    Item_Id = item.Item_Id,
                    Quantity = item.Quantity,
                    Price_At_Time = item.Price_At_Time,
                    Status = item.Order_Item_Status,
                    Completed_At = item.Completed_At,
                };
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing order item {ItemId}", item_id);
                return StatusCode(500, "Internal Server Error");
            }
        }

        /// <summary>
        /// Marks the whole order as delivered/completed.
        /// </summary>
        [HttpPatch("{order_id}/complete")]
        [Authorize(Policy = "staffOnly")]
        [ProducesResponseType(typeof(OrderResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CompleteOrder(int order_id)
        {
            try
            {
                var order = await _context.SessionOrders
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.MenuItem)
                    .FirstOrDefaultAsync(o => o.Order_Id == order_id);

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

                return Ok(new OrderResponseDTO
                {
                    Order_Id = order.Order_Id,
                    Session_Id = order.session_id,
                    Bill_Id = order.Bill_Id,
                    User_Id = order.User_Id,
                    Status = order.Status,
                    Created_At = order.Created_At,
                    Completed_At = order.Completed_At,
                    OrderItems = order.OrderItems.Select(oi => new OrderItemResponseDTO
                    {
                        Order_Item_Id = oi.Order_Item_Id,
                        Order_Id = oi.Order_Key,
                        Menu_Id = oi.Menu_Id,
                        Item_Id = oi.Item_Id,
                        Quantity = oi.Quantity,
                        Price_At_Time = oi.Price_At_Time,
                        Status = oi.Order_Item_Status,
                        Completed_At = oi.Completed_At,
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
        /// <param name="order_id">The unique identifier of the order to cancel</param>
        /// <returns>
        /// An <see cref="IActionResult"/> containing the updated <see cref="OrderResponseDTO"/> object.
        /// Returns HTTP 200 (OK) with the cancelled order on success.
        /// Returns HTTP 400 (Bad Request) if the order has already been delivered.
        /// Returns HTTP 404 (Not Found) if the order doesn't exist.
        /// Returns HTTP 409 (Conflict) if the order is not in pending status.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during cancellation.
        /// </returns>
        /// <response code="200">Returns the cancelled order</response>
        /// <response code="400">If the order has already been delivered and cannot be cancelled</response>
        /// <response code="404">If the order is not found</response>
        /// <response code="409">If the order is not in pending status</response>
        /// <response code="500">If an internal error occurs while cancelling the order</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     POST /api/order/123/cancel
        ///
        /// This endpoint requires authentication.
        /// Only orders with 'Pending' status can be cancelled.
        /// Orders that are approved or delivered cannot be cancelled.
        /// Staff can cancel any order at their location. Customers (guests included) can cancel
        /// orders they placed, while they're seated at that table.
        /// </remarks>
        [HttpPatch("{order_id}/cancel")]
        [Authorize]
        [ProducesResponseType(typeof(OrderResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CancelOrder(
            int order_id
        )
        {
            try
            {
                var order = await _context.SessionOrders.Include(o => o.OrderItems).FirstOrDefaultAsync(so => so.Order_Id == order_id);
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
                return Ok(new OrderResponseDTO
                {
                    Order_Id = order.Order_Id,
                    Session_Id = order.session_id,
                    Bill_Id = order.Bill_Id,
                    User_Id = order.User_Id,
                    User_Name = order.User_Name,
                    Status = order.Status,
                    Created_At = order.Created_At,
                    Completed_At = order.Completed_At,
                    OrderItems = order.OrderItems.Select(oi => new OrderItemResponseDTO
                    {
                        Order_Item_Id = oi.Order_Item_Id,
                        Order_Id = oi.Order_Key,
                        Menu_Id = oi.Menu_Id,
                        Item_Id = oi.Item_Id,
                        Quantity = oi.Quantity,
                        Price_At_Time = oi.Price_At_Time,
                        Status = oi.Order_Item_Status,
                        Completed_At = oi.Completed_At,
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
        /// <returns>
        /// Returns HTTP 200 (OK) with the lines added, including any over-limit charges.
        /// Returns HTTP 400 (Bad Request) if the payload is invalid, the order is not pending,
        /// an item is unavailable, or a Block limit would be exceeded (nothing is added).
        /// Returns HTTP 404 (Not Found) if the order doesn't exist.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs.
        /// </returns>
        /// <response code="200">Items added; response lists each line and any over-limit charges</response>
        /// <response code="400">Invalid request or a limit was exceeded; errors lists every problem</response>
        /// <response code="401">If the caller isn't signed in</response>
        /// <response code="403">If the caller isn't staff or seated at the order's table</response>
        /// <response code="404">If the order is not found</response>
        /// <response code="500">If an internal error occurs while adding items</response>
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
        /// - Per person (per dining session, limit x headcount from the session's bills):
        ///   Block rejects the request; Charge adds the excess as a separate line at full price.
        /// Cancelled orders and cancelled items don't count toward limits.
        /// The request is all-or-nothing: if any item fails, nothing is added.
        /// Prices are set by the server.
        /// Staff edits to pending orders (PATCH) are not limit-checked, so staff can override.
        /// Guests can't add items once the bill has been requested ("Ready for Bill"); staff can.
        /// </remarks>
        [HttpPost("{orderId}/items")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AddOrderItems(int orderId, [FromBody] List<OrderItemCreateDTO> items)
        {
            try
            {
                //Validate payload
                if (items == null || !items.Any())
                {
                    return BadRequest(new { message = "No items provided." });
                }
                if (items.Any(i => i.Quantity <= 0 || i.Quantity > 99))
                {
                    return BadRequest(new { message = "Each quantity must be between 1 and 99." });
                }

                // Combine duplicate item IDs so per-order limits can't be bypassed by splitting a request
                var requested = items
                    .GroupBy(i => i.Item_Id)
                    .Select(g => (ItemId: g.Key, Quantity: g.Sum(i => i.Quantity)))
                    .ToList();

                var order = await _context.SessionOrders
                    .Include(o => o.OrderItems)
                    .Include(o => o.DiningSession)
                    .Include(o => o.Bill)
                    .FirstOrDefaultAsync(o => o.Order_Id == orderId);

                if (order == null)
                {
                    return NotFound(new { message = $"Order {orderId} not found" });
                }

                // Staff can add to any order; customers only to orders at the table they're seated at
                if (!await SessionAccess.CanViewSessionAsync(_context, User, order.session_id))
                {
                    return Forbid();
                }

                // Guests can't add to a bill after pressing "Ready for Bill", but staff can
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

                var session = order.DiningSession;
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

                var sessionItemUsed = used.GroupBy(u => u.Item_Id).ToDictionary(g => g.Key, g => g.Sum(u => u.Quantity));
                var sessionCategoryUsed = used.GroupBy(u => u.Category_id).ToDictionary(g => g.Key, g => g.Sum(u => u.Quantity));
                var orderItemUsed = used.Where(u => u.Order_Key == orderId)
                    .GroupBy(u => u.Item_Id).ToDictionary(g => g.Key, g => g.Sum(u => u.Quantity));
                var orderCategoryUsed = used.Where(u => u.Order_Key == orderId)
                    .GroupBy(u => u.Category_id).ToDictionary(g => g.Key, g => g.Sum(u => u.Quantity));


                // Plan limit lines first, add nothing if any item fails (all or nothing)
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
                            category.senior_limit, category.toddler_limit, category.per_order_limit, category.over_limit_behavior),
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
                if (errors.Any())
                {
                    return BadRequest(new { message = $"Nothing was added. {string.Join(" ", errors)}", errors });
                }

                foreach (var line in newLines)
                {
                    order.OrderItems.Add(line);
                }
                await _context.SaveChangesAsync();

                var namesById = menuRows.GroupBy(a => a.Item_Id).ToDictionary(g => g.Key, g => g.First().MenuItem.Name);
                var overLimit = newLines.Where(l => l.Charge_Reason == ChargeReason.OverLimit).ToList();

                return Ok(new
                {
                    message = overLimit.Any()
                        ? "Items added. Some items are over the limit and will be charged."
                        : "Items added successfully",
                    count = newLines.Count,
                    orderId,
                    userId,
                    userName,
                    lines = newLines.Select(l => new
                    {
                        orderItemId = l.Order_Item_Id,
                        itemId = l.Item_Id,
                        name = namesById.GetValueOrDefault(l.Item_Id),
                        quantity = l.Quantity,
                        priceAtTime = l.Price_At_Time,
                        chargeReason = l.Charge_Reason.ToString()
                    }),
                    overLimitTotal = overLimit.Sum(l => l.Quantity * l.Price_At_Time)
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
        [HttpPatch("{order_id}/items/{item_id}")]
        [Authorize(Policy = "staffOnly")]
        [ProducesResponseType(typeof(OrderItemResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateOrderItem(
                   int order_id,
                   int item_id,
                   [FromBody] OrderItemUpdateDTO updateData
               )
        {
            try
            {
                var order = await _context.SessionOrders
                    .FirstOrDefaultAsync(o => o.Order_Id == order_id);

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
                    return Forbid("You don't have permission to modify this order");
                }

                // Can only modify pending orders
                if (order.Status != OrderStatus.Pending)
                {
                    return BadRequest("Can only modify pending orders");
                }

                var orderItem = await _context.OrderItems
                    .Include(oi => oi.MenuItem)
                    .FirstOrDefaultAsync(oi => oi.Order_Item_Id == item_id && oi.Order_Key == order_id);

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
                if (updateData.Price_At_Time.HasValue)
                {
                    if (!isStaff)
                    {
                        return Forbid("Only staff can modify prices");
                    }
                    if (updateData.Price_At_Time.Value < 0)
                    {
                        return BadRequest("Price cannot be negative");
                    }
                    orderItem.Price_At_Time = updateData.Price_At_Time.Value;
                }

                // Update status if provided
                if (!string.IsNullOrEmpty(updateData.Status))
                {
                    if (!isStaff)
                    {
                        return Forbid("Only staff can modify order item status");
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

                return Ok(new OrderItemResponseDTO
                {
                    Order_Item_Id = orderItem.Order_Item_Id,
                    Order_Id = orderItem.Order_Key,
                    Menu_Id = orderItem.Menu_Id,
                    Item_Id = orderItem.Item_Id,
                    Quantity = orderItem.Quantity,
                    Price_At_Time = orderItem.Price_At_Time,
                    Status = orderItem.Order_Item_Status,
                    Completed_At = orderItem.Completed_At,
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
        /// Retrieves all orders from the database.
        /// </summary>
        /// <returns>
        /// An <see cref="ActionResult"/> containing a collection of <see cref="SessionOrder"/> objects.
        /// Returns HTTP 200 (OK) with the list of all orders on success.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during retrieval.
        /// </returns>
        /// <response code="200">Returns the list of all orders</response>
        /// <response code="500">If an internal error occurs while retrieving orders</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     GET /api/order
        ///
        /// Returns all orders in the system without any filtering.
        /// </remarks>
        //GET api/order
        //Get all Orders
        [HttpGet]
        [Authorize(Policy = "staffOnly")]
        [ProducesResponseType(typeof(IEnumerable<SessionOrder>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<SessionOrder>>> GetAllOrder()
        {
            try
            {
                var scope = await _locationAccess.ResolveLocationAsync(User, null);
                if (scope.IsForbidden) return LocationResults.NoLocation();

                var order = await _context.SessionOrders
                    .Where(o => scope.LocationId == null || o.DiningSession.Location_Id == scope.LocationId)
                    .ToListAsync();

                return Ok(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Orders");
                return StatusCode(500, "Internal Server Error");
            }
        }

        /// <summary>
        /// Retrieves all orders, optionally filtered by location and delivered open sessions.
        /// </summary>
        /// <param name="locationId">Optional location filter</param>
        /// <param name="deliveredOpenSessionsOnly"> When true, delivered orders are only returned if the dining session is still open(Ended_At = null) Pending and Processing orders are always returned</param>
        /// <returns>The list of filtered orders</returns>
        /// <response code="200">Returns the list of filtered orders</response>
        /// <response code="500">If an internal error occurs while retrieving orders</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     GET /api/order/all_orders?locationId=1&deliveredOpenSessionsOnly=true
        ///
        /// Returns all orders filtered by the specified location and delivered open sessions.
        /// </remarks>
        [HttpGet("all_orders")]
        [Authorize(Policy = "staffOnly")]
        [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<object>>> GetAllOrdersByLocation([FromQuery] int? locationId = null, [FromQuery] bool deliveredOpenSessionsOnly = false)
        {
            try
            {
                var scope = await _locationAccess.ResolveLocationAsync(User, locationId);
                if (scope.IsForbidden) return LocationResults.NoLocation();
                locationId = scope.LocationId;

                var query = StaffOrdersQuery();

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
        /// <returns>Orders placed in the range, newest first, in the same shape as all_orders</returns>
        /// <remarks>
        /// Sample request:
        ///
        ///     GET /api/order/history?from=2026-10-20&amp;to=2026-10-26&amp;tableNumber=14&amp;name=lee
        ///
        /// </remarks>
        [HttpGet("history")]
        [Authorize(Policy = "staffOnly")]
        [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<object>>> GetOrderHistory(
            [FromQuery] string? from,
            [FromQuery] string? to,
            [FromQuery] int? locationId = null,
            [FromQuery] int? tableNumber = null,
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

        // Orders with everything the staff order views need. Shared by all_orders and history
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

        // The staff order shape, shared by all_orders and history so the two can't drift apart
        private static object ToStaffOrder(SessionOrder o) => new
        {
            orderId = o.Order_Id,
            sessionId = o.session_id,
            billId = o.Bill_Id,
            billName = o.Bill.Bill_Name,
            locationId = o.DiningSession.Location_Id,
            tableNumbers = o.DiningSession.Table_Id.HasValue
                ? new[] { o.DiningSession.Table!.table_number }
                : o.DiningSession.TableGroup?.Tables?.Select(t => t.table_number).ToArray() ?? Array.Empty<int>(),
            userName = o.User_Name,
            status = o.Status.ToString(),
            createdAt = o.Created_At,
            completedAt = o.Completed_At,
            items = o.OrderItems.Select(oi =>
            {
                var assignment = oi
                    .MenuItem
                    .MenuAssignments
                    .EffectiveFor(oi.Menu_Id, oi.Item_Id, o.DiningSession.Location_Id);
                bool isAddOn = assignment?.Is_Add_On ?? false;

                return new
                {
                    orderItemId = oi.Order_Item_Id,
                    itemId = oi.Item_Id,
                    name = oi.MenuItem.Name,
                    quantity = oi.Quantity,
                    priceAtTime = isAddOn || oi.Charge_Reason == ChargeReason.OverLimit ? oi.Price_At_Time : 0,
                    isAddOn = isAddOn,
                    chargeReason = oi.Charge_Reason.ToString(),
                    status = oi.Order_Item_Status.ToString()
                };
            }).ToList()
        };

        /// <summary>
        /// Retrieves detailed information about a specific order including all items and customer details.
        /// </summary>
        /// <param name="id">The unique identifier of the order</param>
        /// <returns>
        /// An <see cref="ActionResult"/> containing detailed order information.
        /// Returns HTTP 200 (OK) with the order details on success.
        /// Returns HTTP 404 (Not Found) if the order doesn't exist.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during retrieval.
        /// </returns>
        /// <response code="200">Returns the detailed order information</response>
        /// <response code="401">If the caller isn't signed in</response>
        /// <response code="403">If the caller isn't staff or seated at the order's table</response>
        /// <response code="404">If the order is not found</response>
        /// <response code="500">If an internal error occurs while retrieving the order</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     GET /api/order/789
        ///
        /// Returns comprehensive order details including:
        /// - Order metadata (ID, session, bill, status, timestamps)
        /// - Customer information (name and email)
        /// - Complete list of ordered items with:
        ///   - Item details (ID, name, quantity, price)
        ///   - Individual item status
        ///   - Item subtotals
        /// - Total order cost
        /// </remarks>
        //GET: api/order/{id}
        //Get an Order with all the items
        [HttpGet("{id}")]
        [Authorize]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
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

                // Staff see any order; customers only orders at the table they're seated at
                if (!await SessionAccess.CanViewSessionAsync(_context, User, order.session_id))
                {
                    return Forbid();
                }

                //Create object to store information about the order
                //Add the customers information
                //List all the items and calculate the cost of each item
                //Calculate the cost of every item together
                var orderInfo = new
                {
                    orderId = order.Order_Id,
                    sessionId = order.session_id,
                    billId = order.Bill_Id,
                    status = order.Status,
                    createdAt = order.Created_At,
                    completedAt = order.Completed_At,

                    customer = new
                    {
                        userId = order.User_Id,
                        userName = order.User_Name
                    },

                    items = order.OrderItems.Select(o => new
                    {
                        itemId = o.Item_Id,
                        itemName = o.MenuItem.Name,
                        quantity = o.Quantity,
                        priceAtTime = o.Price_At_Time,
                        itemStatus = o.Order_Item_Status,
                        itemTotal = o.Quantity * o.Price_At_Time
                    }).ToList(),

                    orderTotal = order.OrderItems.Select(i => i.Quantity * i.Price_At_Time).Sum()
                };

                return Ok(orderInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving order {OrderId}", id);
                return StatusCode(500, new { message = "An error occurred while retrieving the order", error = ex.Message });
            }
        }

        /// <summary>
        /// Retrieves all orders associated with a specific dining session.
        /// </summary>
        /// <param name="sessionId">The unique identifier of the dining session</param>
        /// <returns>
        /// An <see cref="ActionResult"/> containing a collection of order summary objects.
        /// Returns HTTP 200 (OK) with the list of orders sorted by creation time on success.
        /// Returns HTTP 404 (Not Found) if the session doesn't exist.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during retrieval.
        /// </returns>
        /// <response code="200">Returns the list of all orders for the specified session</response>
        /// <response code="404">If the session is not found</response>
        /// <response code="500">If an internal error occurs while retrieving orders</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     GET /api/order/session/456
        ///
        /// Returns order summaries including:
        /// - Order and customer details (ID, name)
        /// - Order status
        /// - Item count and total price
        /// - Timestamps (created and completed)
        /// 
        /// Orders are sorted by creation date in ascending order (oldest first).
        /// </remarks>
        //GET: api/order/session/{id}
        //Get all orders tied to a Dining Session
        [HttpGet("session/{sessionId}")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<object>>> GetOrderBySession(int sessionId, [FromQuery] int? bill_id = null)
        {
            try
            {
                var session = await _context.DiningSessions.FindAsync(sessionId);
                if (session == null)
                {
                    return NotFound(new { message = $"Can't find session with the ID: {sessionId}" });
                }

                // Staff see any table; customers see every order at the table they're seated at,
                // so when one person orders for everyone the rest of the table can see it too
                if (!await SessionAccess.CanViewSessionAsync(_context, User, sessionId))
                {
                    return Forbid();
                }

                var sessionOrder = await _context.SessionOrders
                    .Where(o => o.session_id == sessionId &&
                                (!bill_id.HasValue || o.Bill_Id == bill_id.Value))
                    .Include(o => o.Bill)
                    .Include(o => o.OrderItems)
                        .ThenInclude(or => or.MenuItem)
                            .ThenInclude(mi => mi.MenuAssignments)
                    .OrderBy(o => o.Created_At)
                    .ToListAsync();

                var orderAll = sessionOrder.Select(o => new
                {
                    orderId = o.Order_Id,
                    billId = o.Bill_Id,
                    billName = o.Bill?.Bill_Name,
                    billStatus = o.Bill?.Status.ToString(),
                    sessionId = o.session_id,
                    userId = o.User_Id,
                    userName = o.User_Name,
                    status = o.Status,
                    itemTotal = o.OrderItems.Count,
                    orderTotal = o.OrderItems.Sum(or =>
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
                    createdAt = o.Created_At,
                    completedAt = o.Completed_At,
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
                            orderItemId = oi.Order_Item_Id,
                            itemId = oi.Item_Id,
                            name = oi.MenuItem?.Name,
                            quantity = oi.Quantity,
                            priceAtTime = isAddOn || oi.Charge_Reason == ChargeReason.OverLimit ? oi.Price_At_Time : 0, // Only show price for add-ons
                            isAddOn = isAddOn,
                            chargeReason = oi.Charge_Reason.ToString(),
                            status = oi.Order_Item_Status
                        };
                    }).ToList()
                }).ToList();

                return Ok(orderAll);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving orders for session {SessionId}", sessionId);
                return StatusCode(500, new { message = "An error occurred while retrieving session orders", error = ex.Message });
            }
        }

        //GET: api/Order/active-session/orders
        //Get all of the orders for the user's ACTIVE session
        //Add an optional Bill ID parameter in-case of bill splitting
        [HttpGet("active-session/orders")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<object>>> GetOrdersForActiveSession([FromQuery] int? bill_id = null)
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
                                .Where(o => o.User_Id == userId && o.session_id == activeSessionId && (bill_id == null || o.Bill_Id == bill_id))
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
                    orderId = o.Order_Id,
                    SessionId = o.session_id,
                    bill_id = o.Bill_Id,
                    billStatus = o.Bill?.Status.ToString(),
                    status = o.Status.ToString(),
                    createdAt = o.Created_At,
                    completedAt = o.Completed_At,
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
                            orderItemId = i.Order_Item_Id,
                            itemId = i.Item_Id,
                            name = i.MenuItem?.Name,
                            quantity = i.Quantity,
                            price = isAddOn || i.Charge_Reason == ChargeReason.OverLimit ? i.Price_At_Time : 0, // Only show price for add-ons
                            isAddOn = isAddOn,
                            chargeReason = i.Charge_Reason.ToString(),
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