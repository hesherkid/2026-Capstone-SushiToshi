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

    }
}