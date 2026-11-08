using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using back_end.Helpers;
using back_end.Services;

namespace back_end.Controllers
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
    /// <param name="sessionId">The unique identifier of the dining session</param>
    /// <param name="billData">The bill creation data including guest counts and bill name</param>
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
    /// - A guest's bill can't take the table's open bills past its seats; staff's can,
    ///   for a party that pulled up extra chairs
    /// 
    /// The bill is automatically created with 'Open' status.
    ///
    /// Guests seated at the table can start their own bill from their phone: they're put on it,
    /// and it's marked Started_By_Guest so staff check the guest counts. A guest who has already
    /// ordered on another open bill can't start a new one (staff can move them).
    /// </remarks>
    [Authorize]
    [HttpPost("create_Bill/{sessionId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateBill(int sessionId, CreateBill billData)
    {
      try
      {
        var session = await _context.DiningSessions
            .Include(ds => ds.Table)
            .Include(ds => ds.TableGroup)
                .ThenInclude(tg => tg!.Tables)
            .FirstOrDefaultAsync(ds => ds.Session_Id == sessionId);

        if (session == null)
        {
          return NotFound(new { message = "Bill or session was not found" });
        }

        if (!await SessionAccess.CanViewSessionAsync(_context, User, session.Session_Id))
        {
          return LocationResults.OtherLocation();
        }

        if (session.Ended_At != null)
        {
          return BadRequest(new { message = $"Cannot create bill for session that ended {session.Ended_At}" });
        }

        // A guest starting their own bill is put on it
        SessionParticipant? seat = null;
        if (!SessionAccess.IsStaff(User))
        {
          var userId = int.TryParse(ClaimsHelpers.GetUserId(User), out var parsed) ? parsed : 0;
          seat = await _context.SessionParticipants.FirstOrDefaultAsync(sp =>
              sp.Session_Id == sessionId && sp.User_Id == userId && sp.Left_At == null);
          if (seat == null)
          {
            return Forbid();
          }
          var cantLeave = await BillLinks.CantLeaveReasonAsync(_context, seat);
          if (cantLeave != null)
          {
            return BadRequest(new { message = cantLeave });
          }
        }

        // Check if session has either a table or table group assigned
        if (!session.Table_Id.HasValue && !session.TableGroup_Id.HasValue)
        {
          return BadRequest(new { message = "Cannot create a bill for a session that has no table or table group assigned" });
        }

        if (billData.AdultCount < 0 || billData.SeniorCount < 0 || billData.ChildCount < 0 || billData.TotCount < 0)
        {
          return BadRequest(new { message = "Guest counts can't be negative." });
        }

        // Validate Bill_Name is required
        if (string.IsNullOrWhiteSpace(billData.BillName))
        {
          return BadRequest(new { message = "Bill_Name is required." });
        }

        int totalCount = billData.AdultCount + billData.SeniorCount + billData.ChildCount + billData.TotCount;

        if (totalCount == 0)
        {
          return BadRequest(new { message = "Bill must have at least one guest in the session" });
        }

        // Guests can't seat more than the table has; staff can, for a party that pulled up extra chairs
        if (seat != null)
        {
          int tableCapacity = BillLinks.TableCapacity(session);

          // Check total guest count across open bills in this session
          var existingBillsGuestCount = await _context.Bills
              .Where(b =>
                  b.Session_Id == sessionId &&
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
        }

        var newBill = new Billing
        {
          Session_Id = sessionId,
          Bill_Name = billData.BillName!, // safe after validation
          Senior_Count = billData.SeniorCount,
          Adult_Count = billData.AdultCount,
          Child_Count = billData.ChildCount,
          Tot_Count = billData.TotCount,
          Total_Count = totalCount,
          Status = BillStatus.Open,
          Started_By_Guest = seat != null
        };

        _context.Bills.Add(newBill);
        if (seat != null)
        {
          seat.Bill = newBill;
        }
        await _context.SaveChangesAsync();
        await _context.Entry(newBill).ReloadAsync();

        return Ok(new BillResponse
        {
          BillId = newBill.Bill_Id,
          SessionId = newBill.Session_Id,
          BillName = newBill.Bill_Name,
          SeniorCount = newBill.Senior_Count,
          AdultCount = newBill.Adult_Count,
          ChildCount = newBill.Child_Count,
          TotCount = newBill.Tot_Count,
          TotalCount = newBill.Total_Count,
          Status = newBill.Status.ToString(),
          CreatedAt = newBill.Created_At,
          ClosedAt = newBill.Closed_At,
          TableNumbers = GetTableNumbers(session)
        });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error creating bill for session {SessionId}", sessionId);
        return StatusCode(500, new { message = "Error creating bill" });
      }
    }

    /// <summary>
    /// Retrieves all bills for a specific dining session, optionally filtered by table.
    /// </summary>
    /// <param name="sessionId">The unique identifier of the dining session</param>
    /// <param name="tableId">Optional table ID to filter bills by a specific table</param>
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
    [HttpGet("get_bills/{sessionId}")]
    [Authorize]
    public async Task<IActionResult> GetBills(int sessionId, [FromQuery(Name = "_table_id")] int? tableId = null)
    {
      try
      {
        if (!await SessionAccess.CanViewSessionAsync(_context, User, sessionId))
        {
          return Forbid();
        }

        var session = await _context.DiningSessions
            .Include(ds => ds.Table)
            .Include(ds => ds.TableGroup)
                .ThenInclude(tg => tg!.Tables)
            .FirstOrDefaultAsync(ds => ds.Session_Id == sessionId);

        if (session == null)
        {
          return NotFound(new { message = "Session was not found" });
        }

        var billQuery = _context.Bills.Where(b => b.Session_Id == sessionId);

        // Customers only see their own bill; other parties' bills are private
        if (!SessionAccess.IsStaff(User))
        {
          var myBillId = await SessionAccess.MyBillIdAsync(_context, User, sessionId);
          billQuery = billQuery.Where(b => b.Bill_Id == myBillId);
        }

        // If table_id is specified, validate it belongs to the session
        if (tableId.HasValue)
        {
          bool tableInSession = false;

          // Check if it's the directly assigned table
          if (session.Table_Id == tableId.Value)
          {
            tableInSession = true;
          }
          // Check if it's in the table group
          else if (session.TableGroup?.Tables.Any(t => t.Table_Id == tableId.Value) == true)
          {
            tableInSession = true;
          }

          if (!tableInSession)
          {
            return BadRequest(new { message = $"Table with ID {tableId.Value} is not associated with session {sessionId}" });
          }
        }

        // A session with no bills yet is normal (e.g. just seated), so return an empty list
        var bills = billQuery.OrderByDescending(b => b.Created_At).ToList();

        var billResponses = bills.Select(bill => new BillResponse
        {
          BillId = bill.Bill_Id,
          SessionId = bill.Session_Id,
          BillName = bill.Bill_Name ?? string.Empty,
          SeniorCount = bill.Senior_Count,
          AdultCount = bill.Adult_Count,
          ChildCount = bill.Child_Count,
          TotCount = bill.Tot_Count,
          TotalCount = bill.Total_Count,
          Status = bill.Status.ToString(),
          CreatedAt = bill.Created_At,
          ClosedAt = bill.Closed_At,
          RequestedAt = bill.Requested_At,
          TableNumbers = GetTableNumbers(session)
        }).ToList();

        return Ok(billResponses);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting bill for session {SessionId}", sessionId);
        return StatusCode(500, new { message = "Error getting bill" });
      }
    }

    /// <summary>
    /// Retrieves detailed information about a specific bill.
    /// </summary>
    /// <param name="sessionId">The unique identifier of the dining session</param>
    /// <param name="billId">The unique identifier of the bill</param>
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
    [HttpGet("get_bill/{billId}")]
    [Authorize]
    public async Task<IActionResult> GetBill([FromQuery(Name = "_session_id")] int sessionId, int billId)
    {
      try
      {
        if (!await SessionAccess.CanUseBillAsync(_context, User, sessionId, billId))
        {
          return Forbid();
        }

        var bill = await _context.Bills
            .Include(b => b.DiningSession)
                .ThenInclude(ds => ds.Table)
            .Include(b => b.DiningSession)
                .ThenInclude(ds => ds.TableGroup)
                    .ThenInclude(tg => tg!.Tables)
            .FirstOrDefaultAsync(b => b.Bill_Id == billId && b.Session_Id == sessionId);

        if (bill == null)
        {
          return NotFound(new { message = "Bill or session was not found" });
        }

        return Ok(new BillResponse
        {
          BillId = bill.Bill_Id,
          SessionId = bill.Session_Id,
          BillName = bill.Bill_Name ?? string.Empty,
          SeniorCount = bill.Senior_Count,
          AdultCount = bill.Adult_Count,
          ChildCount = bill.Child_Count,
          TotCount = bill.Tot_Count,
          TotalCount = bill.Total_Count,
          Status = bill.Status.ToString(),
          CreatedAt = bill.Created_At,
          ClosedAt = bill.Closed_At,
          RequestedAt = bill.Requested_At,
          TableNumbers = GetTableNumbers(bill.DiningSession)
        });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting bill for session {SessionId}", sessionId);
        return StatusCode(500, new { message = "Error getting bill" });
      }
    }

    /// <summary>
    /// Staff: changes an open bill's guest counts.
    /// </summary>
    /// <remarks>
    /// Sample request:
    ///
    ///     PUT /api/bill/guest_counts/123?_session_id=456
    ///     { "adult_count": 3, "senior_count": 0, "child_count": 1, "tot_count": 0 }
    ///
    /// At least one guest. Can go past the table's seats, for a party that pulled up extra chairs.
    /// Works after the guests have asked for the bill.
    /// Staff have now checked the counts, so the bill's Started_By_Guest and
    /// Counts_Changed_By_Guest flags are cleared.
    /// </remarks>
    /// <response code="200">The bill with its new counts.</response>
    /// <response code="400">The bill isn't open, or the counts aren't valid.</response>
    /// <response code="403">The table is at another location.</response>
    /// <response code="404">The bill or session was not found.</response>
    [Authorize(Policy = "staffOnly")]
    [HttpPut("guest_counts/{billId}")]
    [ProducesResponseType(typeof(BillResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateGuestCounts([FromQuery(Name = "_session_id")] int sessionId, int billId, UpdateGuestCountsDto counts)
    {
      try
      {
        // So nobody can close the bill, or change its count, while this saves
        await using var billLock = await BillLock.LockAsync(_context, billId);
        var bill = await _context.Bills
            .Include(b => b.DiningSession).ThenInclude(ds => ds.Table)
            .Include(b => b.DiningSession).ThenInclude(ds => ds.TableGroup).ThenInclude(tg => tg!.Tables)
            .FirstOrDefaultAsync(b => b.Bill_Id == billId && b.Session_Id == sessionId);
        if (bill == null)
        {
          return NotFound(new { message = "Bill or session was not found" });
        }
        if (!await SessionAccess.CanViewSessionAsync(_context, User, bill.Session_Id))
        {
          return LocationResults.OtherLocation();
        }
        if (bill.Status != BillStatus.Open)
        {
          return BadRequest(new { message = $"Only open bills can be changed (this bill is {bill.Status})." });
        }

        int total = counts.AdultCount + counts.SeniorCount + counts.ChildCount + counts.TotCount;
        var problem = await BillLinks.GuestCountProblemAsync(_context, bill, total, byStaff: true);
        if (problem != null)
        {
          return BadRequest(new { message = problem });
        }

        BillLinks.SetCounts(bill, counts.AdultCount, counts.SeniorCount, counts.ChildCount, counts.TotCount);
        bill.Started_By_Guest = false;
        bill.Counts_Changed_By_Guest = false;
        await _context.SaveChangesAsync();
        if (billLock != null) await billLock.CommitAsync();

        return Ok(new BillResponse
        {
          BillId = bill.Bill_Id,
          SessionId = bill.Session_Id,
          BillName = bill.Bill_Name,
          SeniorCount = bill.Senior_Count,
          AdultCount = bill.Adult_Count,
          ChildCount = bill.Child_Count,
          TotCount = bill.Tot_Count,
          TotalCount = bill.Total_Count,
          Status = bill.Status.ToString(),
          CreatedAt = bill.Created_At,
          ClosedAt = bill.Closed_At,
          TableNumbers = GetTableNumbers(bill.DiningSession)
        });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error changing guest counts on bill {BillId}", billId);
        return StatusCode(500, new { message = "Error changing guest counts" });
      }
    }

    /// <summary>
    /// Staff: locks an open bill's guest counts so guests can't change them from their phones, or unlocks them.
    /// </summary>
    /// <remarks>
    /// Sample request:
    ///
    ///     PUT /api/bill/lock_guest_counts/123?_session_id=456
    ///     { "locked": true }
    ///
    /// Locking means staff have checked the counts, so it also clears the bill's Started_By_Guest
    /// and Counts_Changed_By_Guest flags. Staff can still change locked counts with Edit Guests.
    /// </remarks>
    /// <response code="200">The bill is locked or unlocked.</response>
    /// <response code="400">The bill isn't open.</response>
    /// <response code="403">The table is at another location.</response>
    /// <response code="404">The bill or session was not found.</response>
    [Authorize(Policy = "staffOnly")]
    [HttpPut("lock_guest_counts/{billId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LockGuestCounts([FromQuery(Name = "_session_id")] int sessionId, int billId, LockGuestCountsDto request)
    {
      try
      {
        // So a guest's change can't save in between
        await using var billLock = await BillLock.LockAsync(_context, billId);
        var bill = await _context.Bills
            .FirstOrDefaultAsync(b => b.Bill_Id == billId && b.Session_Id == sessionId);
        if (bill == null)
        {
          return NotFound(new { message = "Bill or session was not found" });
        }
        if (!await SessionAccess.CanViewSessionAsync(_context, User, bill.Session_Id))
        {
          return LocationResults.OtherLocation();
        }
        if (bill.Status != BillStatus.Open)
        {
          return BadRequest(new { message = $"Only open bills can be changed (this bill is {bill.Status})." });
        }

        bill.Counts_Locked = request.Locked;
        if (request.Locked)
        {
          bill.Started_By_Guest = false;
          bill.Counts_Changed_By_Guest = false;
        }
        await _context.SaveChangesAsync();
        if (billLock != null) await billLock.CommitAsync();

        return Ok(new { bill_id = bill.Bill_Id, counts_locked = bill.Counts_Locked });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error locking guest counts on bill {BillId}", billId);
        return StatusCode(500, new { message = "Error locking guest counts" });
      }
    }

    /// <summary>
    /// Staff: merges an open bill into another open bill at the same table, e.g. when everyone wants one bill.
    /// </summary>
    /// <remarks>
    /// Sample request:
    ///
    ///     PUT /api/bill/merge/123?_session_id=456
    ///     { "into_bill_id": 124 }
    ///
    /// Everything on bill 123 moves to bill 124: everyone on it, all of its orders that aren't
    /// cancelled (including ones staff placed and ones from people who have left), its leftover
    /// fee, and its guest counts, so guests who never joined on a phone move too. The table's total
    /// doesn't change, so the seat limit still holds. Bill 123 is then cancelled, with nothing on it.
    /// </remarks>
    /// <response code="200">The bill everything moved to, with its new counts.</response>
    /// <response code="400">Either bill isn't open, they're the same bill, or the other bill isn't at this table.</response>
    /// <response code="403">The table is at another location.</response>
    /// <response code="404">The bill or session was not found.</response>
    [Authorize(Policy = "staffOnly")]
    [HttpPut("merge/{billId}")]
    [ProducesResponseType(typeof(BillResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MergeBill([FromQuery(Name = "_session_id")] int sessionId, int billId, MergeBillDto request)
    {
      try
      {
        if (request.IntoBillId == billId)
        {
          return BadRequest(new { message = "Pick another bill to merge into." });
        }

        // Nobody can order onto, close or change either bill while everything moves
        await using var billLock = await BillLock.LockAsync(_context, billId, request.IntoBillId);
        var from = await _context.Bills
            .FirstOrDefaultAsync(b => b.Bill_Id == billId && b.Session_Id == sessionId);
        if (from == null)
        {
          return NotFound(new { message = "Bill or session was not found" });
        }
        if (!await SessionAccess.CanViewSessionAsync(_context, User, from.Session_Id))
        {
          return LocationResults.OtherLocation();
        }

        var into = await _context.Bills
            .Include(b => b.DiningSession).ThenInclude(ds => ds.Table)
            .Include(b => b.DiningSession).ThenInclude(ds => ds.TableGroup).ThenInclude(tg => tg!.Tables)
            .FirstOrDefaultAsync(b => b.Bill_Id == request.IntoBillId);
        if (into == null || into.Session_Id != sessionId)
        {
          return BadRequest(new { message = "That bill isn't at this table." });
        }
        foreach (var bill in new[] { from, into })
        {
          if (bill.Status != BillStatus.Open)
          {
            return BadRequest(new { message = $"{bill.Bill_Name} is already {bill.Status.ToString().ToLower()}." });
          }
        }

        var orders = await _context.SessionOrders
            .Where(o => o.Bill_Id == from.Bill_Id && o.Status != OrderStatus.Cancelled)
            .ToListAsync();
        orders.ForEach(o => o.Bill_Id = into.Bill_Id);

        // Everyone who was on it, including people who have left, so nothing points at the cancelled bill
        var seats = await _context.SessionParticipants
            .Where(sp => sp.Session_Id == sessionId && sp.Bill_Id == from.Bill_Id)
            .ToListAsync();
        seats.ForEach(sp => sp.Bill_Id = into.Bill_Id);

        BillLinks.SetCounts(into,
            into.Adult_Count + from.Adult_Count,
            into.Senior_Count + from.Senior_Count,
            into.Child_Count + from.Child_Count,
            into.Tot_Count + from.Tot_Count);
        into.Leftover_Charge += from.Leftover_Charge;
        from.Leftover_Charge = 0m;

        // Cancelled bills don't count towards guests or turnover, so its counts can stay as a record
        from.Status = BillStatus.Cancelled;
        from.Closed_At = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        if (billLock != null) await billLock.CommitAsync();

        return Ok(new BillResponse
        {
          BillId = into.Bill_Id,
          SessionId = into.Session_Id,
          BillName = into.Bill_Name,
          SeniorCount = into.Senior_Count,
          AdultCount = into.Adult_Count,
          ChildCount = into.Child_Count,
          TotCount = into.Tot_Count,
          TotalCount = into.Total_Count,
          Status = into.Status.ToString(),
          CreatedAt = into.Created_At,
          ClosedAt = into.Closed_At,
          RequestedAt = into.Requested_At,
          TableNumbers = GetTableNumbers(into.DiningSession)
        });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error merging bill {BillId} into {IntoBillId}", billId, request.IntoBillId);
        return StatusCode(500, new { message = "Error merging bills" });
      }
    }

    /// <summary>
    /// Closes an open bill after verifying all orders are completed.
    /// </summary>
    /// <param name="sessionId">The unique identifier of the dining session</param>
    /// <param name="billId">The unique identifier of the bill to close</param>
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
    [Authorize(Policy = "staffOnly")]
    [HttpPut("close_bill/{billId}")]
    public async Task<IActionResult> CloseBill([FromQuery(Name = "_session_id")] int sessionId, int billId)
    {
      try
      {
        // Waits for an order being placed on this bill, so the pending order check below sees it
        await using var billLock = await BillLock.LockAsync(_context, billId);

        var bill = await _context.Bills
            .Include(b => b.DiningSession)
                .ThenInclude(ds => ds.Table)
            .Include(b => b.DiningSession)
                .ThenInclude(ds => ds.TableGroup)
                    .ThenInclude(tg => tg!.Tables)
            .FirstOrDefaultAsync(b => b.Bill_Id == billId && b.Session_Id == sessionId);

        if (bill == null)
        {
          return NotFound(new { message = "Bill or session was not found" });
        }

        if (!await SessionAccess.CanViewSessionAsync(_context, User, bill.Session_Id))
        {
          return LocationResults.OtherLocation();
        }

        if (bill.Status == BillStatus.Closed)
        {
          return BadRequest(new { message = "Bill is already closed" });
        }

        if (bill.Status != BillStatus.Open)
        {
          return BadRequest(new { message = $"Only open bills can be closed (this bill is {bill.Status})." });
        }

        var pendingOrders = await _context.SessionOrders
            .Where(o => o.Bill_Id == billId && o.Status == OrderStatus.Pending)
            .ToListAsync();

        if (pendingOrders.Any())
        {
          return BadRequest(new { message = "Cannot close bill with pending orders" });
        }

        // Freeze this bill's prices, so later pricing changes don't alter a closed bill
        var pricing = await _pricingService.GetPricingAsync(bill.DiningSession.Location_Id, bill.Created_At);
        var partySize = await PartySizeAsync(sessionId);
        BillPriceSnapshot.Freeze(bill, pricing, BillCalculator.GratuityRateFor(pricing, partySize));

        bill.Status = BillStatus.Closed;
        bill.Closed_At = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        if (billLock != null) await billLock.CommitAsync();
        await _context.Entry(bill).ReloadAsync();

        return Ok(new BillResponse
        {
          BillId = bill.Bill_Id,
          SessionId = bill.Session_Id,
          BillName = bill.Bill_Name ?? string.Empty,
          SeniorCount = bill.Senior_Count,
          AdultCount = bill.Adult_Count,
          ChildCount = bill.Child_Count,
          TotCount = bill.Tot_Count,
          TotalCount = bill.Total_Count,
          Status = bill.Status.ToString(),
          CreatedAt = bill.Created_At,
          ClosedAt = bill.Closed_At,
          RequestedAt = bill.Requested_At,
          TableNumbers = GetTableNumbers(bill.DiningSession)
        });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error closing bill {BillId} for session {SessionId}", billId, sessionId);
        return StatusCode(500, new { message = "Error closing bill" });
      }
    }

    /// <summary>
    /// The table's leftover fee: the default amount, and each open bill's current share.
    /// </summary>
    /// <param name="sessionId">The dining session (table)</param>
    /// <response code="200">The default fee, the current total and each open bill's share</response>
    /// <response code="404">If the session doesn't exist</response>
    /// <response code="500">If there was an error retrieving the leftover fee</response>
    /// <remarks>
    /// Used to fill in the "Add leftover fee" dialog before staff apply it.
    /// </remarks>
    [HttpGet("leftover_fee/{sessionId}")]
    [Authorize(Policy = "staffOnly")]
    [ProducesResponseType(typeof(LeftoverFeeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetLeftoverFee(int sessionId)
    {
      try
      {
        var locationId = await SessionLocationAsync(sessionId);
        if (locationId == null)
        {
          return NotFound(new { message = "Session was not found" });
        }

        if (!await SessionAccess.CanViewSessionAsync(_context, User, sessionId))
        {
          return LocationResults.OtherLocation();
        }

        var openBills = await OpenBillsAsync(sessionId);
        var pricing = await _pricingService.GetCurrentPricingAsync(locationId.Value);
        return Ok(LeftoverFeeResult(sessionId, openBills, pricing.LeftoverFee));
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting leftover fee for session {SessionId}", sessionId);
        return StatusCode(500, new { message = "Error getting leftover fee" });
      }
    }

    /// <summary>
    /// Applies a leftover fee to the table, split evenly across its open bills.
    /// </summary>
    /// <param name="sessionId">The dining session (table)</param>
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
    [HttpPut("leftover_fee/{sessionId}")]
    [Authorize(Policy = "staffOnly")]
    [ProducesResponseType(typeof(LeftoverFeeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApplyLeftoverFee(int sessionId, [FromBody] LeftoverFeeRequest request)
    {
      try
      {
        if (!await _context.DiningSessions.AnyAsync(s => s.Session_Id == sessionId))
        {
          return NotFound(new { message = "Session was not found" });
        }

        if (!await SessionAccess.CanViewSessionAsync(_context, User, sessionId))
        {
          return LocationResults.OtherLocation();
        }
        var locationId = await SessionLocationAsync(sessionId);
        if (locationId == null)
        {
          return NotFound(new { message = "Session was not found" });
        }
        // The location's default fee unless staff entered an amount
        var defaultFee = (await _pricingService.GetCurrentPricingAsync(locationId.Value)).LeftoverFee;
        var amount = request?.Amount ?? defaultFee;
        if (amount < 0)
        {
          return BadRequest(new { message = "The leftover fee can't be negative." });
        }

        var openBills = await OpenBillsAsync(sessionId);
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
            amount, sessionId, openBills.Count);

        return Ok(LeftoverFeeResult(sessionId, openBills, defaultFee));
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error applying leftover fee to session {SessionId}", sessionId);
        return StatusCode(500, new { message = "Error applying leftover fee" });
      }
    }

    // Open bills in a fixed order (oldest first), so any extra cent always lands on the same bill
    private Task<List<Billing>> OpenBillsAsync(int sessionId) =>
        _context.Bills
            .Where(b => b.Session_Id == sessionId && b.Status == BillStatus.Open)
            .OrderBy(b => b.Bill_Id)
            .ToListAsync();

    // The session's location, or null if the session doesn't exist
    private Task<int?> SessionLocationAsync(int sessionId) =>
        _context.DiningSessions
            .Where(s => s.Session_Id == sessionId)
            .Select(s => (int?)s.Location_Id)
            .FirstOrDefaultAsync();

    // Everyone at the table, so a big group splitting into small bills still gets the gratuity on each one
    private Task<int> PartySizeAsync(int sessionId) => BillTotalQuery.PartySizeAsync(_context, sessionId);

    private static LeftoverFeeResponse LeftoverFeeResult(int sessionId, List<Billing> openBills, decimal defaultFee) => new()
    {
      SessionId = sessionId,
      DefaultFee = defaultFee,
      Amount = openBills.Sum(b => b.Leftover_Charge),
      Bills = openBills.Select(b => new LeftoverFeeBillShare
      {
        BillId = b.Bill_Id,
        BillName = b.Bill_Name ?? string.Empty,
        LeftoverCharge = b.Leftover_Charge,
      }).ToList(),
    };

    /// <summary>
    /// Guests ask for their bill ("Ready for Bill").
    /// </summary>
    /// <remarks>
    /// Marks the bill as requested, which stops any more orders being placed on it, and sends staff a
    /// "Ready for bill" service request. Asking again is safe: it returns the original request time.
    /// Only guests on this bill (or staff) can ask.
    /// </remarks>
    /// <response code="200">The bill is requested; returns when it was first requested.</response>
    /// <response code="400">The bill isn't open.</response>
    /// <response code="403">The caller isn't on this bill.</response>
    /// <response code="404">The bill or session was not found.</response>
    [HttpPost("request_bill/{billId}")]
    [Authorize]
    public async Task<IActionResult> RequestBill([FromQuery(Name = "_session_id")] int sessionId, int billId)
    {
      try
      {
        if (!await SessionAccess.CanUseBillAsync(_context, User, sessionId, billId))
        {
          return Forbid();
        }

        var bill = await _context.Bills
            .Include(b => b.DiningSession).ThenInclude(ds => ds.TableGroup).ThenInclude(tg => tg!.Tables)
            .FirstOrDefaultAsync(b => b.Bill_Id == billId && b.Session_Id == sessionId);
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
          Notes = $"{back_end.Controllers.ServiceRequestController.BillRequestNotePrefix} {bill.Bill_Name}",
          Status = ServiceRequestStatus.Pending,
        });
        await _context.SaveChangesAsync();

        return Ok(new { requested_at = bill.Requested_At, already_requested = false });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error requesting bill {BillId} for session {SessionId}", billId, sessionId);
        return StatusCode(500, new { message = "Error requesting bill" });
      }
    }

    /// <summary>
    /// Cancels an open bill and all associated pending orders.
    /// </summary>
    /// <param name="sessionId">The unique identifier of the dining session</param>
    /// <param name="billId">The unique identifier of the bill to cancel</param>
    /// <returns>
    /// An <see cref="IActionResult"/> containing the cancelled <see cref="BillResponse"/> object.
    /// Returns HTTP 200 (OK) with the cancelled bill details on success.
    /// Returns HTTP 400 (Bad Request) if the bill is not in Open status, or has orders being made or served.
    /// Returns HTTP 404 (Not Found) if the bill or session doesn't exist.
    /// Returns HTTP 500 (Internal Server Error) if an exception occurs during cancellation.
    /// </returns>
    /// <response code="200">Returns the cancelled bill details</response>
    /// <response code="400">If the bill cannot be cancelled (not in Open status, or has orders being made or served)</response>
    /// <response code="404">If the bill or session is not found</response>
    /// <response code="500">If an internal error occurs while cancelling the bill</response>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/bill/cancel_bill/123?_session_id=456
    ///
    /// Cancels the specified bill and automatically cancels all pending orders associated with it.
    /// Only bills with 'Open' status can be cancelled, and only if none of their orders are being made
    /// or have been served: those still have to be paid for, so merge or close the bill instead.
    /// The bill's closed_at timestamp is set to the current UTC time.
    /// </remarks>
    [Authorize(Policy = "staffOnly")]
    [HttpPost("cancel_bill/{billId}")]
    public async Task<IActionResult> CancelBill([FromQuery(Name = "_session_id")] int sessionId, int billId)
    {
      try
      {
        await using var billLock = await BillLock.LockAsync(_context, billId);
        var bill = await _context.Bills
            .Include(b => b.DiningSession)
                .ThenInclude(ds => ds.Table)
            .Include(b => b.DiningSession)
                .ThenInclude(ds => ds.TableGroup)
                    .ThenInclude(tg => tg!.Tables)
            .FirstOrDefaultAsync(b => b.Bill_Id == billId && b.Session_Id == sessionId);

        if (bill == null)
        {
          return NotFound(new { message = "Bill or session was not found" });
        }

        if (!await SessionAccess.CanViewSessionAsync(_context, User, bill.Session_Id))
        {
          return LocationResults.OtherLocation();
        }

        if (bill.Status != BillStatus.Open)
        {
          return BadRequest(new { message = $"Cannot cancel bill with status {bill.Status}" });
        }

        // Food that's being made or has gone out still has to be paid for, and a cancelled bill is never charged
        var madeOrders = await _context.SessionOrders
            .CountAsync(o => o.Bill_Id == billId && (o.Status == OrderStatus.Processing || o.Status == OrderStatus.Delivered));
        if (madeOrders > 0)
        {
          return BadRequest(new
          {
            message = $"{bill.Bill_Name} has {madeOrders} order{(madeOrders == 1 ? "" : "s")} being made or served. " +
              "Merge it into another bill, or close it, instead of cancelling it."
          });
        }

        var pendingOrders = await _context.SessionOrders
            .Where(o => o.Bill_Id == billId && (o.Status == OrderStatus.Pending))
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
        if (billLock != null) await billLock.CommitAsync();
        await _context.Entry(bill).ReloadAsync();

        return Ok(new BillResponse
        {
          BillId = bill.Bill_Id,
          SessionId = bill.Session_Id,
          BillName = bill.Bill_Name ?? string.Empty,
          SeniorCount = bill.Senior_Count,
          AdultCount = bill.Adult_Count,
          ChildCount = bill.Child_Count,
          TotCount = bill.Tot_Count,
          TotalCount = bill.Total_Count,
          Status = bill.Status.ToString(),
          CreatedAt = bill.Created_At,
          ClosedAt = bill.Closed_At,
          RequestedAt = bill.Requested_At,
          TableNumbers = GetTableNumbers(bill.DiningSession)
        });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error cancelling bill {BillId} for session {SessionId}", billId, sessionId);
        return StatusCode(500, new { message = "Error cancelling bill" });
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
                x.participant.Bill_Id == x.bill.Bill_Id && // Only the bill they're on
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
          BillId = bill.Bill_Id,
          SessionId = bill.Session_Id,
          BillName = bill.Bill_Name ?? string.Empty,
          SeniorCount = bill.Senior_Count,
          AdultCount = bill.Adult_Count,
          ChildCount = bill.Child_Count,
          TotCount = bill.Tot_Count,
          TotalCount = bill.Total_Count,
          Status = bill.Status.ToString(),
          CreatedAt = bill.Created_At,
          ClosedAt = bill.Closed_At,
          RequestedAt = bill.Requested_At,
          TableNumbers = GetTableNumbers(bill.DiningSession)
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
    [HttpGet("summary/{billId}")]
    [ProducesResponseType(typeof(BillSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [Authorize]
    public async Task<IActionResult> GetBillSummary([FromQuery(Name = "_session_id")] int sessionId, int billId)
    {
      try
      {
        // Staff, or customers on this bill (see SessionAccess)
        if (!await SessionAccess.CanUseBillAsync(_context, User, sessionId, billId))
        {
          return Forbid();
        }

        var bill = await _context.Bills
            .Include(b => b.DiningSession.Table)
            .Include(b => b.DiningSession.TableGroup)
                .ThenInclude(tg => tg!.Tables)
            .FirstOrDefaultAsync(b => b.Bill_Id == billId && b.Session_Id == sessionId);

        if (bill == null)
        {
          return NotFound(new { message = "Bill or session was not found" });
        }

        // Closed bills use the prices frozen when they closed; open bills use the current prices
        // for the bill's location on the day the bill was created
        var frozen = BillPriceSnapshot.Read(bill);
        var pricing = frozen
          ?? await _pricingService.GetPricingAsync(bill.DiningSession.Location_Id, bill.Created_At);

        decimal baseChargesSubtotal = BillCalculator.BaseCharges(bill, pricing);

        // Charged lines for this bill (cancelled orders and cancelled items are excluded).
        // Prices are snapshots on each order line, so later price changes don't alter old bills.
        var chargedLines = await BillTotalQuery.ChargedLines(_context, billId)
          .Include(oi => oi.MenuItem)
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

        decimal addOnSubtotal = addOnItems.Sum(item => item.LineTotal);
        decimal overLimitSubtotal = overLimitItems.Sum(item => item.LineTotal);
        decimal leftoverCharge = bill.Leftover_Charge;   // this bill's share of the table's leftover fee
        decimal subtotal = baseChargesSubtotal + addOnSubtotal + overLimitSubtotal + leftoverCharge;

        // A closed bill keeps the gratuity it closed with, even if the table's party size changes later
        var partySize = await PartySizeAsync(sessionId);
        var gratuityRate = frozen?.GratuityRate ?? BillCalculator.GratuityRateFor(pricing, partySize);
        var totals = BillCalculator.Totals(subtotal, gratuityRate, pricing.TaxRate);


        var summary = new BillSummaryResponse
        {
          BillId = bill.Bill_Id,
          SessionId = bill.Session_Id,
          BillName = bill.Bill_Name ?? string.Empty,

          // Guest counts
          AdultCount = bill.Adult_Count,
          SeniorCount = bill.Senior_Count,
          ChildCount = bill.Child_Count,
          TotCount = bill.Tot_Count,
          TotalCount = bill.Total_Count,

          // Base pricing (day-specific)
          AdultBasePrice = pricing.AdultBasePrice,
          SeniorBasePrice = pricing.SeniorBasePrice,
          ChildBasePrice = pricing.ChildBasePrice,
          ToddlerBasePrice = pricing.ToddlerBasePrice,
          BaseChargesSubtotal = baseChargesSubtotal,

          // Add-ons
          AddOnItems = addOnItems,
          AddOnSubtotal = addOnSubtotal,

          // Over-limit and leftover charges
          OverLimitItems = overLimitItems,
          OverLimitSubtotal = overLimitSubtotal,
          LeftoverCharge = leftoverCharge,

          // Totals
          Subtotal = subtotal,
          PartySize = partySize,
          GratuityRate = gratuityRate,
          GratuityAmount = totals.Gratuity,
          GratuityMinPartySize = pricing.GratuityMinPartySize,
          TaxRate = pricing.TaxRate,
          TaxAmount = totals.Tax,
          TotalAmount = totals.Total,

          // Status
          Status = bill.Status.ToString(),
          PricingType = pricing.PricingType,
          HolidayName = pricing.HolidayName,
          PricesLocked = frozen != null,
          CreatedAt = bill.Created_At,
          ClosedAt = bill.Closed_At,
          RequestedAt = bill.Requested_At,
          TableNumbers = GetTableNumbers(bill.DiningSession)
        };

        _logger.LogInformation(
            $"Generated {pricing.PricingType} bill summary for Bill {billId}: " +
            $"Total ${totals.Total:F2} (Base: ${baseChargesSubtotal:F2}, Add-ons: ${addOnSubtotal:F2}, " +
            $"Over-limit: ${overLimitSubtotal:F2}, Leftovers: ${leftoverCharge:F2}, " +
            $"Gratuity: ${totals.Gratuity:F2}, GST: ${totals.Tax:F2})");


        return Ok(summary);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error calculating bill summary for Bill {BillId}", billId);
        return StatusCode(500, new { message = "Error calculating bill summary" });
      }
    }

    private static BillAddOnItem ToBillItem(OrderItems oi, int quantity, decimal pricePerUnit) => new BillAddOnItem
    {
      OrderItemId = oi.Order_Item_Id,
      ItemId = oi.Item_Id,
      ItemName = oi.MenuItem?.Name ?? string.Empty,
      Quantity = quantity,
      PricePerUnit = pricePerUnit,
      LineTotal = quantity * pricePerUnit,
      Status = oi.Order_Item_Status.ToString()
    };
  }
}