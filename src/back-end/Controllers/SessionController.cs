using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.DTO.DiningSessionDTOs;
using Swashbuckle.AspNetCore.Annotations;

namespace back_end.Controllers
{
  [ApiController]
  [Route("api/[controller]")]
  public class SessionController : ControllerBase
  {
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SessionController> _logger;

    public SessionController(ApplicationDbContext context, ILogger<SessionController> logger)
    {
      _context = context;
      _logger = logger;
    }

    // Helper method to get table information from session
    private List<object> GetTableInfo(DiningSession session)
    {
      if (session.Table != null)
      {
        return new List<object>
                {
                    new
                    {
                        tableId = session.Table.Table_Id,
                        tableNumber = session.Table.table_number,
                        seatCount = session.Table.seat_count
                    }
                };
      }
      else if (session.TableGroup != null && session.TableGroup.Tables != null)
      {
        return session.TableGroup.Tables.Select(t => new
        {
          tableId = t.Table_Id,
          tableNumber = t.table_number,
          seatCount = t.seat_count
        }).Cast<object>().ToList();
      }
      return new List<object>();
    }

    // Helper method to get table numbers from either single table or table group
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

    // GET: api/session
    /// <summary>Retrieves all dining sessions.</summary>
    /// <response code="200">A list of all sessions was returned.</response>
    /// <response code="500">An error occurred while retrieving sessions.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<DiningSessionResponseDTO>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<DiningSessionResponseDTO>>> GetAllSessions()
    {
      try
      {
        var sessions = await _context.DiningSessions
          .Include(s => s.Table)
          .Include(s => s.TableGroup)
              .ThenInclude(tg => tg!.Tables)
          .Include(s => s.Participants)
          .Include(s => s.Location)
          .OrderByDescending(s => s.Started_At)
          .ToListAsync();

        var sessionDtos = sessions.Select(s => new DiningSessionResponseDTO
        {
          Session_Id = s.Session_Id,
          Menu_Id = s.Menu_Id,
          Location_Id = s.Location_Id,
          Location_Name = s.Location?.Name,
          Started_at = s.Started_At,
          Ended_at = s.Ended_At,
          First_Order_Time = s.First_Order_At,
          Table_Numbers = GetTableNumbers(s),
          Active_Participants = s.Participants.Count(p => p.Left_At == null)
        }).ToList();

        return Ok(sessionDtos);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Cannot get Dining Sessions");
        return StatusCode(500, "Internal Server Error");
      }
    }


    // GET: api/session/{id}
    /// <summary>Gets a dining session with users, menu, orders, and table assignments.</summary>
    /// <param name="id">Session identifier.</param>
    /// <response code="200">Session details returned.</response>
    /// <response code="404">Session not found.</response>
    /// <response code="500">An error occurred while retrieving the session.</response>
    [HttpGet("{id}")]
    public async Task<ActionResult<object>> GetSessionById(int id)
    {
      try
      {
        //Gather all the information for the inputted session
        var session = await _context.DiningSessions
                .Where(s => s.Session_Id == id)
                .Include(s => s.Menu)
                .Include(s => s.Participants)
                // .ThenInclude(se => se.User) removed for Oauth
                .Include(s => s.Table)
                .Include(s => s.TableGroup)
                    .ThenInclude(tg => tg!.Tables)
                .Include(s => s.Orders)
                    .ThenInclude(so => so.OrderItems)
                .FirstOrDefaultAsync();

        //Check if the session exists
        if (session == null)
        {
          return NotFound(new { message = $"Can't find Session with ID: {id}" });
        }

        //Start creating an object that stores/organizes session information
        var sessionInfo = new
        {
          sessionID = session.Session_Id,
          menu = session.Menu.Name,
          start = session.Started_At,
          end = session.Ended_At,
          firstOrder = session.First_Order_At,
          active = session.Ended_At == null,

          // Updated: Get all the participants in the session with their information
          guests = session.Participants.Select(s => new
          {
            participantId = s.Participant_Id,
            userId = s.User_Id,
            userName = s.User_Name,
            joinedAt = s.Joined_At,
            leftAt = s.Left_At
          }).ToList(),

          //Get information about each table assigned to the session
          tables = GetTableInfo(session),

          //Get all of the orders in the session and their status/completed/done
          orders = session.Orders.Select(s => new
          {
            orderID = s.Order_Id,
            userId = s.User_Id,
            userName = s.User_Name,
            status = s.Status.ToString(),
            itemCount = s.OrderItems.Count,
            createdAt = s.Created_At,
            completedAt = s.Completed_At
          }).ToList()
        };

        return Ok(sessionInfo);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, $"Can't get session for ID: {id}");
        return StatusCode(500, "Internal Server Error");
      }
    }

    // GET: api/session/active
    /// <summary>Gets all active dining sessions.</summary>
    /// <response code="200">Active session summaries returned.</response>
    /// <response code="500">An error occurred while retrieving active sessions.</response>
    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<object>>> GetActiveSessions()
    {
      try
      {
        //Get all current active sessions (ended at is null)
        //Include Menu, Participants, and tables
        var activeSessions = await _context.DiningSessions
                            .Where(s => s.Ended_At == null)
                            .Include(s => s.Menu)
                            .Include(s => s.Participants)
                            // .ThenInclude(se => se.User) removed for Oauth
                            .Include(s => s.Table)
                            .Include(s => s.TableGroup)
                                .ThenInclude(tg => tg!.Tables)
                            .ToListAsync();

        //Keep all key information
        //Session ID, Menu, Session Start time and # of Participants
        var sessionInfo = activeSessions.Select(s => new
        {
          sessionId = s.Session_Id,
          menuName = s.Menu.Name,
          startedAt = s.Started_At,
          participantCount = s.Participants.Count,
          //Get the table number and ID and add up total orders for the session
          tables = GetTableInfo(s),
          orderCount = s.Orders.Count
        }).ToList();

        return Ok(sessionInfo);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Can't get current active Dining Sessions");
        return StatusCode(500, "Internal Server Error");
      }
    }

    // GET: api/session/table/{table}
    /// <summary>Gets the active session (if any) for a specific table.</summary>
    /// <param name="table">Table ID.</param>
    /// <response code="200">Active/empty result returned for the table.</response>
    /// <response code="404">Table not found.</response>
    /// <response code="500">An error occurred while retrieving the session by table.</response>
    [HttpGet("table/{table}")]
    public async Task<ActionResult<object>> GetSessionByTable(int table)
    {
      try
      {
        var sessionTable = await _context.Tables.FindAsync(table);

        //Check if table exists
        if (sessionTable == null)
        {
          return NotFound(new { message = $"Can't find table with ID: {table}" });
        }

        //Find an active dining session connected to inputted table
        //Either directly assigned or through a table group
        var sessionInfo = await _context.DiningSessions
                    .Include(s => s.Menu)
                    .Include(s => s.Table)
                    .Include(s => s.TableGroup)
                        .ThenInclude(tg => tg!.Tables)
                    .Include(s => s.Participants)
                    // .ThenInclude(p => p.User) removed for Oauth
                    .Where(s => s.Ended_At == null)
                    .Where(s => s.Table_Id == table ||
                               (s.TableGroup_Id.HasValue &&
                                s.TableGroup!.Tables.Any(t => t.Table_Id == table)))
                    .FirstOrDefaultAsync();

        //Check if there's an active Dining Session
        if (sessionInfo == null)
        {
          return Ok(new
          {
            message = "No active session at this table",
            tableNumber = sessionTable.table_number,
            tableId = table,
            activeSession = false
          });
        }

        //Get Session, Menu Details and all users active at the table
        var sessionDetails = new
        {
          activeSession = true,
          sessionId = sessionInfo.Session_Id,
          menuName = sessionInfo.Menu.Name,
          menuId = sessionInfo.Menu_Id,
          startedAt = sessionInfo.Started_At,
          participants = sessionInfo.Participants
                                    .Where(s => s.Left_At == null)
                                    .Select(s => new
                                    {
                                      userId = s.User_Id,
                                      userName = s.User_Name,
                                      joinedAt = s.Joined_At
                                    }).ToList()
        };

        return Ok(sessionDetails);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, $"Can't get session for table ID: {table}");
        return StatusCode(500, "Internal Server Error");
      }
    }

    // GET: api/session/user/{userId}/current
    /// <summary>Gets the current active session for a specific user.</summary>
    /// <param name="userId">User identifier.</param>
    /// <response code="200">Active/empty result returned for the user.</response>
    /// <response code="404">User not found.</response>
    /// <response code="500">An error occurred while retrieving the user's current session.</response>
    [HttpGet("user/{userOid}/current")]
    public async Task<ActionResult<object>> GetCurrentSessionByUser(int userId)
    {
      try
      {
        //Check for an active session that the user is currently in
        //Add Menu and Table information
        var session = await _context.SessionParticipants
                            .Where(s => s.User_Id == userId && s.Left_At == null)
                            .Include(s => s.DiningSession)
                                .ThenInclude(se => se.Menu)
                            .Include(s => s.DiningSession)
                                .ThenInclude(se => se.Table)
                            .Include(s => s.DiningSession)
                                .ThenInclude(se => se.TableGroup)
                                    .ThenInclude(tg => tg!.Tables)
                            .Where(s => s.DiningSession.Ended_At == null)
                            .Select(s => s.DiningSession)
                            .FirstOrDefaultAsync();

        //Check if the user is apart of an active session
        if (session == null)
        {
          return Ok(new
          {
            message = "Guest is not currently in an active dining session.",
            userId = userId,
            activeSession = false
          });
        }

        //Get Session ID, Menu information, tables assigned 
        //Then return the information for the active session
        var sessionDetails = new
        {
          activeSession = true,
          sessionId = session.Session_Id,
          menuName = session.Menu.Name,
          menuId = session.Menu_Id,
          startedAt = session.Started_At,
          tables = GetTableInfo(session)
        };

        return Ok(sessionDetails);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, $"Can't get session for user Id: {userId}");
        return StatusCode(500, "Internal Server Error");
      }
    }
  }
}