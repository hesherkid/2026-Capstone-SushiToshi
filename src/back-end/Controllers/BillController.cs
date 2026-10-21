using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.DTO.bill;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using back_end.Helpers;
using back_end.Services;

namespace back_end.controllers
{
  [ApiController]
  [Route("api/[controller]")]
  public class BillController : ControllerBase
  {
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _config;
    private readonly ILogger<BillController> _logger;
    private readonly IPricingService _pricingService;

    public BillController(ApplicationDbContext context, IConfiguration config, ILogger<BillController> logger, IPricingService pricingService)
    {
      _context = context;
      _config = config;
      _logger = logger;
      _pricingService = pricingService;
    }

    // Helper method to get table numbers from session
    private List<int> GetTableNumbers(DiningSession session)
    {
      if (session.Table != null)
      {
        return new List<int> { session.Table.table_number };
      }
      else if (session.TableGroup != null && session.TableGroup.Tables != null)
      {
        return session.TableGroup.Tables.Select(t => t.table_number).ToList();
      }
      return new List<int>();
    }

    /// <summary>
    /// Creates a new bill for a dining session.
    /// </summary>
    /// <param name="_session_id">The unique identifier of the dining session</param>
    /// <param name="bill_data">The bill creation data including guest counts and bill name</param>
    /// <returns>
    /// An <see cref="IActionResult"/> containing the created <see cref="BillResponse"/> object.
    /// Returns HTTP 200 (OK) with the created bill details on success.
    /// Returns HTTP 400 (Bad Request) if validation fails.
    /// Returns HTTP 404 (Not Found) if the session doesn't exist.
    /// Returns HTTP 500 (Internal Server Error) if an exception occurs during creation.
    /// </returns>
    /// <response code="200">Returns the newly created bill</response>
    /// <response code="400">If the session has ended, has no tables, or guest counts are invalid</response>
    /// <response code="404">If the session is not found</response>
    /// <response code="500">If an internal error occurs while creating the bill</response>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/bill/create_Bill/123
    ///     {
    ///         "bill_Name": "Table 5 Bill",
    ///         "adult_Count": 2,
    ///         "senior_Count": 1,
    ///         "child_Count": 0,
    ///         "tot_Count": 3
    ///     }
    ///
    /// Creates a new bill for the specified session.
    /// Requirements:
    /// - Session must be active (not ended)
    /// - Session must have at least one table assigned
    /// - At least one guest (adult, senior, or child) must be specified
    /// 
    /// The bill is automatically created with 'Open' status.
    /// </remarks>
    [HttpPost("create_Bill/{_session_id}")]
    public async Task<IActionResult> Create_Bill(int _session_id, CreateBill bill_data)
    {
      try
      {
        var session = await _context.DiningSessions
            .Include(ds => ds.Table)
            .Include(ds => ds.TableGroup)
                .ThenInclude(tg => tg!.Tables)
            .FirstOrDefaultAsync(ds => ds.Session_Id == _session_id);

        if (session == null)
        {
          return NotFound(new { message = "Bill or session was not found" });
        }

        if (session.Ended_At != null)
        {
          return BadRequest(new { message = $"Cannot create bill for session that ended {session.Ended_At}" });
        }

        // Check if session has either a table or table group assigned
        if (!session.Table_Id.HasValue && !session.TableGroup_Id.HasValue)
        {
          return BadRequest(new { message = "Cannot create a bill for a session that has no table or table group assigned" });
        }

        if (bill_data.Adult_Count <= 0 && bill_data.Senior_Count <= 0 && bill_data.Child_Count <= 0 && bill_data.Tot_Count <= 0)
        {
          return BadRequest(new { message = "Bill must have at least one guest in the session" });
        }

        // Validate Bill_Name is required
        if (string.IsNullOrWhiteSpace(bill_data.Bill_Name))
        {
          return BadRequest(new { message = "Bill_Name is required." });
        }

        int totalCount = bill_data.Total_Count
            ?? (bill_data.Adult_Count + bill_data.Senior_Count + bill_data.Child_Count + bill_data.Tot_Count);

        // Validate against table capacity
        int tableCapacity = 0;
        if (session.Table_Id.HasValue && session.Table != null)
        {
          tableCapacity = session.Table.seat_count;
        }
        else if (session.TableGroup_Id.HasValue && session.TableGroup?.Tables != null)
        {
          tableCapacity = session.TableGroup.Tables.Sum(t => t.seat_count);
        }

        // Check total guest count across open bills in this session
        var existingBillsGuestCount = await _context.Bills
            .Where(b =>
                b.Session_Id == _session_id &&
                b.Status == BillStatus.Open
            )
            .SumAsync(b => b.Total_Count);

        if (existingBillsGuestCount + totalCount > tableCapacity)
        {
          return BadRequest(new
          {
            message = $"Total guest count ({existingBillsGuestCount + totalCount}) exceeds table capacity ({tableCapacity}). " +
                      $"Current open bills: {existingBillsGuestCount} guests, New bill: {totalCount} guests."
          });
        }

        var NewBill = new Billing
        {
          Session_Id = _session_id,
          Bill_Name = bill_data.Bill_Name!, // safe after validation
          Senior_Count = bill_data.Senior_Count,
          Adult_Count = bill_data.Adult_Count,
          Child_Count = bill_data.Child_Count,
          Tot_Count = bill_data.Tot_Count,
          Total_Count = totalCount,
          Status = BillStatus.Open
        };

        _context.Bills.Add(NewBill);
        await _context.SaveChangesAsync();
        await _context.Entry(NewBill).ReloadAsync();

        return Ok(new BillResponse
        {
          Bill_Id = NewBill.Bill_Id,
          Session_Id = NewBill.Session_Id,
          Bill_Name = NewBill.Bill_Name,
          Senior_Count = NewBill.Senior_Count,
          Adult_Count = NewBill.Adult_Count,
          Child_Count = NewBill.Child_Count,
          Tot_Count = NewBill.Tot_Count,
          Total_Count = NewBill.Total_Count,
          Status = NewBill.Status.ToString(),
          Created_At = NewBill.Created_At,
          Closed_At = NewBill.Closed_At,
          Table_Numbers = GetTableNumbers(session)
        });
      }
      catch (DbUpdateException ex)
      {
        Console.WriteLine($"Error in Create Bill {ex}");
        return StatusCode(500, "There was a Problem in the Create Bill Method");
      }
    }

    /// <summary>
    /// Retrieves all bills for a specific dining session, optionally filtered by table.
    /// </summary>
    /// <param name="_session_id">The unique identifier of the dining session</param>
    /// <param name="_table_id">Optional table ID to filter bills by a specific table</param>
    /// <returns>
    /// An <see cref="IActionResult"/> containing a collection of <see cref="BillResponse"/> objects.
    /// Returns HTTP 200 (OK) with the list of bills on success.
    /// Returns HTTP 400 (Bad Request) if the specified table is not associated with the session.
    /// Returns HTTP 404 (Not Found) if the session doesn't exist or no bills are found.
    /// Returns HTTP 500 (Internal Server Error) if an exception occurs during retrieval.
    /// </returns>
    /// <response code="200">Returns the list of bills for the session</response>
    /// <response code="400">If the specified table is not associated with the session</response>
    /// <response code="404">If the session is not found</response>
    /// <response code="500">If an internal error occurs while retrieving bills</response>
    /// <remarks>
    /// Sample requests:
    ///
    ///     GET /api/bill/get_bills/123
    ///     (Returns all bills for session 123)
    ///     
    ///     GET /api/bill/get_bills/123?_table_id=456
    ///     (Returns bills for session 123, filtered by table 456)
    ///
    /// Bills are ordered by creation date (newest first).
    /// </remarks>
    [HttpGet("get_bills/{_session_id}")]
    [Authorize]
    public async Task<IActionResult> Get_Bills(int _session_id, int? _table_id = null)
    {
      try
      {
        if (!await SessionAccess.CanViewSessionAsync(_context, User, _session_id))
        {
          return Forbid();
        }

        var session = await _context.DiningSessions
            .Include(ds => ds.Table)
            .Include(ds => ds.TableGroup)
                .ThenInclude(tg => tg!.Tables)
            .FirstOrDefaultAsync(ds => ds.Session_Id == _session_id);

        if (session == null)
        {
          return NotFound(new { message = "Session was not found" });
        }

        var billQuery = _context.Bills.Where(b => b.Session_Id == _session_id);

        // If table_id is specified, validate it belongs to the session
        if (_table_id.HasValue)
        {
          bool tableInSession = false;

          // Check if it's the directly assigned table
          if (session.Table_Id == _table_id.Value)
          {
            tableInSession = true;
          }
          // Check if it's in the table group
          else if (session.TableGroup?.Tables.Any(t => t.Table_Id == _table_id.Value) == true)
          {
            tableInSession = true;
          }

          if (!tableInSession)
          {
            return BadRequest(new { message = $"Table with ID {_table_id.Value} is not associated with session {_session_id}" });
          }
        }

        // A session with no bills yet is normal (e.g. just seated), so return an empty list
        var bills = billQuery.OrderByDescending(b => b.Created_At).ToList();

        var billResponses = bills.Select(bill => new BillResponse
        {
          Bill_Id = bill.Bill_Id,
          Session_Id = bill.Session_Id,
          Bill_Name = bill.Bill_Name ?? string.Empty,
          Senior_Count = bill.Senior_Count,
          Adult_Count = bill.Adult_Count,
          Child_Count = bill.Child_Count,
          Tot_Count = bill.Tot_Count,
          Total_Count = bill.Total_Count,
          Status = bill.Status.ToString(),
          Created_At = bill.Created_At,
          Closed_At = bill.Closed_At,
          Requested_At = bill.Requested_At,
          Table_Numbers = GetTableNumbers(session)
        }).ToList();

        return Ok(billResponses);
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error in Get Bill: {ex}");
        return StatusCode(500, "There was a Problem in the Get Bill Method");
      }
    }

    /// <summary>
    /// Retrieves detailed information about a specific bill.
    /// </summary>
    /// <param name="_session_id">The unique identifier of the dining session</param>
    /// <param name="_bill_id">The unique identifier of the bill</param>
    /// <returns>
    /// An <see cref="IActionResult"/> containing the <see cref="BillResponse"/> object.
    /// Returns HTTP 200 (OK) with the bill details on success.
    /// Returns HTTP 404 (Not Found) if the bill or session doesn't exist.
    /// Returns HTTP 500 (Internal Server Error) if an exception occurs during retrieval.
    /// </returns>
    /// <response code="200">Returns the bill details</response>
    /// <response code="404">If the bill or session is not found</response>
    /// <response code="500">If an internal error occurs while retrieving the bill</response>
    /// <remarks>
    /// Sample request:
    ///
    ///     GET /api/bill/get_bill/123?_session_id=456
    ///
    /// Returns complete bill information including guest counts, status, and timestamps.
    /// </remarks>
    [HttpGet("get_bill/{_bill_id}")]
    [Authorize]
    public async Task<IActionResult> Get_Bill(int _session_id, int _bill_id)
    {
      try
      {
        if (!await SessionAccess.CanViewSessionAsync(_context, User, _session_id))
        {
          return Forbid();
        }

        var bill = await _context.Bills
            .Include(b => b.DiningSession)
                .ThenInclude(ds => ds.Table)
            .Include(b => b.DiningSession)
                .ThenInclude(ds => ds.TableGroup)
                    .ThenInclude(tg => tg!.Tables)
            .FirstOrDefaultAsync(b => b.Bill_Id == _bill_id && b.Session_Id == _session_id);

        if (bill == null)
        {
          return NotFound(new { message = "Bill or session was not found" });
        }

        return Ok(new BillResponse
        {
          Bill_Id = bill.Bill_Id,
          Session_Id = bill.Session_Id,
          Bill_Name = bill.Bill_Name ?? string.Empty,
          Senior_Count = bill.Senior_Count,
          Adult_Count = bill.Adult_Count,
          Child_Count = bill.Child_Count,
          Tot_Count = bill.Tot_Count,
          Total_Count = bill.Total_Count,
          Status = bill.Status.ToString(),
          Created_At = bill.Created_At,
          Closed_At = bill.Closed_At,
          Requested_At = bill.Requested_At,
          Table_Numbers = GetTableNumbers(bill.DiningSession)
        });
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error in Get Bill: {ex}");
        return StatusCode(500, "There was a Problem in the Get Bill Method");
      }
    }

    /// <summary>
    /// Closes an open bill after verifying all orders are completed.
    /// </summary>
    /// <param name="_session_id">The unique identifier of the dining session</param>
    /// <param name="_bill_id">The unique identifier of the bill to close</param>
    /// <returns>
    /// An <see cref="IActionResult"/> containing the closed <see cref="BillResponse"/> object.
    /// Returns HTTP 200 (OK) with the closed bill details on success.
    /// Returns HTTP 400 (Bad Request) if the bill is already closed or has pending orders.
    /// Returns HTTP 404 (Not Found) if the bill or session doesn't exist.
    /// Returns HTTP 500 (Internal Server Error) if an exception occurs during closure.
    /// </returns>
    /// <response code="200">Returns the closed bill details</response>
    /// <response code="400">If the bill is already closed or has pending orders</response>
    /// <response code="404">If the bill or session is not found</response>
    /// <response code="500">If an internal error occurs while closing the bill</response>
    /// <remarks>
    /// Sample request:
    ///
    ///     PUT /api/bill/close_bill/123?_session_id=456
    ///
    /// Closes the specified bill and sets the closed_at timestamp.
    /// Requirements:
    /// - Bill must not already be closed
    /// - All orders associated with the bill must be completed (no pending orders)
    /// </remarks>
    [HttpPut("close_bill/{_bill_id}")]
    public async Task<IActionResult> Close_Bill(int _session_id, int _bill_id)
    {
      try
      {
        var bill = await _context.Bills
            .Include(b => b.DiningSession)
                .ThenInclude(ds => ds.Table)
            .Include(b => b.DiningSession)
                .ThenInclude(ds => ds.TableGroup)
                    .ThenInclude(tg => tg!.Tables)
            .FirstOrDefaultAsync(b => b.Bill_Id == _bill_id && b.Session_Id == _session_id);

        if (bill == null)
        {
          return NotFound(new { message = "Bill or session was not found" });
        }

        if (bill.Status == BillStatus.Closed)
        {
          return BadRequest(new { message = "Bill is already closed" });
        }

        var pendingOrders = await _context.SessionOrders
            .Where(o => o.Bill_Id == _bill_id && o.Status == OrderStatus.Pending)
            .ToListAsync();

        if (pendingOrders.Any())
        {
          return BadRequest(new { message = "Cannot close bill with pending orders" });
        }

        bill.Status = BillStatus.Closed;
        bill.Closed_At = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await _context.Entry(bill).ReloadAsync();

        return Ok(new BillResponse
        {
          Bill_Id = bill.Bill_Id,
          Session_Id = bill.Session_Id,
          Bill_Name = bill.Bill_Name ?? string.Empty,
          Senior_Count = bill.Senior_Count,
          Adult_Count = bill.Adult_Count,
          Child_Count = bill.Child_Count,
          Tot_Count = bill.Tot_Count,
          Total_Count = bill.Total_Count,
          Status = bill.Status.ToString(),
          Created_At = bill.Created_At,
          Closed_At = bill.Closed_At,
          Requested_At = bill.Requested_At,
          Table_Numbers = GetTableNumbers(bill.DiningSession)
        });
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error in Close Bill: {ex}");
        return StatusCode(500, "There was a Problem in the Close Bill Method");
      }
    }

    /// <summary>
    /// The table's leftover fee: the default amount, and each open bill's current share.
    /// </summary>
    /// <param name="_session_id">The dining session (table)</param>
    /// <response code="200">The default fee, the current total and each open bill's share</response>
    /// <response code="404">If the session doesn't exist</response>
    /// <remarks>
    /// Used to fill in the "Add leftover fee" dialog before staff apply it.
    /// </remarks>
    [HttpGet("leftover_fee/{_session_id}")]
    [Authorize(Policy = "staffOnly")]
    [ProducesResponseType(typeof(LeftoverFeeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLeftoverFee(int _session_id)
    {
      if (!await _context.DiningSessions.AnyAsync(s => s.Session_Id == _session_id))
      {
        return NotFound(new { message = "Session was not found" });
      }

      var openBills = await OpenBillsAsync(_session_id);
      return Ok(LeftoverFeeResult(_session_id, openBills));
    }

    /// <summary>
    /// Applies a leftover fee to the table, split evenly across its open bills.
    /// </summary>
    /// <param name="_session_id">The dining session (table)</param>
    /// <param name="request">The fee; leave amount out to use the default, or send 0 to remove it</param>
    /// <response code="200">The fee was applied; returns each open bill's share</response>
    /// <response code="400">If the amount is negative or the table has no open bills</response>
    /// <response code="404">If the session doesn't exist</response>
    /// <remarks>
    /// Sample request:
    ///
    ///     PUT /api/bill/leftover_fee/456
    ///     { "amount": 15.00 }
    ///
    /// Staff decide when a table has left too much food. The fee is for the whole table and is
    /// split evenly across its open bills ($15.00 over 2 bills = $7.50 each), replacing any fee
    /// applied before. Closed and paid bills keep what they had, so apply it before closing bills.
    /// The fee is taxed with the rest of the bill.
    /// </remarks>
    [HttpPut("leftover_fee/{_session_id}")]
    [Authorize(Policy = "staffOnly")]
    [ProducesResponseType(typeof(LeftoverFeeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApplyLeftoverFee(int _session_id, [FromBody] LeftoverFeeRequest request)
    {
      try
      {
        if (!await _context.DiningSessions.AnyAsync(s => s.Session_Id == _session_id))
        {
          return NotFound(new { message = "Session was not found" });
        }

        var amount = request?.Amount ?? _pricingService.GetCurrentPricing().LeftoverFee;
        if (amount < 0)
        {
          return BadRequest(new { message = "The leftover fee can't be negative." });
        }

        var openBills = await OpenBillsAsync(_session_id);
        if (openBills.Count == 0)
        {
          return BadRequest(new { message = "This table has no open bills to add a leftover fee to." });
        }

        var shares = LeftoverFee.SplitEvenly(amount, openBills.Count);
        for (var i = 0; i < openBills.Count; i++)
        {
          openBills[i].Leftover_Charge = shares[i];
        }
        await _context.SaveChangesAsync();

        _logger.LogInformation("Leftover fee {Amount:F2} applied to session {SessionId} across {Count} bills",
            amount, _session_id, openBills.Count);

        return Ok(LeftoverFeeResult(_session_id, openBills));
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error applying leftover fee to session {SessionId}", _session_id);
        return StatusCode(500, new { message = "Error applying leftover fee" });
      }
    }

    // Open bills in a fixed order (oldest first), so any extra cent always lands on the same bill
    private Task<List<Billing>> OpenBillsAsync(int sessionId) =>
        _context.Bills
            .Where(b => b.Session_Id == sessionId && b.Status == BillStatus.Open)
            .OrderBy(b => b.Bill_Id)
            .ToListAsync();

    private LeftoverFeeResponse LeftoverFeeResult(int sessionId, List<Billing> openBills) => new()
    {
      Session_Id = sessionId,
      Default_Fee = _pricingService.GetCurrentPricing().LeftoverFee,
      Amount = openBills.Sum(b => b.Leftover_Charge),
      Bills = openBills.Select(b => new LeftoverFeeBillShare
      {
        Bill_Id = b.Bill_Id,
        Bill_Name = b.Bill_Name ?? string.Empty,
        Leftover_Charge = b.Leftover_Charge,
      }).ToList(),
    };

    /// <summary>
    /// Guests ask for their bill ("Ready for Bill").
    /// </summary>
    /// <remarks>
    /// Marks the bill as requested, which stops any more orders being placed on it, and sends staff a
    /// "Ready for bill" service request. Asking again is safe: it returns the original request time.
    /// Only guests seated at the table (or staff) can ask.
    /// </remarks>
    /// <response code="200">The bill is requested; returns when it was first requested.</response>
    /// <response code="400">The bill isn't open.</response>
    /// <response code="403">The caller isn't seated at this table.</response>
    /// <response code="404">The bill or session was not found.</response>
    [HttpPost("request_bill/{_bill_id}")]
    [Authorize]
    public async Task<IActionResult> Request_Bill(int _session_id, int _bill_id)
    {
      if (!await SessionAccess.CanViewSessionAsync(_context, User, _session_id))
      {
        return Forbid();
      }

      var bill = await _context.Bills
          .Include(b => b.DiningSession).ThenInclude(ds => ds.TableGroup).ThenInclude(tg => tg!.Tables)
          .FirstOrDefaultAsync(b => b.Bill_Id == _bill_id && b.Session_Id == _session_id);
      if (bill == null)
      {
        return NotFound(new { message = "Bill or session was not found" });
      }
      if (bill.Status != BillStatus.Open)
      {
        return BadRequest(new { message = $"This bill is already {bill.Status.ToString().ToLower()}." });
      }
      if (bill.Requested_At != null)
      {
        return Ok(new { requested_at = bill.Requested_At, already_requested = true });
      }

      if (!int.TryParse(ClaimsHelpers.GetUserId(User), out var userId))
      {
        return Unauthorized(new { message = "Please sign in to ask for your bill." });
      }

      // Requests are tied to a table; a table group's go to its first table
      var session = bill.DiningSession;
      int? tableId = session.Table_Id ?? session.TableGroup?.Tables.OrderBy(t => t.table_number).FirstOrDefault()?.Table_Id;
      if (tableId == null)
      {
        return BadRequest(new { message = "This session has no table to send the request to." });
      }

      bill.Requested_At = DateTime.UtcNow;
      _context.ServiceRequests.Add(new ServiceRequest
      {
        Session_Id = session.Session_Id,
        Table_Id = tableId.Value,
        Request_By = userId,
        Notes = $"Ready for bill: {bill.Bill_Name}",
        Status = ServiceRequestStatus.Pending,
      });
      await _context.SaveChangesAsync();

      return Ok(new { requested_at = bill.Requested_At, already_requested = false });
    }

    /// <summary>
    /// Cancels an open bill and all associated pending orders.
    /// </summary>
    /// <param name="_session_id">The unique identifier of the dining session</param>
    /// <param name="_bill_id">The unique identifier of the bill to cancel</param>
    /// <returns>
    /// An <see cref="IActionResult"/> containing the cancelled <see cref="BillResponse"/> object.
    /// Returns HTTP 200 (OK) with the cancelled bill details on success.
    /// Returns HTTP 400 (Bad Request) if the bill is not in Open status.
    /// Returns HTTP 404 (Not Found) if the bill or session doesn't exist.
    /// Returns HTTP 500 (Internal Server Error) if an exception occurs during cancellation.
    /// </returns>
    /// <response code="200">Returns the cancelled bill details</response>
    /// <response code="400">If the bill cannot be cancelled (not in Open status)</response>
    /// <response code="404">If the bill or session is not found</response>
    /// <response code="500">If an internal error occurs while cancelling the bill</response>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/bill/cancel_bill/123?_session_id=456
    ///
    /// Cancels the specified bill and automatically cancels all pending orders associated with it.
    /// Only bills with 'Open' status can be cancelled.
    /// The bill's closed_at timestamp is set to the current UTC time.
    /// </remarks>
    [HttpPost("cancel_bill/{_bill_id}")]
    public async Task<IActionResult> Cancel_Bill(int _session_id, int _bill_id)
    {
      try
      {
        var bill = await _context.Bills
            .Include(b => b.DiningSession)
                .ThenInclude(ds => ds.Table)
            .Include(b => b.DiningSession)
                .ThenInclude(ds => ds.TableGroup)
                    .ThenInclude(tg => tg!.Tables)
            .FirstOrDefaultAsync(b => b.Bill_Id == _bill_id && b.Session_Id == _session_id);

        if (bill == null)
        {
          return NotFound(new { message = "Bill or session was not found" });
        }

        if (bill.Status != BillStatus.Open)
        {
          return BadRequest(new { message = $"Cannot cancel bill with status {bill.Status}" });
        }

        var pendingOrders = await _context.SessionOrders
            .Where(o => o.Bill_Id == _bill_id && (o.Status == OrderStatus.Pending))
            .ToListAsync();

        if (pendingOrders.Any())
        {
          for (int i = 0; i < pendingOrders.Count; i++)
          {
            var order = pendingOrders[i];
            order.Status = OrderStatus.Cancelled;
          }
        }

        bill.Status = BillStatus.Cancelled;
        bill.Closed_At = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await _context.Entry(bill).ReloadAsync();

        return Ok(new BillResponse
        {
          Bill_Id = bill.Bill_Id,
          Session_Id = bill.Session_Id,
          Bill_Name = bill.Bill_Name ?? string.Empty,
          Senior_Count = bill.Senior_Count,
          Adult_Count = bill.Adult_Count,
          Child_Count = bill.Child_Count,
          Tot_Count = bill.Tot_Count,
          Total_Count = bill.Total_Count,
          Status = bill.Status.ToString(),
          Created_At = bill.Created_At,
          Closed_At = bill.Closed_At,
          Requested_At = bill.Requested_At,
          Table_Numbers = GetTableNumbers(bill.DiningSession)
        });
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error in Cancel Bill: {ex}");
        return StatusCode(500, "There was a Problem in the Cancel Bill Method");
      }
    }

    /// <summary>
    /// Retrieves all bills associated with the current user's active dining session.
    /// </summary>
    /// <returns>
    /// An <see cref="ActionResult"/> containing a collection of <see cref="BillResponse"/> objects.
    /// Returns HTTP 200 (OK) with the list of bills on success.
    /// Returns HTTP 404 (Not Found) if no bills exist for the user's active session.
    /// Returns HTTP 500 (Internal Server Error) if an exception occurs during retrieval.
    /// </returns>
    /// <response code="200">Returns the list of bills for the user's active session</response>
    /// <response code="404">If no bills are found for the user's active session</response>
    /// <response code="500">If an internal error occurs while retrieving bills</response>
    /// <remarks>
    /// Sample request:
    ///
    ///     GET /api/bill/active/bills
    ///
    /// This endpoint requires authentication.
    /// Returns all bills associated with the authenticated user's current active session.
    /// Only returns bills where:
    /// - The user is an active participant (hasn't left the session)
    /// - The dining session is still active (hasn't ended)
    /// </remarks>
    [Authorize]
    [HttpGet("active/bills")]
    public async Task<ActionResult<List<BillResponse>>> GetActiveBills()
    {
      try
      {
        // Retrieve current user ID from claims using ClaimsHelpers
        var userIdString = ClaimsHelpers.GetUserId(User);

        if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out var userId))
        {
          return Unauthorized(new { detail = "User ID not found in claims or invalid format." });
        }

        // Get all bills for user's active session using joins
        var bills = await _context.Bills
            .Include(b => b.DiningSession.Table)
            .Include(b => b.DiningSession.TableGroup)
                .ThenInclude(tg => tg!.Tables)
            .Join(
                _context.SessionParticipants,
                bill => bill.Session_Id,
                participant => participant.Session_Id,
                (bill, participant) => new { bill, participant }
            )
            .Where(x =>
                x.participant.User_Id == userId &&
                x.participant.Left_At == null &&  // User hasn't left
                x.bill.DiningSession.Ended_At == null        // Session is active
            )
            .Select(x => x.bill)
            .Distinct() // Avoid duplicate bills if any
            .ToListAsync();

        if (!bills.Any())
        {
          return NotFound(new { detail = "No bills found for active session" });
        }

        // Convert bills to response model
        _logger.LogInformation($"Found {bills.Count} bills for user {userId}'s active session");

        var billResponses = bills.Select(bill => new BillResponse
        {
          Bill_Id = bill.Bill_Id,
          Session_Id = bill.Session_Id,
          Bill_Name = bill.Bill_Name ?? string.Empty,
          Senior_Count = bill.Senior_Count,
          Adult_Count = bill.Adult_Count,
          Child_Count = bill.Child_Count,
          Tot_Count = bill.Tot_Count,
          Total_Count = bill.Total_Count,
          Status = bill.Status.ToString(),
          Created_At = bill.Created_At,
          Closed_At = bill.Closed_At,
          Requested_At = bill.Requested_At,
          Table_Numbers = GetTableNumbers(bill.DiningSession)
        }).ToList();

        return Ok(billResponses);
      }
      catch (Exception ex)
      {
        _logger.LogError($"Database error retrieving bills: {ex.Message}");
        return StatusCode(500, new { detail = "Error retrieving bills" });
      }
    }

    /// <summary>
    /// Retrieves a detailed bill summary with pricing breakdown for payment.
    /// </summary>
    [HttpGet("summary/{_bill_id}")]
    [ProducesResponseType(typeof(BillSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [Authorize]
    public async Task<IActionResult> GetBillSummary(int _session_id, int _bill_id)
    {
      try
      {
        // Staff, or customers seated at this table (see SessionAccess)
        if (!await SessionAccess.CanViewSessionAsync(_context, User, _session_id))
        {
          return Forbid();
        }

        var bill = await _context.Bills
            .Include(b => b.DiningSession.Table)
            .Include(b => b.DiningSession.TableGroup)
                .ThenInclude(tg => tg!.Tables)
            .FirstOrDefaultAsync(b => b.Bill_Id == _bill_id && b.Session_Id == _session_id);

        if (bill == null)
        {
          return NotFound(new { message = "Bill or session was not found" });
        }

        // Get pricing based on when the bill was created
        var pricing = _pricingService.GetPricingForDate(bill.Created_At);

        // Calculate base charges using day-specific pricing
        decimal baseChargesSubtotal =
            (bill.Adult_Count * pricing.AdultBasePrice) +
            (bill.Senior_Count * pricing.SeniorBasePrice) +
            (bill.Child_Count * pricing.ChildBasePrice) +
            (bill.Tot_Count * pricing.ToddlerBasePrice);

        // Charged lines for this bill (cancelled orders and cancelled items are excluded).
        // Prices are snapshots on each order line, so later price changes don't alter old bills.
        var chargedLines = await _context.OrderItems
          .Include(oi => oi.MenuItem)
          .Where(oi =>
              oi.SessionOrder.Bill_Id == _bill_id &&
              oi.SessionOrder.Status != OrderStatus.Cancelled &&
              oi.Order_Item_Status != OrderStatus.Cancelled &&
              oi.Price_At_Time > 0)
          .ToListAsync();

        // Add-ons and drinks (charged every time); also any line staff priced manually
        var addOnItems = chargedLines
          .Where(oi => oi.Charge_Reason == ChargeReason.None && oi.Price_At_Time > 0)
          .Select(oi => ToBillItem(oi, oi.Quantity, oi.Price_At_Time))
          .ToList();

        // Units over a Charge limit (e.g. Hot Stone extras)
        var overLimitItems = chargedLines
          .Where(oi => oi.Charge_Reason == ChargeReason.OverLimit)
          .Select(oi => ToBillItem(oi, oi.Quantity, oi.Price_At_Time))
          .ToList();

        decimal addOnSubtotal = addOnItems.Sum(item => item.Line_Total);
        decimal overLimitSubtotal = overLimitItems.Sum(item => item.Line_Total);
        decimal leftoverCharge = bill.Leftover_Charge;   // this bill's share of the table's leftover fee
        decimal subtotal = baseChargesSubtotal + addOnSubtotal + overLimitSubtotal + leftoverCharge;
        decimal taxAmount = Math.Round(subtotal * pricing.TaxRate, 2);
        decimal totalAmount = subtotal + taxAmount;

        var summary = new BillSummaryResponse
        {
          Bill_Id = bill.Bill_Id,
          Session_Id = bill.Session_Id,
          Bill_Name = bill.Bill_Name ?? string.Empty,

          // Guest counts
          Adult_Count = bill.Adult_Count,
          Senior_Count = bill.Senior_Count,
          Child_Count = bill.Child_Count,
          Tot_Count = bill.Tot_Count,
          Total_Count = bill.Total_Count,

          // Base pricing (day-specific)
          Adult_Base_Price = pricing.AdultBasePrice,
          Senior_Base_Price = pricing.SeniorBasePrice,
          Child_Base_Price = pricing.ChildBasePrice,
          Toddler_Base_Price = pricing.ToddlerBasePrice,
          Base_Charges_Subtotal = baseChargesSubtotal,

          // Add-ons
          AddOn_Items = addOnItems,
          AddOn_Subtotal = addOnSubtotal,

          // Over-limit and leftover charges
          OverLimit_Items = overLimitItems,
          OverLimit_Subtotal = overLimitSubtotal,
          Leftover_Charge = leftoverCharge,

          // Totals
          Subtotal = subtotal,
          Tax_Rate = pricing.TaxRate,
          Tax_Amount = taxAmount,
          Total_Amount = totalAmount,

          // Status
          Status = bill.Status.ToString(),
          Pricing_Type = pricing.PricingType,
          Created_At = bill.Created_At,
          Closed_At = bill.Closed_At,
          Requested_At = bill.Requested_At,
          Table_Numbers = GetTableNumbers(bill.DiningSession)
        };

        _logger.LogInformation(
            $"Generated {pricing.PricingType} bill summary for Bill {_bill_id}: " +
            $"Total ${totalAmount:F2} (Base: ${baseChargesSubtotal:F2}, Add-ons: ${addOnSubtotal:F2}, " +
            $"Over-limit: ${overLimitSubtotal:F2}, Leftovers: ${leftoverCharge:F2}, Tax: ${taxAmount:F2})");

        return Ok(summary);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error calculating bill summary for Bill {BillId}", _bill_id);
        return StatusCode(500, new { message = "Error calculating bill summary", error = ex.Message });
      }
    }

    private static BillAddOnItem ToBillItem(OrderItems oi, int quantity, decimal pricePerUnit) => new BillAddOnItem
    {
      Order_Item_Id = oi.Order_Item_Id,
      Item_Id = oi.Item_Id,
      Item_Name = oi.MenuItem?.Name ?? string.Empty,
      Quantity = quantity,
      Price_Per_Unit = pricePerUnit,
      Line_Total = quantity * pricePerUnit,
      Status = oi.Order_Item_Status.ToString()
    };
  }
}