using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.DTO.MenuItemViewDTOs;
using back_end.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace back_end.Controllers
{
    [ApiController]
    [Route("api/menu-item-views")]
    [Authorize]
    public class MenuItemViewController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MenuItemViewController> _logger;

        public MenuItemViewController(ApplicationDbContext context, ILogger<MenuItemViewController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Records that a diner looked at a menu item during their dining session.
        /// </summary>
        /// <param name="request">Session, item and how many seconds the item was on screen.</param>
        /// <remarks>
        /// Sample request:
        ///
        ///     POST /api/menu-item-views
        ///     {
        ///         "session_id": 42,
        ///         "item_id": 7,
        ///         "view_seconds": 12
        ///     }
        ///
        /// Rules:
        /// - Any signed-in user, including the shared guest account. Customers must be a participant
        ///   of the session; Staff and Admin may record views for any session.
        /// - The session must still be open.
        /// - The item must be on the session's menu. Menu and location come from the session.
        /// - Every view is stored, including short ones. Reports ignore views under 5 seconds.
        /// - view_seconds above 600 is stored as 600.
        /// - Was_Available records whether the item could be ordered at the time of the view.
        /// - Rate limited per sign-in token (policy "views").
        /// </remarks>
        /// <response code="204">View recorded</response>
        /// <response code="400">Invalid body</response>
        /// <response code="401">Not signed in, or the token has no user id</response>
        /// <response code="403">The caller is not part of this session</response>
        /// <response code="404">Session not found, or the item is not on the session's menu</response>
        /// <response code="409">The session has ended</response>
        /// <response code="429">Too many views from this sign-in</response>
        /// <response code="500">Unexpected server error</response>
        [HttpPost]
        [EnableRateLimiting(ViewTrackingRules.RateLimitPolicy)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> RecordView([FromBody] MenuItemViewCreateDTO request)
        {
            if (!int.TryParse(ClaimsHelpers.GetUserId(User), out var userId))
            {
                return Unauthorized(new { message = "User ID not found in claims." });
            }

            try
            {
                var session = await _context.DiningSessions
                    .AsNoTracking()
                    .Where(s => s.Session_Id == request.SessionId)
                    .Select(s => new { s.Menu_Id, s.Location_Id, s.Ended_At })
                    .FirstOrDefaultAsync();

                if (session == null)
                {
                    return NotFound(new { message = $"Session {request.SessionId} was not found." });
                }

                if (session.Ended_At != null)
                {
                    return Conflict(new { message = "This dining session has ended." });
                }

                var isStaff = User.IsInRole("Staff") || User.IsInRole("Admin");
                if (!isStaff)
                {
                    var isParticipant = await _context.SessionParticipants
                        .AnyAsync(p => p.Session_Id == request.SessionId && p.User_Id == userId);

                    if (!isParticipant)
                    {
                        return StatusCode(StatusCodes.Status403Forbidden,
                            new { message = "You are not part of this dining session." });
                    }
                }

                var assignment = await _context.MenuItemAssignments
                    .AsNoTracking()
                    .Where(a => a.Menu_Id == session.Menu_Id && a.Item_Id == request.ItemId)
                    .Select(a => new { a.Status, ItemStatus = a.MenuItem.Status })
                    .FirstOrDefaultAsync();

                if (assignment == null)
                {
                    return NotFound(new { message = $"Item {request.ItemId} is not on this session's menu." });
                }

                var seconds = Math.Min(request.ViewSeconds, ViewTrackingRules.MaxRecordedSeconds);
                var now = DateTime.UtcNow;

                _context.MenuItemViews.Add(new MenuItemView
                {
                    Item_Id = request.ItemId,
                    Session_Id = request.SessionId,
                    Menu_Id = session.Menu_Id,
                    Location_Id = session.Location_Id,
                    User_Id = userId,
                    Viewed_At = now.AddSeconds(-seconds),
                    View_Seconds = seconds,
                    Was_Available = assignment.Status != MenuItemStatus.Unavailable
                        && assignment.ItemStatus != MenuItemStatus.Unavailable
                });

                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording view of item {ItemId} in session {SessionId}",
                    request.ItemId, request.SessionId);
                return StatusCode(500, new { message = "An error occurred while recording the item view." });
            }
        }
    }
}