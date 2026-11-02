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
                if(existingParticipant is null)
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

        var myBill = bills.FirstOrDefault(b => b.Bill_Id == seat.Bill_Id);
        return Ok(new MyBillResponseDTO
        {
            My_Bill = myBill is null ? null : Choice(myBill),
            Change_Blocked_Reason = await BillLinks.CantLeaveReasonAsync(_context, seat),
            Open_Bills = bills.Where(b => b.Status == BillStatus.Open).Select(Choice).ToList(),
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

    // The caller's seat at this table, if they're still seated
    private async Task<SessionParticipant?> MySeatAsync(int sessionId)
    {
        if (!int.TryParse(ClaimsHelpers.GetUserId(User), out var userId)) return null;
        return await _context.SessionParticipants
            .CurrentSeats(userId)
            .FirstOrDefaultAsync(sp => sp.Session_Id == sessionId);
    }

    private static TableBillChoiceDTO Choice(Billing bill) => new()
    {
        Bill_Id = bill.Bill_Id,
        Bill_Name = bill.Bill_Name,
        Total_Count = bill.Total_Count,
        Status = bill.Status.ToString(),
        Requested_At = bill.Requested_At,
    };

    }
}