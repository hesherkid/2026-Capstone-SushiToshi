using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using System.Linq.Expressions;
using back_end.DTO.MenuItems;
using Microsoft.AspNetCore.Authorization;
using back_end.DTO.MenuDTO;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using back_end.domain.Seeders;
using System.Security.Claims;
using back_end.DTO.SessionParticipantDTOs;
using Swashbuckle.AspNetCore.Annotations;
using back_end.Helpers;

namespace back_end.Controllers
{
    [ApiController]
    [Route("api/[controller]")]

    public class SessionParticipantController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SessionParticipantController> _logger;

        public SessionParticipantController(ApplicationDbContext context, ILogger<SessionParticipantController> logger)
        {
            _context = context;
            _logger = logger;
        }


        /// <summary>
        /// Leaves the specified dining session.
        /// </summary>
        /// <param name="session_id">The session identifier to leave.</param>
        /// <remarks>
        /// - You must currently be an active participant.  
        /// - You cannot leave if you have any non-delivered orders in the session.  
        /// - Sets <c>Left_At</c> to the current UTC time.
        /// </remarks>
        /// <response code="200">Left the session successfully.</response>
        /// <response code="401">The request is unauthenticated or the user claim is missing.</response>
        /// <response code="404">The session or user record was not found.</response>
        /// <response code="400">The user is not an active participant, or has active orders.</response>
        /// <response code="500">An unexpected error occurred while leaving the session.</response>
        [Authorize]
        [HttpPost("{session_id:int}/leave")]
        [Produces("application/json")]
        [SwaggerOperation(
            OperationId = "LeaveSession",
            Summary = "Leave a dining session",
            Description = "Marks the current user as having left the specified session, provided there are no active orders."
        )]
        public async Task<IActionResult> LeaveSession(
                int session_id
            )
        {
            try
            {

                var session = await _context.DiningSessions.FirstOrDefaultAsync(ds => ds.Session_Id == session_id);
                if (session is null)
                {
                    return NotFound("Dining Session was not found");
                }

                // Get authenticated user info
                var userIdString = ClaimsHelpers.GetUserId(User);

                if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out var userId))
                {
                    return Unauthorized("User ID not found in claims or invalid format.");
                }

                var existingParticipant = await _context.SessionParticipants
                    .FirstOrDefaultAsync(sp => sp.Session_Id == session_id && sp.User_Id == userId && !sp.Left_At.HasValue);
                if (existingParticipant is null)
                {
                    return BadRequest("You are not a active participant to the session");
                }
                var activeOrders = await _context.SessionOrders
                    .FirstOrDefaultAsync(so => so.session_id == session_id && so.User_Id == userId && so.Status != OrderStatus.Delivered);
                if (activeOrders != null)
                {
                    return BadRequest("You can not leave a active session with active orders");
                }
                existingParticipant.Left_At = DateTime.UtcNow;
                _context.SessionParticipants.Update(existingParticipant);
                await _context.SaveChangesAsync();
                return Ok("You have successfully left a session");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leaving session {SessionId}", session_id);
                return StatusCode(500, "Error leaving session");
            }
        }

    /// <summary>
    /// The bill you're on at your table, and the table's open bills you could join.
    /// </summary>
    /// <remarks>
    /// Guests pick their party's bill (or start one with POST /api/bill/create_Bill) before ordering.
    /// Other bills show only their name and party size.
    /// </remarks>
    /// <response code="200">Your bill (null if you haven't picked one) and the open bills.</response>
    /// <response code="403">You aren't seated at this table.</response>
    [Authorize]
    [HttpGet("{session_id:int}/bill")]
    [ProducesResponseType(typeof(MyBillResponseDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyBill(int session_id)
    {
        var seat = await MySeatAsync(session_id);
        if (seat is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You aren't seated at this table." });
        }

        var bills = await _context.Bills
            .Where(b => b.Session_Id == session_id && (b.Status == BillStatus.Open || b.Bill_Id == seat.Bill_Id))
            .OrderBy(b => b.Created_At)
            .ToListAsync();

        // People still seated on each bill
        var people = await _context.SessionParticipants
            .Where(sp => sp.Session_Id == session_id && sp.Left_At == null && sp.Bill_Id != null)
            .GroupBy(sp => sp.Bill_Id!.Value)
            .Select(g => new { Bill_Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Bill_Id, g => g.Count);
        int PeopleOn(Billing bill) => people.GetValueOrDefault(bill.Bill_Id);

        var myBill = bills.FirstOrDefault(b => b.Bill_Id == seat.Bill_Id);
        return Ok(new MyBillResponseDTO
        {
            My_Bill = myBill is null ? null : new MyBillDTO
            {
                Bill_Id = myBill.Bill_Id,
                Bill_Name = myBill.Bill_Name,
                Total_Count = myBill.Total_Count,
                Status = myBill.Status.ToString(),
                Requested_At = myBill.Requested_At,
                People_Count = PeopleOn(myBill),
                Adult_Count = myBill.Adult_Count,
                Senior_Count = myBill.Senior_Count,
                Child_Count = myBill.Child_Count,
                Tot_Count = myBill.Tot_Count,
            },
            Change_Blocked_Reason = await BillLinks.CantLeaveReasonAsync(_context, seat),
            Open_Bills = bills.Where(b => b.Status == BillStatus.Open).Select(b => Choice(b, PeopleOn(b))).ToList(),
        });
    }

    /// <summary>
    /// Puts you on one of your table's open bills.
    /// </summary>
    /// <remarks>
    /// Sample request:
    ///
    ///     PUT /api/sessionparticipant/123/bill
    ///     { "bill_id": 45 }
    ///
    /// You can switch bills until you've ordered on one; after that, ask staff to move you.
    /// Bills that have asked for the bill can't be joined.
    /// </remarks>
    /// <response code="200">You're on the bill; returns the same as GET.</response>
    /// <response code="400">The bill can't be joined, or you've already ordered on another bill.</response>
    /// <response code="403">You aren't seated at this table.</response>
    [Authorize]
    [HttpPut("{session_id:int}/bill")]
    [ProducesResponseType(typeof(MyBillResponseDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ChooseBill(int session_id, ChooseBillDTO request)
    {
        var seat = await MySeatAsync(session_id);
        if (seat is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You aren't seated at this table." });
        }

        if (seat.Bill_Id == request.Bill_Id)
        {
            return await GetMyBill(session_id);
        }

        var bill = await _context.Bills.FirstOrDefaultAsync(b => b.Bill_Id == request.Bill_Id);
        var cantJoin = BillLinks.CantJoinReason(bill, session_id)
            ?? await BillLinks.CantLeaveReasonAsync(_context, seat);
        if (cantJoin != null)
        {
            return BadRequest(new { message = cantJoin });
        }

        seat.Bill_Id = request.Bill_Id;
        await _context.SaveChangesAsync();
        return await GetMyBill(session_id);
    }

    /// <summary>
    /// Changes the guest counts on your bill, e.g. when more of your party arrives.
    /// </summary>
    /// <remarks>
    /// Sample request:
    ///
    ///     PUT /api/sessionparticipant/123/bill/guests
    ///     { "adult_count": 3, "senior_count": 0, "child_count": 1, "tot_count": 0 }
    ///
    /// Anyone on the bill can change it while it's open and nobody has asked for the bill.
    /// The table's open bills together can't seat more guests than the table has seats.
    /// A change marks the bill Counts_Changed_By_Guest so staff check the counts.
    /// </remarks>
    /// <response code="200">The counts are saved; returns the same as GET.</response>
    /// <response code="400">You aren't on a bill, it can't be changed, or the counts aren't valid.</response>
    /// <response code="403">You aren't seated at this table.</response>
    [Authorize]
    [HttpPut("{session_id:int}/bill/guests")]
    [ProducesResponseType(typeof(MyBillResponseDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateGuestCounts(int session_id, UpdateGuestCountsDTO request)
    {
        var seat = await MySeatAsync(session_id);
        if (seat is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You aren't seated at this table." });
        }
        if (seat.Bill_Id is null)
        {
            return BadRequest(new { message = "Choose your bill first." });
        }

        var bill = await _context.Bills
            .Include(b => b.DiningSession).ThenInclude(ds => ds.Table)
            .Include(b => b.DiningSession).ThenInclude(ds => ds.TableGroup).ThenInclude(tg => tg!.Tables)
            .FirstAsync(b => b.Bill_Id == seat.Bill_Id);
        if (bill.Status != BillStatus.Open)
        {
            return BadRequest(new { message = $"{bill.Bill_Name} is already {bill.Status.ToString().ToLower()}." });
        }
        if (bill.Requested_At != null)
        {
            return BadRequest(new { message = $"{bill.Bill_Name} has already asked for the bill. Ask your server to change the guest count." });
        }

        int total = request.Adult_Count + request.Senior_Count + request.Child_Count + request.Tot_Count;
        var problem = await BillLinks.GuestCountProblemAsync(_context, bill, total);
        if (problem != null)
        {
            return BadRequest(new { message = total == 0 ? problem : $"{problem} Ask your server if more of your party is coming." });
        }

        if (BillLinks.SetCounts(bill, request.Adult_Count, request.Senior_Count, request.Child_Count, request.Tot_Count))
        {
            bill.Counts_Changed_By_Guest = true;
            await _context.SaveChangesAsync();
        }
        return await GetMyBill(session_id);
    }

    /// <summary>
    /// Staff: moves someone at the table to another of its open bills, or onto one if they have none.
    /// </summary>
    /// <remarks>
    /// Sample request:
    ///
    ///     PUT /api/sessionparticipant/123/participants/45/bill
    ///     { "bill_id": 67, "move_orders": true }
    ///
    /// With move_orders, the orders they placed on their old bill go with them (cancelled ones
    /// stay), so their extras are charged on the new bill. Moving everyone who ordered something
    /// onto a new bill, then fixing both bills' guest counts, splits a bill by person.
    /// Orders can only move off a bill that's still open, since a closed bill's total is final.
    /// Staff can move someone even after they've ordered, and onto a bill that has asked for the bill.
    /// </remarks>
    /// <response code="200">They're on the bill.</response>
    /// <response code="400">The bill isn't open, isn't at the table, or their orders can't move.</response>
    /// <response code="403">The table is at another location.</response>
    /// <response code="404">Nobody with that seat is at the table.</response>
    [Authorize(Policy = "staffOnly")]
    [HttpPut("{session_id:int}/participants/{participant_id:int}/bill")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MoveGuest(int session_id, int participant_id, MoveGuestDTO request)
    {
        if (!await SessionAccess.CanViewSessionAsync(_context, User, session_id))
        {
            return LocationResults.OtherLocation();
        }

        var seat = await _context.SessionParticipants.FirstOrDefaultAsync(sp =>
            sp.Participant_Id == participant_id && sp.Session_Id == session_id && sp.Left_At == null);
        if (seat is null)
        {
            return NotFound(new { message = "That person isn't at this table." });
        }
        if (seat.Bill_Id == request.Bill_Id)
        {
            return Ok(new { bill_id = seat.Bill_Id, orders_moved = 0 });
        }

        var fromBillId = seat.Bill_Id;
        // Nobody can close either bill, or order onto the old one, while their orders move
        await using var billLock = await BillLock.LockAsync(_context, fromBillId is int fromId ? new[] { fromId, request.Bill_Id } : new[] { request.Bill_Id });

        var to = await _context.Bills.FirstOrDefaultAsync(b => b.Bill_Id == request.Bill_Id);
        if (to == null || to.Session_Id != session_id)
        {
            return BadRequest(new { message = "That bill isn't at this table." });
        }
        if (to.Status != BillStatus.Open)
        {
            return BadRequest(new { message = $"{to.Bill_Name} is already {to.Status.ToString().ToLower()}." });
        }

        var orders = new List<SessionOrder>();
        if (request.Move_Orders && fromBillId != null && seat.User_Id != null)
        {
            var from = await _context.Bills.AsNoTracking().FirstAsync(b => b.Bill_Id == fromBillId);
            orders = await _context.SessionOrders
                .Where(o => o.Bill_Id == fromBillId && o.User_Id == seat.User_Id && o.Status != OrderStatus.Cancelled)
                .ToListAsync();
            if (orders.Count > 0 && from.Status != BillStatus.Open)
            {
                return BadRequest(new { message = $"{from.Bill_Name} is already {from.Status.ToString().ToLower()}, so its orders can't move." });
            }
            orders.ForEach(o => o.Bill_Id = to.Bill_Id);
        }

        seat.Bill_Id = to.Bill_Id;
        await _context.SaveChangesAsync();
        if (billLock != null) await billLock.CommitAsync();

        return Ok(new { bill_id = to.Bill_Id, orders_moved = orders.Count });
    }

    // The caller's seat at this table, if they're still seated
    private async Task<SessionParticipant?> MySeatAsync(int sessionId)
    {
        if (!int.TryParse(ClaimsHelpers.GetUserId(User), out var userId)) return null;
        return await _context.SessionParticipants
            .CurrentSeats(userId)
            .FirstOrDefaultAsync(sp => sp.Session_Id == sessionId);
    }

    private static TableBillChoiceDTO Choice(Billing bill, int peopleCount) => new()
    {
        Bill_Id = bill.Bill_Id,
        Bill_Name = bill.Bill_Name,
        Total_Count = bill.Total_Count,
        Status = bill.Status.ToString(),
        Requested_At = bill.Requested_At,
        People_Count = peopleCount,
    };

    }
}