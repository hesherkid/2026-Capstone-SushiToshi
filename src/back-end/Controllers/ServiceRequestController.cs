using back_end.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using Microsoft.AspNetCore.Authorization;
using back_end.Dtos;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using back_end.domain.enums;
using back_end.Services;

namespace back_end.Controllers
{
  [ApiController]
  [Route("api/[controller]")]
  public class ServiceRequestController : ControllerBase
  {
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ServiceRequestController> _logger;
    private readonly ILocationAccessService _locationAccess;

    // How long a table waits between service requests
    public static readonly TimeSpan RequestCooldown = TimeSpan.FromSeconds(30);
    // BillController writes bill requests with this prefix
    public const string BillRequestNotePrefix = "Ready for bill:";

    public ServiceRequestController(ApplicationDbContext context, ILogger<ServiceRequestController> logger, ILocationAccessService locationAccess)
    {
      _context = context;
      _logger = logger;
      _locationAccess = locationAccess;
    }

    /// <summary>
    /// Creates a new service request for an active dining session.
    /// </summary>
    /// <param name="sessionId">The unique identifier of the dining session</param>
    /// <param name="requestData">The service request data containing notes and other details</param>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/servicerequest/123
    ///     {
    ///         "notes": "Need water refill"
    ///     }
    ///
    /// - The caller must be seated at the table (joined and not left).
    /// - One request per table every 30 seconds, whoever sends it. "Ready for bill" requests
    ///   don't count toward this; they have their own check in BillController.
    /// - The request starts as Pending.
    /// </remarks>
    /// <response code="200">The new service request.</response>
    /// <response code="400">The caller isn't seated at this table, the account wasn't found, or the
    /// visit has no table assigned.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="404">The session doesn't exist or has ended.</response>
    /// <response code="429">The table sent a request in the last 30 seconds; retry_after_seconds (and the
    /// Retry-After header) say how long to wait.</response>
    /// <response code="500">The request couldn't be saved.</response>
    [Authorize]
    [HttpPost("{sessionId}")]
    [ProducesResponseType(typeof(ServiceRequestResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateServiceRequest(
            int sessionId,
            ServiceRequestCreateDto requestData
        )
    {
      try
      {
        var session = await _context.DiningSessions
            .Include(ds => ds.Table)
            .Include(ds => ds.TableGroup)
              .ThenInclude(tg => tg!.Tables)
            .FirstOrDefaultAsync(ds => ds.Session_Id == sessionId && ds.Ended_At == null);

        if (session is null)
        {
          return NotFound("The Session Id was not found");
        }

        if (!int.TryParse(ClaimsHelpers.GetUserId(User), out var userId))
        {
          return Unauthorized(new { message = "User ID not found in claims." });
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.User_id == userId);
        if (user is null)
        {
          return BadRequest("Issue in processing your request");
        }

        var participant = await _context.SessionParticipants
            .FirstOrDefaultAsync(sp => sp.Session_Id == sessionId &&
                                      sp.User_Id == user.User_id &&
                                      sp.Left_At == null);
        if (participant is null)
        {
          return BadRequest("Issue in processing your request for session");
        }

        // Determine the table ID for the service request
        int? tableId = null;
        if (session.Table_Id.HasValue)
        {
          // Session has a single table assigned
          tableId = session.Table_Id.Value;
        }
        else if (session.TableGroup is { Tables.Count: > 0 } group)
        {
          tableId = group.Tables.First().Table_Id;
        }

        if (!tableId.HasValue)
        {
          return BadRequest("Session does not have a table or table group assigned");
        }

        // One cooldown per table, so guests on different phones can't take turns sending.
        // Asking for the bill has its own once-only check and doesn't count here.
        var lastRequestAt = await _context.ServiceRequests
            .Where(sr => sr.Session_Id == sessionId && !sr.Notes.StartsWith(BillRequestNotePrefix))
            .OrderByDescending(sr => sr.Created_At)
            .Select(sr => (DateTime?)sr.Created_At)
            .FirstOrDefaultAsync();
        if (lastRequestAt.HasValue)
        {
          var waitFor = lastRequestAt.Value.Add(RequestCooldown) - DateTime.UtcNow;
          if (waitFor > TimeSpan.Zero)
          {
            var retryAfterSeconds = (int)Math.Ceiling(waitFor.TotalSeconds);
            Response.Headers.RetryAfter = retryAfterSeconds.ToString();
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
              message = "Your server has your last request. You can send another in a moment.",
              retry_after_seconds = retryAfterSeconds,
            });
          }
        }

        var serviceRequest = new ServiceRequest
        {
          Session_Id = session.Session_Id,
          Table_Id = tableId.Value,
          Request_By = user.User_id,
          Notes = requestData.Notes ?? string.Empty,
          Status = ServiceRequestStatus.Pending
        };

        _context.ServiceRequests.Add(serviceRequest);
        await _context.SaveChangesAsync();

        // Reload to get table navigation property
        await _context.Entry(serviceRequest).Reference(sr => sr.Table).LoadAsync();

        return Ok(ServiceRequestResponseDto.From(serviceRequest));
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error creating service request");
        return StatusCode(500, new { message = "An error occurred while processing your request." });
      }
    }

    /// <summary>
    /// Marks a service request as completed and assigns it to the current user.
    /// </summary>
    /// <param name="requestId">The unique identifier of the service request to complete</param>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/servicerequest/123/complete
    ///
    /// This endpoint requires Admin or Staff role authorization.
    /// The service request status will be updated to 'Completed' and the completion timestamp will be set.
    /// The current authenticated user will be assigned as the user who completed the request.
    /// </remarks>
    /// <response code="200">The completed service request.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">The table is at another location, or the caller isn't staff.</response>
    /// <response code="404">The service request doesn't exist.</response>
    /// <response code="409">The service request is already completed.</response>
    /// <response code="500">The service request couldn't be updated.</response>
    [Authorize(Roles = "Admin,Staff")]
    [HttpPost("{requestId}/complete")]
    [ProducesResponseType(typeof(ServiceRequestResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CompleteServiceRequest(int requestId)
    {
      try
      {
        var serviceRequest = await _context.ServiceRequests
            .Include(sr => sr.Table)
            .FirstOrDefaultAsync(sr => sr.request_id == requestId);

        if (serviceRequest is null)
        {
          return NotFound(new { message = "The service request was not found" });
        }

        if (!await SessionAccess.CanViewSessionAsync(_context, User, serviceRequest.Session_Id))
        {
          return LocationResults.OtherLocation();
        }

        if (serviceRequest.Status == ServiceRequestStatus.Completed)
        {
          return Conflict(new { message = "The service request is already completed" });
        }

        if (!int.TryParse(ClaimsHelpers.GetUserId(User), out var claimedByUserId))
        {
          return Unauthorized(new { message = "User ID not found in claims." });
        }

        var now = DateTime.UtcNow;
        serviceRequest.Claimed_By = claimedByUserId;
        serviceRequest.Claimed_At ??= now;
        serviceRequest.Completed_At = now;
        serviceRequest.Status = ServiceRequestStatus.Completed;

        await _context.SaveChangesAsync();

        return Ok(ServiceRequestResponseDto.From(serviceRequest));
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error completing service request");
        return StatusCode(500, new { message = "An error occurred while processing your request." });
      }
    }

    /// <summary>
    /// Retrieves all pending service requests for a specific session.
    /// </summary>
    /// <param name="sessionId">The unique identifier of the session</param>
    /// <remarks>
    /// Sample request:
    ///
    ///     GET /api/servicerequest/by-session/123
    ///
    /// Staff and admins can read any session. Customers can read only a session they're seated in,
    /// so the bill summary can tell them a "Ready for bill" request is already waiting.
    /// Only returns service requests with 'Pending' status.
    /// </remarks>
    /// <response code="200">The pending requests (empty if none).</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Not seated at this table, or staff at another location.</response>
    /// <response code="404">The session doesn't exist.</response>
    /// <response code="500">The requests couldn't be loaded.</response>
    [Authorize]
    [HttpGet("by-session/{sessionId}")]
    [ProducesResponseType(typeof(List<ServiceRequestResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetSessionServiceRequest(int sessionId)
    {
      try
      {
        var session = await _context.DiningSessions
            .FirstOrDefaultAsync(ds => ds.Session_Id == sessionId);

        if (session is null)
        {
          return NotFound(new { message = "The session was not found" });
        }

        if (!await SessionAccess.CanViewSessionAsync(_context, User, sessionId))
        {
          return Forbid();
        }

        var sessionRequestList = await _context.ServiceRequests
            .Include(sr => sr.Table)
            .Where(sr => sr.Session_Id == sessionId && sr.Status == ServiceRequestStatus.Pending)
            .ToListAsync();

        var response = sessionRequestList.Select(ServiceRequestResponseDto.From).ToList();

        return Ok(response);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting session service requests");
        return StatusCode(500, new { message = "An error occurred while processing your request." });
      }
    }

    /// <summary>
    /// Retrieves all service requests from the database.
    /// </summary>
    /// <response code="200">The service requests (can be empty).</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Not staff, or a staff member with no working location.</response>
    /// <response code="500">The requests couldn't be loaded.</response>
    [Authorize(Policy = "staffOnly")]
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ServiceRequestResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<ServiceRequestResponseDto>>> GetAllServiceRequest()
    {
      try
      {
        // Staff see their location's requests; admins see every location's
        var scope = await _locationAccess.ResolveLocationAsync(User, null);
        if (scope.IsForbidden) return LocationResults.NoLocation();

        // A DTO, not the entity: the entity's users would send their password hashes
        var requests = await _context.ServiceRequests
            .Include(sr => sr.Table)
            .Where(sr => scope.LocationId == null || sr.DiningSession.Location_Id == scope.LocationId)
            .ToListAsync();
        return Ok(requests.Select(ServiceRequestResponseDto.From).ToList());
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting service requests");
        return StatusCode(500, "Internal Server Error");
      }
    }

    /// <summary>
    /// Every pending service request, oldest first. Staff get their own location; admins get every location.
    /// </summary>
    /// <remarks>
    /// Sample request:
    ///
    ///     GET /api/servicerequest/pending
    /// </remarks>
    /// <response code="200">The pending requests (can be empty).</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Not staff, or a staff member with no working location.</response>
    /// <response code="500">The requests couldn't be loaded.</response>
    [Authorize(Roles = "Admin,Staff")]
    [HttpGet("pending")]
    [ProducesResponseType(typeof(IEnumerable<ServiceRequestResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<ServiceRequestResponseDto>>> GetAllPendingServiceRequests()
    {
      try
      {
        // Staff see their location's requests; admins see every location's
        var scope = await _locationAccess.ResolveLocationAsync(User, null);
        if (scope.IsForbidden) return LocationResults.NoLocation();

        var pendingRequests = await _context.ServiceRequests
            .Include(sr => sr.Table)
            .Include(sr => sr.RequestedByUser)
            .Include(sr => sr.DiningSession)
            .Where(sr => sr.Status == ServiceRequestStatus.Pending)
            .Where(sr => scope.LocationId == null || sr.DiningSession.Location_Id == scope.LocationId)
            .OrderBy(sr => sr.Created_At)
            .ToListAsync();

        var response = pendingRequests.Select(ServiceRequestResponseDto.From).ToList();

        return Ok(response);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting pending service requests");
        return StatusCode(500, new { message = "An error occurred while processing your request." });
      }
    }
  }
}