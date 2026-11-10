using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;
using Microsoft.AspNetCore.Mvc;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.Dtos;
using back_end.Helpers;
using back_end.Services;
using Microsoft.AspNetCore.Authorization;


namespace back_end.Controllers
{
  [ApiController]
  [Route("api/[controller]")]
  public class DashboardController : Controller
  {
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _config;
    private readonly ILogger<DashboardController> _logger;
    private readonly ILocationAccessService _locationAccess;

    public DashboardController(ApplicationDbContext context, IConfiguration config, ILogger<DashboardController> logger, ILocationAccessService locationAccess)
    {
      _context = context;
      _config = config;
      _logger = logger;
      _locationAccess = locationAccess;
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

    // Helper method to count tables in session
    private int GetTableCount(DiningSession session)
    {
      if (session.Table_Id.HasValue)
      {
        return 1;
      }
      else if (session.TableGroup_Id.HasValue && session.TableGroup != null)
      {
        return session.TableGroup.Tables?.Count() ?? 0;
      }
      return 0;
    }

    /// <summary>
    /// Retrieves detailed information about all active dining sessions for dashboard display.
    /// </summary>
    /// <param name="locationId">Optional location ID to filter sessions by specific location</param>
    /// <returns>
    /// An <see cref="IActionResult"/> containing a collection of <see cref="DashboardSessionsDto"/> objects.
    /// Returns HTTP 200 (OK) with the list of active sessions on success.
    /// Returns HTTP 404 (Not Found) if no active sessions exist.
    /// Returns HTTP 500 (Internal Server Error) if an exception occurs during retrieval.
    /// </returns>
    /// <response code="200">Returns the list of active dining sessions with detailed information</response>
    /// <response code="204">If no active dining sessions are found</response>
    /// <response code="500">If an internal error occurs while retrieving sessions</response>
    /// <remarks>
    /// Sample requests:
    ///
    ///     GET /api/dashboard/sessions
    ///     (Returns all active sessions across all locations)
    ///     
    ///     GET /api/dashboard/sessions?locationId=1
    ///     (Returns only active sessions for location with ID 1)
    ///
    /// Returns comprehensive information about each active session including:
    /// - Session metadata (ID, menu, timestamps, location)
    /// - Table numbers
    /// - Active participant count
    /// - Associated bills with details
    /// - Whether the session can be closed (no open bills)
    /// 
    /// Sessions are ordered by start time (newest first).
    /// </remarks>
    [Authorize(Policy = "staffOnly")]
    [HttpGet("sessions")]
    [ProducesResponseType(typeof(List<DashboardSessionsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDashboardSessions([FromQuery] int? locationId)
    {
      try
      {
        var scope = await _locationAccess.ResolveLocationAsync(User, locationId);
        if (scope.IsForbidden) return LocationResults.NoLocation();
        locationId = scope.LocationId;

        var query = _context.DiningSessions
            .Include(ds => ds.Location)
            .Include(ds => ds.Table)
            .Include(ds => ds.TableGroup)
                .ThenInclude(tg => tg!.Tables)
            .Include(ds => ds.Bills)
            .Include(ds => ds.Participants)
            .Where(ds => ds.Ended_At == null);

        // Filter by location if provided
        if (locationId.HasValue)
        {
          query = query.Where(ds => ds.Location_Id == locationId.Value);
        }

        var sessions = await query
            .OrderByDescending(ds => ds.Started_At)
            .ToListAsync();

        if (sessions == null || sessions.Count == 0)
        {
          return NoContent();
        }

        var dashboardSessions = new List<DashboardSessionsDto>();
        foreach (var session in sessions)
        {
          bool hasOpenBill = session.Bills.Any(b => b.Status == BillStatus.Open);
          bool isClosable = !hasOpenBill;

          var activeParticipants = session.Participants.Count(p => p.Left_At == null);

          var dashboardSession = new DashboardSessionsDto
          {
            SessionId = session.Session_Id,
            MenuId = session.Menu_Id,
            LocationId = session.Location_Id,
            LocationName = session.Location?.Name,
            StartedAt = session.Started_At,
            EndedAt = session.Ended_At,
            FirstOrderAt = session.First_Order_At,
            TableNumbers = GetTableNumbers(session),
            SeatCount = BillLinks.TableCapacity(session),
            ActiveParticipants = activeParticipants,
            Bills = session.Bills.Select(b => new DashboardBillDto
            {
              BillId = b.Bill_Id,
              BillName = b.Bill_Name,
              Status = b.Status,
              CreatedAt = b.Created_At,
              TotalGuests = b.Total_Count,
              AdultCount = b.Adult_Count,
              SeniorCount = b.Senior_Count,
              ChildCount = b.Child_Count,
              TotCount = b.Tot_Count,
              RequestedAt = b.Requested_At,
              StartedByGuest = b.Started_By_Guest,
              CountsChangedByGuest = b.Counts_Changed_By_Guest,
              CountsLocked = b.Counts_Locked,
              PeopleOnBill = session.Participants.Count(p => p.Left_At == null && p.Bill_Id == b.Bill_Id)
            }).ToList(),
            People = session.Participants
                .Where(p => p.Left_At == null)
                .OrderBy(p => p.Joined_At)
                .Select(p => new DashboardPersonDto { ParticipantId = p.Participant_Id, UserName = p.User_Name, BillId = p.Bill_Id })
                .ToList(),
            BillCount = session.Bills.Count(),
            IsClosable = isClosable
          };
          dashboardSessions.Add(dashboardSession);
        }
        return Ok(dashboardSessions);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to load dashboard sessions for location {LocationId}", locationId);
        return StatusCode(500, new { message = "An error occurred while processing your request." });
      }
    }


    /// <summary>
    /// Retrieves dashboard summary statistics for active dining sessions.
    /// </summary>
    /// <param name="locationId">Optional location ID to filter statistics by specific location</param>
    /// <returns>
    /// An <see cref="IActionResult"/> containing a <see cref="DashboardSummaryDto"/> object with summary statistics.
    /// Returns HTTP 200 (OK) with the dashboard summary on success.
    /// Returns HTTP 500 (Internal Server Error) if an exception occurs during retrieval.
    /// </returns>
    /// <response code="200">Returns dashboard summary statistics</response>
    /// <response code="500">If an internal error occurs while retrieving the summary</response>
    /// <remarks>
    /// Sample requests:
    ///
    ///     GET /api/dashboard/summary
    ///     (Returns statistics for all locations)
    ///     
    ///     GET /api/dashboard/summary?locationId=1
    ///     (Returns statistics for location with ID 1 only)
    ///
    /// Returns real-time statistics including:
    /// - Total number of active (not ended) sessions
    /// - Total number of tables currently in use
    /// - Total number of open bills in active sessions
    /// - Total number of active participants (who haven't left)
    /// - Location information (if filtered by location)
    /// </remarks>
    [Authorize(Policy = "staffOnly")]
    [HttpGet("summary")]
    [ProducesResponseType(typeof(DashboardSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDashboardSummary([FromQuery] int? locationId)
    {
      try
      {
        var scope = await _locationAccess.ResolveLocationAsync(User, locationId);
        if (scope.IsForbidden) return LocationResults.NoLocation();
        locationId = scope.LocationId;

        var query = _context.DiningSessions
            .Include(ds => ds.Table)
            .Include(ds => ds.TableGroup)
                .ThenInclude(tg => tg!.Tables)
            .Include(ds => ds.Location)
            .Where(ds => ds.Ended_At == null);

        // Filter by location if provided
        if (locationId.HasValue)
        {
          query = query.Where(ds => ds.Location_Id == locationId.Value);
        }

        var activeSessions = await query.ToListAsync();

        int totalActiveSessions = activeSessions.Count();

        // Count total tables in use - sum of individual tables and tables in groups
        int totalTablesInUse = activeSessions.Sum(session => GetTableCount(session));

        // Build query for active bills
        var billsQuery = _context.Bills
            .Join(
                _context.DiningSessions,
                bill => bill.Session_Id,
                session => session.Session_Id,
                (bill, session) => new { bill, session })
            .Where(x => x.session.Ended_At == null &&
                        x.bill.Status == BillStatus.Open);

        // Filter by location if provided
        if (locationId.HasValue)
        {
          billsQuery = billsQuery.Where(x => x.session.Location_Id == locationId.Value);
        }

        int totalActiveBills = await billsQuery.CountAsync();

        // Build query for active participants
        var participantsQuery = _context.SessionParticipants
            .Join(
                _context.DiningSessions,
                sp => sp.Session_Id,
                session => session.Session_Id,
                (sp, session) => new { sp, session })
            .Where(x => x.sp.Left_At == null &&
                        x.session.Ended_At == null);

        // Filter by location if provided
        if (locationId.HasValue)
        {
          participantsQuery = participantsQuery.Where(x => x.session.Location_Id == locationId.Value);
        }

        int totalActiveParticipants = await participantsQuery.CountAsync();

        // Get location name if filtering by location
        string? locationName = null;
        if (locationId.HasValue)
        {
          var location = await _context.Locations
              .FirstOrDefaultAsync(l => l.Location_Id == locationId.Value);
          locationName = location?.Name;
        }

        var summary = new DashboardSummaryDto
        {
          LocationId = locationId,
          LocationName = locationName,
          TotalActiveSessions = totalActiveSessions,
          TotalTablesInUse = totalTablesInUse,
          TotalActiveBills = totalActiveBills,
          TotalActiveParticipants = totalActiveParticipants
        };

        return Ok(summary);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to load dashboard summary for location {LocationId}", locationId);
        return StatusCode(500, new { message = "An error occurred while processing your request." });
      }
    }
  }
}