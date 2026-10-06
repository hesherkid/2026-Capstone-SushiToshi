using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.DTO.OrdersDTOs;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using back_end.Helpers;



namespace back_end.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]

    public class OrderController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<OrderController> _logger;

        public OrderController(ApplicationDbContext context, ILogger<OrderController> logger)
        {
            _context = context;
            _logger = logger;
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
        /// The bill must be in 'Open' status to create an order.
        /// </remarks>
        [HttpPost]
        [AllowAnonymous]
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

                var bill = await _context.Bills.FirstOrDefaultAsync(b => b.Bill_Id == order_data.Bill_Id && b.Status == BillStatus.Open);
                if (bill is null)
                {
                    return NotFound("Bill Was not found for the current session order Create Bill");
                }

                int? userId = null;
                string userName;

                if (order_data.Request_By_User_Id.HasValue)
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

                var newOrder = new SessionOrder
                {
                    session_id = order_data.Session_Id,
                    Bill_Id = order_data.Bill_Id,
                    User_Id = userId,
                    User_Name = userName,
                    Status = OrderStatus.Pending,
                    Created_At = DateTime.UtcNow
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
        [Authorize(Policy = "staffOnly")]
        [HttpPatch("{order_id}/approve")]
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
        [Authorize(Policy = "staffOnly")]
        [HttpDelete("{order_id}/items/{order_item_id}")]
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
        [Authorize(Policy = "staffOnly")]
        [HttpPatch("{order_id}/items/{item_id}/complete")]
        [ProducesResponseType(typeof(OrderItemResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CompleteOrderItem(
            int item_id
        )
        {
            try
            {
                var item = await _context.OrderItems.FirstOrDefaultAsync(or => or.Order_Item_Id == item_id);
                if (item is null)
                {
                    return NotFound("Item in a Order was not found");
                }
                item.Order_Item_Status = OrderStatus.Delivered;
                item.Completed_At = DateTime.UtcNow;
                _context.OrderItems.Update(item);
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
                _logger.LogError(ex, "Error getting Orders");
                return StatusCode(500, "Internal Server Error");
            }
        }

        /// <summary>
        /// Marks the whole order as delivered/completed.
        /// </summary>
        [Authorize(Policy = "staffOnly")]
        [HttpPatch("{order_id}/complete")]
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

                if (order.Status != OrderStatus.Processing)
                {
                    return BadRequest("Only approved orders can be marked as completed");
                }

                order.Status = OrderStatus.Delivered;
                order.Completed_At = DateTime.UtcNow;

                foreach (var item in order.OrderItems)
                {
                    item.Order_Item_Status = OrderStatus.Delivered;
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
        /// </remarks>
        [Authorize]
        [HttpPatch("{order_id}/cancel")]
        [ProducesResponseType(typeof(OrderResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
        /// Adds items to the created order from POST: api/Order
        /// POST: api/Order/{order_id}/items
        /// </summary>
        [HttpPost("{orderId}/items")]
        [AllowAnonymous]
        public async Task<IActionResult> AddOrderItems(int orderId, [FromBody] List<OrderItemCreateDTO> items)
        {
            try
            {
                //Validate payload
                if (items == null || !items.Any())
                {
                    return BadRequest(new { message = "No items provided." });
                }

                //Retrieve the order
                var order = await _context.SessionOrders
                    .Include(o => o.OrderItems)
                    .FirstOrDefaultAsync(o => o.Order_Id == orderId);

                //Check if order exists
                if (order == null)
                {
                    return NotFound(new { message = $"Order {orderId} not found" });
                }

                //Determine guest or logged in
                int? userId = null;
                string? userName = null;

                if (User.Identity?.IsAuthenticated == true)
                {
                    var userIdString = ClaimsHelpers.GetUserId(User);
                    userName = ClaimsHelpers.GetUserDisplayName(User);

                    if (int.TryParse(userIdString, out var parsedUserId))
                    {
                        userId = parsedUserId;
                        _logger.LogInformation("🔐 Authenticated user {UserId} ({Name})", userId, userName);
                    }
                    else
                    {
                        _logger.LogWarning("Could not parse user ID from claims: {UserIdString}", userIdString);
                    }
                }
                else
                {
                    // For guests, use null userId (they don't have database user records)
                    userId = null;
                    userName = "Guest";
                }

                // For guests, we still allow the operation but with null user ID
                // The User_Id field in the order can be null for guest users

                //Add items to order
                foreach (var item in items)
                {
                    _logger.LogInformation("Adding item {ItemId} qty {Qty} price {Price}",
                        item.Item_Id, item.Quantity, item.Price_At_Time);

                    order.OrderItems.Add(new OrderItems
                    {
                        Menu_Id = order.DiningSession?.Menu_Id ?? 1, //FIXME: Defaulting to 1 if DiningSession or Menu_Id is null. Only works if DiningSession has loaded. Line 538 that loads the order should include the DiningSession, currently only includes items. 
                        //TODO: either add lazy loading for DiningSession or include it when fetching the order.
                        Item_Id = item.Item_Id,
                        Quantity = item.Quantity,
                        Price_At_Time = item.Price_At_Time, //FIXME: This should reflect the price at the time of ordering, ensure it's correctly set.
                        Order_Item_Status = OrderStatus.Pending
                    });
                }


                _logger.LogInformation("💾 Saving changes...");
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Items added successfully",
                    count = items.Count,
                    orderId,
                    userId,
                    userName
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Error adding order items",
                    error = ex.ToString()
                });
            }
        }




        /// <summary>
        /// Updates order items before approval (pending orders only).
        /// </summary>
        [Authorize(Policy = "staffOnly")]
        [HttpPatch("{order_id}/items/{item_id}")]
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
                    if (Enum.TryParse<OrderStatus>(updateData.Status, true, out var newStatus))
                    {
                        orderItem.Order_Item_Status = newStatus;
                        if (newStatus == OrderStatus.Cancelled || newStatus == OrderStatus.Delivered)
                        {
                            orderItem.Completed_At = DateTime.UtcNow;
                        }
                        else
                        {
                            orderItem.Completed_At = null;
                        }
                    }
                    else
                    {
                        return BadRequest("Invalid status value");
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
        [Authorize(Policy = "staffOnly")]
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<SessionOrder>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<SessionOrder>>> GetAllOrder()
        {
            try
            {
                var order = await _context.SessionOrders.ToListAsync();

                return Ok(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Orders");
                return StatusCode(500, "Internal Server Error");
            }
        }

        // GET: api/Order/all-orders
        // Get all orders, filtered by location
        // Helpful for dashbaord
        [Authorize(Policy = "staffOnly")]
        [HttpGet("all_orders")]
        public async Task<ActionResult<IEnumerable<object>>> GetAllOrdersByLocation([FromQuery] int? locationId = null)
        {
            try
            {
                var query = _context.SessionOrders
                    .Include(o => o.DiningSession)
                        .ThenInclude(ds => ds.Menu)
                    .Include(o => o.DiningSession)
                        .ThenInclude(ds => ds.Table)
                    .Include(o => o.DiningSession)
                        .ThenInclude(ds => ds.TableGroup)
                            .ThenInclude(tg => tg.Tables)
                    .Include(o => o.Bill)
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.MenuItem)
                            .ThenInclude(mi => mi.MenuAssignments)
                    .AsQueryable();

                // Filter by location if provided
                if (locationId.HasValue)
                {
                    query = query.Where(o => o.DiningSession.Location_Id == locationId.Value);
                }

                var orders = await query
                    .OrderByDescending(o => o.Created_At)
                    .ToListAsync();

                var result = orders.Select(o => new
                {
                    orderId = o.Order_Id,
                    sessionId = o.session_id,
                    billId = o.Bill_Id,
                    locationId = o.DiningSession.Location_Id,
                    tableNumbers = o.DiningSession.Table_Id.HasValue
                        ? new[] { o.DiningSession.Table.table_number }
                        : o.DiningSession.TableGroup?.Tables?.Select(t => t.table_number).ToArray() ?? Array.Empty<int>(),
                    userName = o.User_Name,
                    status = o.Status.ToString(),
                    createdAt = o.Created_At,
                    completedAt = o.Completed_At,
                    items = o.OrderItems.Select(oi =>
                    {
                        var assignment = oi.MenuItem?.MenuAssignments
                            .FirstOrDefault(ma => ma.Menu_Id == oi.Menu_Id && ma.Item_Id == oi.Item_Id);
                        bool isAddOn = assignment?.Is_Add_On ?? false;

                        return new
                        {
                            orderItemId = oi.Order_Item_Id,
                            itemId = oi.Item_Id,
                            name = oi.MenuItem.Name,
                            quantity = oi.Quantity,
                            priceAtTime = isAddOn ? oi.Price_At_Time : 0,
                            isAddOn = isAddOn,
                            status = oi.Order_Item_Status.ToString()
                        };
                    }).ToList()
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving staff orders");
                return StatusCode(500, "Internal Server Error");
            }
        }

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
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<object>>> GetOrderBySession(int sessionId, [FromQuery] int? bill_id = null)
        {
            try
            {
                var session = await _context.DiningSessions.FindAsync(sessionId);
                if (session == null)
                {
                    return NotFound(new { message = $"Can't find session with the ID: {sessionId}" });
                }

                // Determine user identity
                int? userId = null;
                var userRole = ClaimsHelpers.GetUserRole(User) ?? "";
                bool isStaff = userRole.Contains("Staff") || userRole.Contains("Admin");

                // For authenticated users, get their User ID
                if (User.Identity?.IsAuthenticated == true)
                {
                    var userIdString = ClaimsHelpers.GetUserId(User);
                    if (int.TryParse(userIdString, out var parsedUserId))
                    {
                        userId = parsedUserId;
                    }
                }

                // Fetch orders (staff can see all, regular users can only see their own)
                var sessionOrder = await _context.SessionOrders
                    .Where(o => o.session_id == sessionId &&
                                (!bill_id.HasValue || o.Bill_Id == bill_id.Value) &&
                                (isStaff || !userId.HasValue || o.User_Id == userId))
                    .Include(o => o.OrderItems)
                        .ThenInclude(or => or.MenuItem)
                    .OrderBy(o => o.Created_At)
                    .ToListAsync();

                var orderAll = sessionOrder.Select(o => new
                {
                    orderId = o.Order_Id,
                    billId = o.Bill_Id,
                    billStatus = o.Bill?.Status.ToString(),
                    sessionId = o.session_id,
                    userId = o.User_Id,
                    userName = o.User_Name,
                    status = o.Status,
                    itemTotal = o.OrderItems.Count,
                    orderTotal = o.OrderItems.Sum(or =>
                    {
                        // Get menu assignment for this item
                        var assignment = or.MenuItem?.MenuAssignments
                            .FirstOrDefault(ma => ma.Menu_Id == or.Menu_Id && ma.Item_Id == or.Item_Id);

                        // Only count price if it's an add-on item
                        bool isAddOn = assignment?.Is_Add_On ?? false;
                        return isAddOn ? (or.Quantity * or.Price_At_Time) : 0;
                    }),
                    createdAt = o.Created_At,
                    completedAt = o.Completed_At,
                    // List of items in the order
                    items = o.OrderItems.Select(oi =>
                    {
                        // Get menu assignment for this specific item
                        var assignment = oi.MenuItem?.MenuAssignments
                            .FirstOrDefault(ma => ma.Menu_Id == oi.Menu_Id && ma.Item_Id == oi.Item_Id);

                        bool isAddOn = assignment?.Is_Add_On ?? false;

                        return new
                        {
                            orderItemId = oi.Order_Item_Id,
                            itemId = oi.Item_Id,
                            name = oi.MenuItem?.Name,
                            quantity = oi.Quantity,
                            priceAtTime = isAddOn ? oi.Price_At_Time : 0, // Only show price for add-ons
                            isAddOn = isAddOn,
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


        /// <summary>
        /// Retrieves all orders placed by a specific user.
        /// </summary>
        /// <param name="userOid">The unique identifier of the user (OID)</param>
        /// <returns>
        /// An <see cref="ActionResult"/> containing a collection of order history objects.
        /// Returns HTTP 200 (OK) with the list of orders sorted by most recent on success.
        /// Returns HTTP 404 (Not Found) if the user doesn't exist.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during retrieval.
        /// </returns>
        /// <response code="200">Returns the list of all orders for the specified user</response>
        /// <response code="404">If the user is not found</response>
        /// <response code="500">If an internal error occurs while retrieving orders</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     GET /api/order/user/{userOid}
        ///
        /// Returns order history including:
        /// - Order details (ID, session, menu name, status)
        /// - Item count and total price
        /// - Timestamps (created and completed)
        /// - List of items with names and quantities
        /// 
        /// Orders are sorted by creation date in descending order (newest first).
        /// </remarks>
        //GET: api/order/user/{userOid}
        //Get all orders placed by a specific customer
        [HttpGet("user/{userId}")]
        [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<object>>> GetOrderByUser(int userId)
        {
            try
            {
                //Check if userId is valid
                if (userId <= 0)
                {
                    return NotFound(new { message = $"Invalid user ID: {userId}" });
                }

                //Get all orders from this user and sort by the newest
                //Include session menu and item details
                var order = await _context.SessionOrders
                            .Where(o => o.User_Id == userId)
                            .Include(o => o.DiningSession)
                                .ThenInclude(or => or.Menu)
                            .Include(o => o.OrderItems)
                                .ThenInclude(od => od.MenuItem)
                            .OrderByDescending(o => o.Created_At)
                            .ToListAsync();

                //Include Order totals and items for each OrderID
                var orderHistory = order.Select(o => new
                {
                    orderId = o.Order_Id,
                    sessionId = o.session_id,
                    billId = o.Bill_Id,
                    menuName = o.DiningSession.Menu.Name,
                    status = o.Status,
                    itemCount = o.OrderItems.Count,
                    orderTotal = o.OrderItems.Sum(or => or.Quantity * or.Price_At_Time),
                    createdAt = o.Created_At,
                    completedAt = o.Completed_At,

                    items = o.OrderItems.Select(or => new
                    {
                        name = or.MenuItem.Name,
                        quantity = or.Quantity
                    }).ToList()
                }).ToList();

                return Ok(orderHistory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Can't find Order for User ID: {userId}");
                return StatusCode(500, "Internal Server Error");
            }
        }

        //GET: api/Order/active-session/orders
        //Get all of the orders for the user's ACTIVE session
        //Add an optional Bill ID parameter in-case of bill splitting
        [HttpGet("active-session/orders")]
        [AllowAnonymous]
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
                        // Get menu assignment for this item
                        var assignment = i.MenuItem?.MenuAssignments
                            .FirstOrDefault(ma => ma.Menu_Id == i.Menu_Id && ma.Item_Id == i.Item_Id);

                        bool isAddOn = assignment?.Is_Add_On ?? false;

                        return new
                        {
                            orderItemId = i.Order_Item_Id,
                            itemId = i.Item_Id,
                            name = i.MenuItem?.Name,
                            quantity = i.Quantity,
                            price = isAddOn ? i.Price_At_Time : 0, // Only show price for add-ons
                            isAddOn = isAddOn,
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