using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;
using Microsoft.AspNetCore.Mvc;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.DTO.DiningSessionDTOs;
using back_end.DTO.TableGroupDTOs;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using back_end.Helpers;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace back_end.controllers
{
  [ApiController]
  [Route("api/[controller]")]
  public class DiningSessionController : Controller
  {
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _config;
    private readonly ILogger<DashBoardController> _logger;

    public DiningSessionController(ApplicationDbContext context, IConfiguration config, ILogger<DashBoardController> logger)
    {
      _context = context;
      _config = config;
      _logger = logger;
    }

    // Validation method for table assignment
    private void ValidateTableAssignment(DiningSession session)
    {
      if ((session.Table_Id.HasValue && session.TableGroup_Id.HasValue) ||
          (!session.Table_Id.HasValue && !session.TableGroup_Id.HasValue))
      {
        throw new ValidationException("DiningSession must have either Table_Id or TableGroup_Id, but not both.");
      }
    }

    /// <summary>
    /// Creates a new dining session with a specified menu and optionally assigns a table or table group.
    /// </summary>
    /// <param name="sessionData">The session creation data containing the menu ID and optional table/table group</param>
    /// <param name="assignmentType">Specifies what to assign: "table", "table_group", or "none" (default: "none")</param>
    /// <returns>
    /// An <see cref="IActionResult"/> containing the created <see cref="DiningSessionResponseDTO"/> object.
    /// Returns HTTP 200 (OK) with the created session details on success.
    /// Returns HTTP 400 (Bad Request) if validation fails or resources don't exist.
    /// Returns HTTP 500 (Internal Server Error) if an exception occurs during creation.
    /// </returns>
    /// <response code="200">Returns the newly created dining session</response>
    /// <response code="400">If validation fails or resources are not found</response>
    /// <response code="500">If an internal error occurs while creating the session</response>
    /// <remarks>
    /// Sample requests:
    ///
    ///     POST /api/diningsession/Create_Dinning_Session?assignmentType=table
    ///     {
    ///         "menu_Id": 123,
    ///         "location_Id": 1,
    ///         "table_Id": 456
    ///     }
    ///
    ///     POST /api/diningsession/Create_Dinning_Session?assignmentType=table_group
    ///     {
    ///         "menu_Id": 123,
    ///         "location_Id": 1,
    ///         "tableGroup_Id": 789
    ///     }
    ///
    ///     POST /api/diningsession/Create_Dinning_Session
    ///     {
    ///         "menu_Id": 123,
    ///         "location_Id": 1
    ///     }
    ///
    /// Creates a new dining session with the specified menu.
    /// The session is automatically marked as started with the current UTC timestamp.
    /// Use assignmentType to control whether a table, table group, or neither is assigned during creation.
    /// </remarks>
    [HttpPost("Create_Dinning_Session")]
    // Staff seat tables; guests join an existing session through POST /api/DiningSession/join
    [Authorize(Roles = "Admin,Staff")]
    [ProducesResponseType(typeof(DiningSessionResponseDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> create_dining_session(
        [FromBody] CreateSessionRequestDTO sessionData)

    {
      try
      {
        var menu = await _context.Menus.Where(m => m.Menu_id == sessionData.Menu_Id).FirstOrDefaultAsync();
        if (menu == null) return BadRequest("Menu Does not Exist");

        var location = await _context.Locations.Where(l => l.Location_Id == sessionData.Location_Id).FirstOrDefaultAsync();
        if (location == null) return BadRequest("Location Does not Exist");

        string assignmentType = sessionData.AssignmentType?.ToLower() ?? "none";
        int? tableId = null;
        int? tableGroupId = null;
        List<int> tableNumbers = new();

        if (assignmentType == "table")
        {
          if (!sessionData.Table_Id.HasValue)
            return BadRequest("Table_Id is required when assignmentType is 'table'");

          var table = await _context.Tables
              .FirstOrDefaultAsync(t => t.Table_Id == sessionData.Table_Id.Value && t.is_active);

          if (table == null) return BadRequest($"Table: {sessionData.Table_Id} not found or inactive");
          if (table.Location_Id != sessionData.Location_Id)
            return BadRequest($"Table belongs to a different location");

          // Busy if the table, or a table group it belongs to, already has an open session
          bool tableBusy = await _context.DiningSessions
              .AnyAsync(d => d.Ended_At == null &&
                             (d.Table_Id == table.Table_Id ||
                              (table.TableGroup_Id != null && d.TableGroup_Id == table.TableGroup_Id)));

          if (tableBusy)
            return BadRequest($"Table {table.table_number} already has an active session");

          tableId = table.Table_Id;
          tableNumbers.Add(table.table_number);
        }
        else if (assignmentType == "tablegroup")
        {
          if (!sessionData.TableGroup_Id.HasValue)
            return BadRequest("TableGroup_Id is required when assignmentType is 'table_group'");

          var tableGroup = await _context.TableGroups
              .Include(tg => tg.Tables)
              .FirstOrDefaultAsync(tg => tg.TableGroup_Id == sessionData.TableGroup_Id.Value && tg.Is_Active);

          if (tableGroup == null)
            return BadRequest($"Table group: {sessionData.TableGroup_Id} not found or inactive");
          if (tableGroup.Location_Id != sessionData.Location_Id)
            return BadRequest($"Table group belongs to a different location");

          var tableIds = tableGroup.Tables.Select(t => t.Table_Id).ToList();
          var checkIfTablesInActiveSession = await _context.DiningSessions
              .AnyAsync(d => d.Ended_At == null &&
                            (d.Table_Id.HasValue && tableIds.Contains(d.Table_Id.Value) ||
                      d.TableGroup_Id.HasValue && d.TableGroup!.Tables.Any(t => tableIds.Contains(t.Table_Id))));

          if (checkIfTablesInActiveSession)
            return BadRequest($"One or more tables in table group {tableGroup.TableGroup_Id} are currently in another active dining session");

          tableGroupId = tableGroup.TableGroup_Id;
          tableNumbers = tableGroup.Tables.Select(t => t.table_number).ToList();
        }
        else if (assignmentType != "none")
        {
          return BadRequest("Invalid assignmentType. Must be 'table', 'table_group', or 'none'");
        }

        var newSession = new DiningSession
        {
          Menu_Id = sessionData.Menu_Id,
          Location_Id = sessionData.Location_Id,
          Table_Id = tableId,
          TableGroup_Id = tableGroupId,
          Started_At = DateTime.UtcNow
        };

        ValidateTableAssignment(newSession);
        _context.Add<DiningSession>(newSession);
        await _context.SaveChangesAsync();

        return Ok(new DiningSessionResponseDTO
        {
          Session_Id = newSession.Session_Id,
          Menu_Id = newSession.Menu_Id,
          Started_at = newSession.Started_At,
          Ended_at = newSession.Ended_At,
          First_Order_Time = newSession.First_Order_At,
          Table_Numbers = tableNumbers,
          Active_Participants = 0
        });
      }
      catch (ValidationException vex)
      {
        return BadRequest(vex.Message);
      }
      catch (Exception ex)
      {
        return StatusCode(500, new { message = "An error occurred while processing your request.", error = ex.Message });
      }
    }

    /// <summary>
    /// Retrieves a list of dining sessions filtered by their active status.
    /// </summary>
    /// <param name="act">Query parameters to filter sessions by active status</param>
    /// <returns>
    /// An <see cref="IActionResult"/> containing a collection of <see cref="DiningSessionResponseDTO"/> objects.
    /// Returns HTTP 200 (OK) with the list of filtered sessions sorted by start time (newest first).
    /// Returns HTTP 500 (Internal Server Error) if an exception occurs during retrieval.
    /// </returns>
    /// <response code="200">Returns the list of dining sessions filtered by active status</response>
    /// <response code="500">If an internal error occurs while retrieving sessions</response>
    /// <remarks>
    /// Sample requests:
    ///
    ///     GET /api/diningsession/get_list_dining_sessions?ActiveOnly=true
    ///     (Returns only active/ongoing sessions)
    ///     
    ///     GET /api/diningsession/get_list_dining_sessions?ActiveOnly=false
    ///     (Returns only ended sessions)
    ///
    /// Sessions are ordered by start time in descending order (newest first).
    /// Each session includes table numbers and participant count.
    /// </remarks>
    [HttpGet("get_list_dining_sessions")]
    [ProducesResponseType(typeof(IEnumerable<DiningSessionResponseDTO>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> list_dining_sessions([FromQuery] ListDiningSessionsRequestDTO act)
    {
      try
      {
        var user = ClaimsHelpers.GetUserId(User);
        var location = await _context.Users.Where(u => u.User_id.ToString() == user).Select(u => u.Location_id).FirstOrDefaultAsync();
        var query = _context.DiningSessions.Where(ds => ds.Location_Id == location)
            .Include(s => s.Table)
            .Include(s => s.TableGroup)
                .ThenInclude(tg => tg!.Tables)
            .Include(s => s.Participants)
            .AsQueryable();

        if (act.ActiveOnly)
        {
          query = query.Where(ds => ds.Ended_At == null);
        }
        else
        {
          query = query.Where(ds => ds.Ended_At != null);
        }

        var sessions = await query.OrderByDescending(q => q.Started_At).ToListAsync();

        var Response = sessions.Select(session => new DiningSessionResponseDTO
        {
          Session_Id = session.Session_Id,
          Menu_Id = session.Menu_Id,
          Started_at = session.Started_At,
          Ended_at = session.Ended_At,
          First_Order_Time = session.First_Order_At,
          Table_Numbers = GetTableNumbers(session),
          Active_Participants = session.Participants.Count()
        }).ToList();

        return Ok(Response);
      }
      catch (Exception ex)
      {
        return StatusCode(500, new { message = "An error occurred while processing your request.", error = ex.Message });
      }
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

    /// <summary>
    /// Retrieves detailed information about a specific dining session.
    /// </summary>
    /// <param name="session_id">The unique identifier of the dining session</param>
    /// <returns>
    /// An <see cref="IActionResult"/> containing a <see cref="DiningSessionDetailDTO"/> object with session details.
    /// Returns HTTP 200 (OK) with the session details on success.
    /// Returns HTTP 404 (Not Found) if the session doesn't exist.
    /// Returns HTTP 500 (Internal Server Error) if an exception occurs during retrieval.
    /// </returns>
    /// <response code="200">Returns detailed information about the dining session</response>
    /// <response code="404">If the session is not found</response>
    /// <response code="500">If an internal error occurs while retrieving the session</response>
    /// <remarks>
    /// Sample request:
    ///
    ///     GET /api/diningsession/get_location/123
    ///
    /// Returns comprehensive session details including:
    /// - Session metadata (ID, menu, timestamps)
    /// - Table numbers
    /// - Participant counts
    /// - Order and bill statistics
    /// </remarks>
    [HttpGet("get_location/{session_id}")]
    public async Task<IActionResult> get_dining_session(int session_id)
    {
      try
      {
        var session = await _context.DiningSessions
            .Include(s => s.Bills)
            .Include(s => s.Table)
            .Include(s => s.TableGroup)
                .ThenInclude(tg => tg!.Tables)
            .Include(s => s.Orders)
            .FirstOrDefaultAsync(s => s.Session_Id == session_id);

        if (session == null)
        {
          return BadRequest($"Session:{session_id} Does not Exist");
        }

        int active_participants = await _context.SessionParticipants
            .Where(sp => sp.Session_Id == session_id)
            .CountAsync();

        foreach (var returnedBill in session.Bills)
        {
          _logger.LogInformation($"Bill {returnedBill.Bill_Id}, status:{returnedBill.Status}");
        }

        return Ok(new DiningSessionDetailDTO
        {
          Session_Id = session.Session_Id,
          Menu_Id = session.Menu_Id,
          Started_at = session.Started_At,
          Ended_at = session.Ended_At,
          First_Order_Time = session.First_Order_At,
          Table_Numbers = GetTableNumbers(session),
          Active_Participants = active_participants,
          Orders_Count = session.Orders.Count(),
          Bills_Count = session.Bills.Count(),
          Active_Bill_Count = session.Bills.Count(b => b.Status == BillStatus.Open),
          Total_participant = active_participants
        });
      }
      catch (Exception ex)
      {
        return StatusCode(500, new { message = "An error occurred while processing your request.", error = ex.Message });
      }
    }

    /// <summary>
    /// Adds a table to an existing dining session.
    /// </summary>
    /// <param name="session_id">The unique identifier of the dining session</param>
    /// <param name="tableData">The table assignment data containing the table ID</param>
    /// <returns>
    /// An <see cref="IActionResult"/> containing the updated <see cref="DiningSessionResponseDTO"/> object.
    /// Returns HTTP 200 (OK) with the updated session details on success.
    /// Returns HTTP 400 (Bad Request) if the session has ended, has bills, or the table is unavailable.
    /// Returns HTTP 404 (Not Found) if the session doesn't exist.
    /// Returns HTTP 500 (Internal Server Error) if an exception occurs during the operation.
    /// </returns>
    /// <response code="200">Returns the updated dining session with the added table</response>
    /// <response code="400">If the session has ended, has bills, or the table is inactive/assigned to another session</response>
    /// <response code="404">If the session is not found</response>
    /// <response code="500">If an internal error occurs while adding the table to the session</response>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/diningsession/123/tables
    ///     {
    ///         "table_Id": 456
    ///     }
    ///
    /// Adds a table to an active dining session.
    /// Requirements:
    /// - Session must not have ended
    /// - Session must not have any bills created yet
    /// - Table must be active
    /// - Table must not be assigned to another active session
    /// </remarks>
    [HttpPost("{session_id}/tables")]
    [ProducesResponseType(typeof(DiningSessionResponseDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> add_table_to_dining_session(int session_id, [FromBody] TableAssignmentDTO tableData)
    {
      try
      {
        var session = await _context.DiningSessions
            .Include(d => d.Table)
            .Include(d => d.TableGroup)
            .Include(d => d.Bills)
            .FirstOrDefaultAsync(s => s.Session_Id == session_id);

        if (session == null)
        {
          return NotFound($"Session: {session_id} not found");
        }

        if (session.Ended_At != null)
        {
          return BadRequest($"Cannot modify an ended session: {session_id}");
        }

        if (session.Bills.Any())
        {
          return BadRequest("Cannot add tables after a bill has been created");
        }

        if (session.TableGroup_Id.HasValue)
        {
          return BadRequest("Cannot assign individual table to session with table group. Remove table group first.");
        }

        var table = await _context.Tables
            .FirstOrDefaultAsync(t => t.Table_Id == tableData.Table_Id && t.is_active);

        if (table == null)
        {
          return BadRequest($"Table: {tableData.Table_Id} not found or inactive");
        }

        // Validate that table belongs to same location as session
        if (table.Location_Id != session.Location_Id)
        {
          return BadRequest($"Table belongs to a different location. Session is for location {session.Location_Id}, table is for location {table.Location_Id}");
        }

        session.Table_Id = table.Table_Id;
        session.Table = table;

        ValidateTableAssignment(session);

        await _context.SaveChangesAsync();

        return Ok(new DiningSessionResponseDTO
        {
          Session_Id = session.Session_Id,
          Menu_Id = session.Menu_Id,
          Started_at = session.Started_At,
          Ended_at = session.Ended_At,
          First_Order_Time = session.First_Order_At,
          Table_Numbers = new List<int> { table.table_number },
          Active_Participants = await _context.SessionParticipants.CountAsync(s => s.Session_Id == session.Session_Id),
        });
      }
      catch (ValidationException vex)
      {
        return BadRequest(vex.Message);
      }
      catch (Exception ex)
      {
        return StatusCode(500, new { message = "An error occurred while processing your request.", error = ex.Message });
      }
    }

    [HttpPost("{session_id}/table-groups")]
    public async Task<IActionResult> add_table_group_to_dining_session(int session_id, [FromBody] TableGroupAssignmentDTO tableGroupData)
    {
      try
      {
        var session = await _context.DiningSessions
            .Include(d => d.Table)
            .Include(d => d.TableGroup)
            .Include(d => d.Bills)
            .FirstOrDefaultAsync(s => s.Session_Id == session_id);

        if (session == null)
        {
          return NotFound($"Session: {session_id} not found");
        }

        if (session.Ended_At != null)
        {
          return BadRequest($"Cannot modify an ended session: {session_id}");
        }

        if (session.Bills.Any())
        {
          return BadRequest("Cannot add table group after a bill has been created");
        }

        if (session.Table_Id.HasValue)
        {
          return BadRequest("Cannot assign table group to session with individual table. Remove table first.");
        }

        var tableGroup = await _context.TableGroups
            .Include(tg => tg.Tables)
            .FirstOrDefaultAsync(tg => tg.TableGroup_Id == tableGroupData.TableGroup_Id && tg.Is_Active);

        if (tableGroup == null)
        {
          return BadRequest($"Table group: {tableGroupData.TableGroup_Id} not found or inactive");
        }

        // Validate that table group belongs to same location as session
        if (tableGroup.Location_Id != session.Location_Id)
        {
          return BadRequest($"Table group belongs to a different location. Session is for location {session.Location_Id}, table group is for location {tableGroup.Location_Id}");
        }

        var tableIds = tableGroup.Tables.Select(t => t.Table_Id).ToList();
        var checkIfTablesInActiveSession = await _context.DiningSessions
            .AnyAsync(d => d.Ended_At == null &&
                          d.Session_Id != session_id &&
                          (d.Table_Id.HasValue && tableIds.Contains(d.Table_Id.Value) ||
                           d.TableGroup_Id.HasValue && d.TableGroup!.Tables.Any(t => tableIds.Contains(t.Table_Id))));

        if (checkIfTablesInActiveSession)
        {
          return BadRequest($"One or more tables in table group {tableGroup.TableGroup_Id} are currently in another active dining session.");
        }

        session.TableGroup_Id = tableGroup.TableGroup_Id;
        session.TableGroup = tableGroup;

        ValidateTableAssignment(session);

        await _context.SaveChangesAsync();

        return Ok(new DiningSessionResponseDTO
        {
          Session_Id = session.Session_Id,
          Menu_Id = session.Menu_Id,
          Started_at = session.Started_At,
          Ended_at = session.Ended_At,
          First_Order_Time = session.First_Order_At,
          Table_Numbers = tableGroup.Tables.Select(t => t.table_number).ToList(),
          Active_Participants = await _context.SessionParticipants.CountAsync(s => s.Session_Id == session.Session_Id),
        });
      }
      catch (ValidationException vex)
      {
        return BadRequest(vex.Message);
      }
      catch (Exception ex)
      {
        return StatusCode(500, new { message = "An error occurred while processing your request.", error = ex.Message });
      }
    }

    /// <summary>
    /// Removes a table from a dining session.
    /// </summary>
    /// <param name="session_id">The unique identifier of the dining session</param>
    /// <param name="table_id">The unique identifier of the table to remove</param>
    /// <returns>
    /// An <see cref="IActionResult"/> indicating the result of the operation.
    /// Returns HTTP 200 (OK) with a success message when the table is removed.
    /// Returns HTTP 400 (Bad Request) if the session has open bills.
    /// Returns HTTP 404 (Not Found) if the session, table, or table-session association doesn't exist.
    /// Returns HTTP 500 (Internal Server Error) if an exception occurs during removal.
    /// </returns>
    /// <response code="200">Returns a success message when the table is removed from the session</response>
    /// <response code="400">If the session has open bills preventing table removal</response>
    /// <response code="404">If the session, table, or table-session association is not found</response>
    /// <response code="500">If an internal error occurs while removing the table from the session</response>
    /// <remarks>
    /// Sample request:
    ///
    ///     DELETE /api/diningsession/123/Tables/456
    ///
    /// Removes the association between a table and a dining session.
    /// Tables cannot be removed from sessions that have open bills.
    /// </remarks>
    [HttpDelete("{session_id}/Tables/{table_id}")]
    public async Task<IActionResult> remove_table_from_session(int session_id, int table_id)
    {
      try
      {
        var session = await _context.DiningSessions
            .Include(ds => ds.Bills)
            .Include(ds => ds.Table)
            .FirstOrDefaultAsync(ds => ds.Session_Id == session_id);

        if (session == null)
        {
          return NotFound($"Session was not found for session: {session_id}");
        }

        // Only check the status of the Bill
        if (session.Bills.Any(b => b.Status == BillStatus.Open))
        {
          return BadRequest($"Cannot close a table and session with open bills");
        }

        var table = await _context.Tables
            .FirstOrDefaultAsync(te => te.Table_Id == table_id);

        if (table == null)
        {
          return NotFound($"Table was not found");
        }

        // Check if this table is assigned to the session
        if (session.Table_Id == table_id)
        {
          session.Table_Id = null;
          session.Table = null;
          await _context.SaveChangesAsync();
          return Ok(new { message = "Table removed from session successfully" });
        }
        else
        {
          return NotFound($"Table was not found in the session: {session_id}");
        }
      }
      catch (Exception ex)
      {
        return StatusCode(500, new { message = "An error occurred while processing your request.", error = ex.Message });
      }
    }

    [HttpDelete("{session_id}/table-groups/{table_group_id}")]
    public async Task<IActionResult> remove_table_group_from_session(int session_id, int table_group_id)
    {
      try
      {
        var session = await _context.DiningSessions
            .Include(ds => ds.Bills)
            .Include(ds => ds.TableGroup)
            .FirstOrDefaultAsync(ds => ds.Session_Id == session_id);

        if (session == null)
        {
          return NotFound($"Session was not found for session: {session_id}");
        }

        // Only check the status of the Bill
        if (session.Bills.Any(b => b.Status == BillStatus.Open))
        {
          return BadRequest($"Cannot remove table group from session with open bills");
        }

        // Check if this table group is assigned to the session
        if (session.TableGroup_Id == table_group_id)
        {
          session.TableGroup_Id = null;
          session.TableGroup = null;
          await _context.SaveChangesAsync();
          return Ok(new { message = "Table group removed from session successfully" });
        }
        else
        {
          return NotFound($"Table group was not found in the session: {session_id}");
        }
      }
      catch (Exception ex)
      {
        return StatusCode(500, new { message = "An error occurred while processing your request.", error = ex.Message });
      }
    }

    /// <summary>
    /// Retrieves the active dining session ID for the current authenticated user.
    /// </summary>
    /// <returns>
    /// An <see cref="IActionResult"/> containing the active session ID.
    /// Returns HTTP 200 (OK) with the session ID on success.
    /// Returns HTTP 401 (Unauthorized) if the user is not authenticated.
    /// Returns HTTP 404 (Not Found) if no active session exists for the user.
    /// Returns HTTP 500 (Internal Server Error) if an exception occurs during retrieval.
    /// </returns>
    /// <response code="200">Returns the active session ID for the authenticated user</response>
    /// <response code="401">If the user is not authenticated</response>
    /// <response code="404">If no active session is found for the user</response>
    /// <response code="500">If an internal error occurs while retrieving the active session</response>
    /// <remarks>
    /// Sample request:
    ///
    ///     GET /api/diningsession/participants/active-session-id
    ///
    /// This endpoint requires authentication.
    /// Returns the session ID of the user's current active (not ended) dining session.
    /// A user can only have one active session at a time.
    /// </remarks>

    [HttpGet("participants/active-session-id")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetActiveSessionId()
    {
      try
      {
        // Get current user ID from claims using ClaimsHelpers
        var userIdString = ClaimsHelpers.GetUserId(User);

        _logger.LogInformation($"USER ID: '{userIdString}'");


        if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out var userId))
        {
          return Unauthorized(new { message = "User not authenticated or invalid user ID" });
        }

        // Only a seat the user still holds counts (Left_At == null); a table they left
        // or moved away from must not make them look seated there
        var activeSessionId = await _context.SessionParticipants
            .CurrentSeats(userId)
            .OrderByDescending(sp => sp.DiningSession.Started_At)
            .Select(sp => sp.Session_Id)
            .FirstOrDefaultAsync();

        if (activeSessionId == 0)
        {
          return NotFound(new { message = "No active session found" });
        }

        return Ok(new { session_id = activeSessionId });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error retrieving active session");
        return StatusCode(500, new { message = "An error occurred while processing your request.", error = ex.Message });
      }
    }


    /// <summary>
    /// Retrieves the menu ID associated with a specific dining session.
    /// </summary>
    /// <param name="session_id">The unique identifier of the dining session</param>
    /// <returns>
    /// An <see cref="ActionResult"/> containing a <see cref="SessionMenuResponseDTO"/> object with the menu ID.
    /// Returns HTTP 200 (OK) with the menu ID on success.
    /// Returns HTTP 404 (Not Found) if the dining session doesn't exist.
    /// Returns HTTP 500 (Internal Server Error) if an exception occurs during retrieval.
    /// </returns>
    /// <response code="200">Returns the menu ID for the specified session</response>
    /// <response code="401">If the user is not signed in</response>
    /// <response code="403">If a customer asks about a table they aren't seated at</response>
    /// <response code="404">If the dining session is not found</response>
    /// <response code="500">If an internal error occurs while retrieving the session menu</response>
    /// <remarks>
    /// Sample request:
    ///
    ///     GET /api/diningsession/session-menu/123
    ///
    /// Returns the menu ID associated with the specified dining session.
    /// Staff and admins can ask about any table; customers (guests included) only about the
    /// table they're seated at, the same rule as the session's bills, orders and limits.
    /// </remarks>
    [HttpGet("session-menu/{session_id}")]
    [Authorize]
    [ProducesResponseType(typeof(SessionMenuResponseDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SessionMenuResponseDTO>> get_session_menu_id(int session_id)
    {
      try
      {
        if (!await SessionAccess.CanViewSessionAsync(_context, User, session_id))
        {
          return Forbid();
        }

        var session = await _context.DiningSessions
            .FirstOrDefaultAsync(ds => ds.Session_Id == session_id);

        if (session == null)
        {
          return NotFound("Dining Session was not found");
        }

        return Ok(new SessionMenuResponseDTO
        {
          Menu_Id = session.Menu_Id,
          Location_Id = session.Location_Id
        });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error retrieving active Menu Session");
        return StatusCode(500, new { message = "An error occurred while processing your request.", error = ex.Message });
      }
    }

    /// <summary>
    /// Closes an active dining session by setting the end timestamp.
    /// </summary>
    /// <param name="session_id">The unique identifier of the dining session to close</param>
    /// <returns>
    /// An <see cref="IActionResult"/> containing the updated <see cref="DiningSessionResponseDTO"/> object.
    /// Returns HTTP 200 (OK) with the updated session details on success.
    /// Returns HTTP 400 (Bad Request) if the session is already closed or has open bills.
    /// Returns HTTP 404 (Not Found) if the session doesn't exist.
    /// Returns HTTP 500 (Internal Server Error) if an exception occurs during the operation.
    /// </returns>
    /// <response code="200">Returns the closed dining session with the end timestamp</response>
    /// <response code="400">If the session is already closed or has open bills</response>
    /// <response code="404">If the session is not found</response>
    /// <response code="500">If an internal error occurs while closing the session</response>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/diningsession/123/close
    ///
    /// Closes an active dining session by setting the Ended_At timestamp to the current UTC time.
    /// Everyone still seated leaves with the table (Left_At is set to the same time), so no seat
    /// outlives its session.
    /// Requirements:
    /// - Staff or admin only
    /// - Session must not already be closed (Ended_At must be null)
    /// - Session must not have any open bills
    /// </remarks>
    [HttpPut("{session_id}/close")]
    [Authorize(Roles = "Admin,Staff")]
    [ProducesResponseType(typeof(DiningSessionResponseDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> close_dining_session(int session_id)
    {
      try
      {
        var session = await _context.DiningSessions
            .Include(ds => ds.Bills)
            .Include(ds => ds.Table)
            .Include(ds => ds.TableGroup)
                .ThenInclude(tg => tg!.Tables)
            .Include(ds => ds.Participants)
            .FirstOrDefaultAsync(ds => ds.Session_Id == session_id);

        if (session == null)
        {
          return NotFound($"Session: {session_id} not found");
        }

        if (session.Ended_At != null)
        {
          return BadRequest($"Session: {session_id} is already closed");
        }

        // Check if there are any open bills
        if (session.Bills.Any(b => b.Status == BillStatus.Open))
        {
          return BadRequest($"Cannot close session with open bills. Please settle all bills first.");
        }

        var now = DateTime.UtcNow;
        session.Ended_At = now;

        // The table is done, so everyone still seated leaves with it. 
        // Seats that already left keep the time they actually left
        foreach (var seat in session.Participants.Where(p => p.Left_At == null))
        {
          seat.Left_At = now;
        }

        await _context.SaveChangesAsync();

        return Ok(new DiningSessionResponseDTO
        {
          Session_Id = session.Session_Id,
          Menu_Id = session.Menu_Id,
          Started_at = session.Started_At,
          Ended_at = session.Ended_At,
          First_Order_Time = session.First_Order_At,
          Table_Numbers = GetTableNumbers(session),
          Active_Participants = session.Participants.Count()
        });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error closing dining session");
        return StatusCode(500, new { message = "An error occurred while processing your request.", error = ex.Message });
      }
    }

    /// <summary>
    /// Joins the signed-in user to the active dining session for a table, using the table number
    /// and location from the table's QR code. Each guest signs in with their own guest account.
    /// </summary>
    /// <remarks>
    /// Only joins sessions staff have already started; it never creates one.
    /// A table in a table group joins the group's session.
    /// Calling it again returns the same participant, so rescanning the QR code is safe.
    /// Joining a table leaves any other table the user is still seated at.
    /// </remarks>
    /// <response code="200">Joined (or already in) the table's session.</response>
    /// <response code="401">The request has no valid user.</response>
    /// <response code="404">The table doesn't exist, or it has no active session yet.</response>
    [HttpPost("join")]
    [Authorize]
    [ProducesResponseType(typeof(JoinTableSessionResponseDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> JoinTableSession([FromBody] JoinTableSessionDTO request)
    {
      if (!int.TryParse(ClaimsHelpers.GetUserId(User), out var userId))
      {
        return Unauthorized(new { message = "Please sign in to join a table." });
      }

      var table = await _context.Tables
          .FirstOrDefaultAsync(t => t.table_number == request.Table_Number &&
                                    t.Location_Id == request.Location_Id &&
                                    t.is_active);
      if (table == null)
      {
        return NotFound(new { message = $"Table {request.Table_Number} was not found at this location." });
      }

      // A grouped table seats guests in the group's session, otherwise the table's own
      var session = await _context.DiningSessions
          .Include(ds => ds.Table)
          .Include(ds => ds.TableGroup).ThenInclude(tg => tg!.Tables)
          .Where(ds => ds.Ended_At == null && ds.Location_Id == request.Location_Id)
          .FirstOrDefaultAsync(ds =>
              (table.TableGroup_Id != null && ds.TableGroup_Id == table.TableGroup_Id) ||
              ds.Table_Id == table.Table_Id);
      if (session == null)
      {
        return NotFound(new { message = $"Table {request.Table_Number} doesn't have an active session yet. Please ask staff to start your table." });
      }

      // Someone is seated at one table at a time: scanning a new table (e.g. after moving)
      // leaves any other table they're still in, so their active session is never ambiguous
      var otherSeats = await _context.SessionParticipants
          .Where(sp => sp.User_Id == userId && sp.Left_At == null && sp.Session_Id != session.Session_Id)
          .ToListAsync();
      otherSeats.ForEach(sp => sp.Left_At = DateTime.UtcNow);

      var participant = await _context.SessionParticipants
          .FirstOrDefaultAsync(sp => sp.Session_Id == session.Session_Id &&
                                     sp.User_Id == userId &&
                                     sp.Left_At == null);
      bool alreadyJoined = participant != null;

      if (participant == null)
      {
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.User_id == userId);
        if (user == null)
        {
          return Unauthorized(new { message = "Please sign in to join a table." });
        }

        participant = new SessionParticipant
        {
          Session_Id = session.Session_Id,
          User_Id = userId,
          User_Name = $"{user.First_name} {user.Last_name}".Trim() is { Length: > 0 } name ? name : user.Email,
          Joined_At = DateTime.UtcNow
        };
        _context.SessionParticipants.Add(participant);
      }

      await _context.SaveChangesAsync();

      return Ok(new JoinTableSessionResponseDTO
      {
        Session_Id = session.Session_Id,
        Participant_Id = participant.Participant_Id,
        Menu_Id = session.Menu_Id,
        Table_Numbers = GetTableNumbers(session),
        Already_Joined = alreadyJoined
      });
    }
  }

}