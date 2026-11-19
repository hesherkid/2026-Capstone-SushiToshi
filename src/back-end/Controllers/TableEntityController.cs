using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using Microsoft.AspNetCore.Authorization;
using back_end.Dtos;
using back_end.Helpers;
using back_end.Services;

namespace back_end.Controllers
{
  [ApiController]
  [Route("api/[controller]")]
  public class TableEntityController : ControllerBase
  {
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TableEntityController> _logger;
    private readonly ILocationAccessService _locationAccess;

    public TableEntityController(ApplicationDbContext context, ILogger<TableEntityController> logger, ILocationAccessService locationAccess)
    {
      _context = context;
      _logger = logger;
      _locationAccess = locationAccess;
    }


    /// <summary>
    /// Creates a table at a location.
    /// </summary>
    /// <param name="tableData">The table number, seats, active flag, QR code URL and location</param>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/tableentity
    ///     { "table_number": 14, "seat_count": 4, "is_active": true, "location_id": 1 }
    ///
    /// - Table numbers must be unique within a location.
    /// - Staff can only add tables at a location they work at.
    /// - is_active defaults to true; qr_code_url defaults to empty.
    /// </remarks>
    /// <response code="200">The new table.</response>
    /// <response code="400">The location doesn't exist, the table number is already used there, or a number is negative.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Not staff, or the location isn't one the caller works at.</response>
    /// <response code="500">The table couldn't be saved.</response>
    [Authorize(Policy = "staffOnly")]
    [HttpPost()]
    [ProducesResponseType(typeof(TableEntityResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateTable(TableEntityCreateDto tableData)
    {
      try
      {
        // Validate location exists
        var location = await _context.Locations
            .FirstOrDefaultAsync(l => l.Location_Id == tableData.LocationId);

        if (location == null)
        {
          return BadRequest("Location does not exist");
        }

        if (!await _locationAccess.CanAccessLocationAsync(User, tableData.LocationId))
        {
          return LocationResults.OtherLocation();
        }

        // Check if table number already exists at this location
        var existingTable = await _context.Tables
            .FirstOrDefaultAsync(t => t.table_number == tableData.TableNumber &&
                                      t.Location_Id == tableData.LocationId);

        if (existingTable != null)
        {
          return BadRequest("Table number already exists at this location");
        }

        var newTable = new TableEntity
        {
          table_number = tableData.TableNumber,
          QR_Code = tableData.QrCodeUrl ?? string.Empty,
          seat_count = tableData.SeatCount,
          is_active = tableData.IsActive,
          Location_Id = tableData.LocationId
        };

        _context.Tables.Add(newTable);
        await _context.SaveChangesAsync();
        return Ok(TableEntityResponseDto.From(newTable));
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error creating table");
        return StatusCode(500, new { message = "Error creating table" });
      }
    }

    /// <summary>
    /// Every table, by location then table number. Staff get their own location.
    /// </summary>
    /// <param name="locationId">Optional location filter</param>
    /// <remarks>
    /// Sample request:
    ///
    ///     GET /api/tableentity?location_id=1
    ///
    /// Includes inactive tables and tables in use.
    /// </remarks>
    /// <response code="200">The tables (can be empty).</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Not staff, or a staff member with no working location.</response>
    /// <response code="500">The tables couldn't be loaded.</response>
    [Authorize(Policy = "staffOnly")]
    [HttpGet]
    [ProducesResponseType(typeof(List<TableEntityResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<TableEntityResponseDto>>> GetAllTables([FromQuery(Name = "location_id")] int? locationId)
    {
      try
      {
        var scope = await _locationAccess.ResolveLocationAsync(User, locationId);
        if (scope.IsForbidden) return LocationResults.NoLocation();
        locationId = scope.LocationId;

        var query = _context.Tables
            .Include(t => t.TableGroup)
            .Include(t => t.Location)
            .AsQueryable();

        if (locationId.HasValue)
        {
          query = query.Where(t => t.Location_Id == locationId.Value);
        }

        var tables = await query
        .OrderBy(q => q.Location_Id)      // Group by location
        .ThenBy(q => q.table_number)      // Then sort by table number within location
        .ToListAsync();
        return Ok(tables.Select(TableEntityResponseDto.From).ToList());
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting tables");
        return StatusCode(500, "Internal Server Error");
      }
    }

    /// <summary>
    /// Active tables that nobody is sitting at, alone or as part of a group. Staff get their own location.
    /// </summary>
    /// <param name="locationId">Optional location filter</param>
    /// <remarks>
    /// Sample request:
    ///
    ///     GET /api/tableentity/empty?location_id=1
    ///
    /// A table is empty when it's active, it has no open dining session, and its group (if any) has none either.
    /// </remarks>
    /// <response code="200">The empty tables (can be empty).</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Not staff, or a staff member with no working location.</response>
    /// <response code="500">The tables couldn't be loaded.</response>
    [Authorize(Policy = "staffOnly")]
    [HttpGet("empty")]
    [ProducesResponseType(typeof(List<TableEntityResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<TableEntityResponseDto>>> ListEmptyTables([FromQuery(Name = "location_id")] int? locationId)
    {
      try
      {
        var scope = await _locationAccess.ResolveLocationAsync(User, locationId);
        if (scope.IsForbidden) return LocationResults.NoLocation();
        locationId = scope.LocationId;

        var query = _context.Tables
            .Include(t => t.TableGroup)
            .Include(t => t.Location)
            .Where(t => t.is_active == true &&
                        !t.DiningSessions.Any(ds => ds.Ended_At == null) &&
                        (t.TableGroup == null || !t.TableGroup.DiningSessions.Any(ds => ds.Ended_At == null)));

        if (locationId.HasValue)
        {
          query = query.Where(t => t.Location_Id == locationId.Value);
        }

        var emptyTables = await query.ToListAsync();
        return Ok(emptyTables.Select(TableEntityResponseDto.From).ToList());
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting empty tables");
        return StatusCode(500, "Internal Server Error");
      }
    }

    /// <summary>
    /// One table.
    /// </summary>
    /// <param name="tableId">The table</param>
    /// <response code="200">The table, including its group (if any) and QR code URL.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">The table is at a location the caller doesn't work at.</response>
    /// <response code="404">The table doesn't exist.</response>
    /// <response code="500">The table couldn't be loaded.</response>
    [Authorize]
    [HttpGet("{tableId}")]
    [ProducesResponseType(typeof(TableEntityResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetTable(int tableId)
    {
      try
      {
        var table = await _context.Tables
            .Include(t => t.TableGroup)
            .FirstOrDefaultAsync(t => t.Table_Id == tableId);

        if (table is null)
        {
          return NotFound("Table not found");
        }

        if (!await _locationAccess.CanAccessLocationAsync(User, table.Location_Id))
        {
          return LocationResults.OtherLocation();
        }

        return Ok(TableEntityResponseDto.From(table));
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting table");
        return StatusCode(500, "Internal Server Error");
      }
    }

    /// <summary>
    /// Changes a table's number, seats, active flag or QR code URL.
    /// </summary>
    /// <param name="tableId">The table</param>
    /// <param name="tableData">The fields to change; leave out the ones to keep</param>
    /// <remarks>
    /// Sample request:
    ///
    ///     PUT /api/tableentity/7
    ///     { "seat_count": 6 }
    ///
    /// - Only the fields sent are changed; an empty body changes nothing.
    /// - A new table_number must be unused at the table's location.
    /// - Unlike toggle-status, setting is_active here isn't blocked while the table is in use.
    /// </remarks>
    /// <response code="200">The updated table.</response>
    /// <response code="400">The table number is already used at this location, or a number is negative.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Not staff, or the table is at a location the caller doesn't work at.</response>
    /// <response code="404">The table doesn't exist.</response>
    /// <response code="500">The table couldn't be saved.</response>
    [Authorize(Policy = "staffOnly")]
    [HttpPut("{tableId}")]
    [ProducesResponseType(typeof(TableEntityResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateTable(int tableId, TableEntityUpdateDto tableData)
    {
      try
      {
        var table = await _context.Tables
            .FirstOrDefaultAsync(t => t.Table_Id == tableId);

        if (table is null)
        {
          return NotFound("Table not found");
        }

        if (!await _locationAccess.CanAccessLocationAsync(User, table.Location_Id))
        {
          return LocationResults.OtherLocation();
        }

        // Check if table number is being changed
        if (tableData.TableNumber.HasValue && table.table_number != tableData.TableNumber)
        {
          var existingTable = await _context.Tables
              .FirstOrDefaultAsync(t => t.table_number == tableData.TableNumber &&
                              t.Location_Id == table.Location_Id &&
                              t.Table_Id != tableId);

          if (existingTable != null)
          {
            return BadRequest("Table number already exists");
          }
          table.table_number = tableData.TableNumber.Value;
        }

        if (tableData.SeatCount.HasValue)
        {
          table.seat_count = tableData.SeatCount.Value;
        }

        if (tableData.IsActive.HasValue)
        {
          table.is_active = tableData.IsActive.Value;
        }

        if (tableData.QrCodeUrl != null)
        {
          table.QR_Code = tableData.QrCodeUrl;
        }

        _context.Tables.Update(table);
        await _context.SaveChangesAsync();
        return Ok(TableEntityResponseDto.From(table));
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error updating table");
        return StatusCode(500, "Internal Server Error");
      }
    }

    /// <summary>
    /// Deletes a table by its identifier.
    /// </summary>
    /// <param name="tableId">The table</param>
    /// <remarks>
    /// Sample request:
    ///
    ///     DELETE /api/tableentity/7
    ///
    /// A table that has ever had a dining session can't be deleted, so its history stays intact;
    /// deactivate it instead. A table whose group is in an open session can't be deleted either.
    /// </remarks>
    /// <response code="200">The table was deleted.</response>
    /// <response code="400">The table is in use, has been used before, or its group is in use.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Not an admin.</response>
    /// <response code="404">The table doesn't exist.</response>
    /// <response code="500">The table couldn't be deleted.</response>
    [Authorize(Policy = "adminOnly")]
    [HttpDelete("{tableId}")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteTable(int tableId)
    {
      try
      {
        var table = await _context.Tables
            .Include(t => t.TableGroup)
            .FirstOrDefaultAsync(t => t.Table_Id == tableId);

        if (table is null)
        {
          return NotFound("Could not find a table to delete");
        }

        // Check if table is in an active session (directly assigned)
        var activeSessionDirect = await _context.DiningSessions
            .AnyAsync(ds => ds.Table_Id == tableId && ds.Ended_At == null);

        if (activeSessionDirect)
        {
          return BadRequest("Cannot delete a table that is currently in use in an active session");
        }

        // Check if table is in an ended session (directly assigned)
        var inactiveSessionDirect = await _context.DiningSessions
            .AnyAsync(ds => ds.Table_Id == tableId && ds.Ended_At != null);

        if (inactiveSessionDirect)
        {
          return BadRequest("Cannot delete a table that has previously been used in a session");
        }

        // Check if table is part of a table group that's in an active session
        if (table.TableGroup_Id.HasValue)
        {
          var activeSessionViaGroup = await _context.DiningSessions
              .AnyAsync(ds => ds.TableGroup_Id == table.TableGroup_Id && ds.Ended_At == null);

          if (activeSessionViaGroup)
          {
            return BadRequest("Cannot delete a table that is part of a table group in an active session");
          }
        }

        _context.Tables.Remove(table);
        await _context.SaveChangesAsync();
        return Ok("Table was deleted");
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error deleting table");
        return StatusCode(500, "Internal Server Error");
      }
    }

    /// <summary>
    /// Toggles a table's active status.
    /// </summary>
    /// <param name="tableId">The table</param>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/tableentity/7/toggle-status
    ///
    /// Not allowed while the table, or its group, has an open dining session. Inactive tables
    /// aren't offered for seating.
    /// </remarks>
    /// <response code="200">The table with its new is_active value.</response>
    /// <response code="400">The table, or its group, is in an open session.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Not staff, or the table is at a location the caller doesn't work at.</response>
    /// <response code="404">The table doesn't exist.</response>
    /// <response code="500">The change couldn't be saved.</response>
    [Authorize(Policy = "staffOnly")]
    [HttpPost("{tableId}/toggle-status")]
    [ProducesResponseType(typeof(TableEntityResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ToggleTableStatus(int tableId)
    {
      try
      {
        var table = await _context.Tables
            .Include(t => t.TableGroup)
            .FirstOrDefaultAsync(t => t.Table_Id == tableId);

        if (table is null)
        {
          return NotFound("Could not find table");
        }

        if (!await _locationAccess.CanAccessLocationAsync(User, table.Location_Id))
        {
          return LocationResults.OtherLocation();
        }

        // Check if table is in an active session (directly assigned)
        var activeSessionDirect = await _context.DiningSessions
            .AnyAsync(ds => ds.Table_Id == tableId && ds.Ended_At == null);

        if (activeSessionDirect)
        {
          return BadRequest("Cannot deactivate table that is currently in use in an active session");
        }

        // Check if table is part of a table group that's in an active session
        if (table.TableGroup_Id.HasValue)
        {
          var activeSessionViaGroup = await _context.DiningSessions
              .AnyAsync(ds => ds.TableGroup_Id == table.TableGroup_Id && ds.Ended_At == null);

          if (activeSessionViaGroup)
          {
            return BadRequest("Cannot deactivate table that is part of a table group in an active session");
          }
        }

        // Toggle the status
        table.is_active = !table.is_active;

        _context.Tables.Update(table);
        await _context.SaveChangesAsync();
        return Ok(TableEntityResponseDto.From(table));
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error toggling table status");
        return StatusCode(500, "Internal Server Error");
      }
    }

    /// <summary>
    /// Whether a table is free: no open dining session on it, or on its group.
    /// </summary>
    /// <param name="tableId">The table</param>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/tableentity/7/active-session
    ///
    /// success is true when the table is free, false when it's in use; message says which.
    /// </remarks>
    /// <response code="200">success and message.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">The table is at a location the caller doesn't work at.</response>
    /// <response code="404">The table doesn't exist.</response>
    /// <response code="500">The check couldn't be done.</response>
    [Authorize]
    [HttpPost("{tableId}/active-session")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CheckTableActiveSession(int tableId)
    {
      try
      {
        var table = await _context.Tables
            .Include(t => t.TableGroup)
            .FirstOrDefaultAsync(t => t.Table_Id == tableId);

        if (table == null)
        {
          return NotFound("Table not found");
        }

        if (!await _locationAccess.CanAccessLocationAsync(User, table.Location_Id))
        {
          return LocationResults.OtherLocation();
        }

        // Check if table is directly assigned to an active session
        var activeSessionDirect = await _context.DiningSessions
            .AnyAsync(ds => ds.Table_Id == tableId && ds.Ended_At == null);

        if (activeSessionDirect)
        {
          return Ok(new { success = false, message = "Table is in an active session" });
        }

        // Check if table is part of a table group in an active session
        if (table.TableGroup_Id.HasValue)
        {
          var activeSessionViaGroup = await _context.DiningSessions
              .AnyAsync(ds => ds.TableGroup_Id == table.TableGroup_Id && ds.Ended_At == null);

          if (activeSessionViaGroup)
          {
            return Ok(new { success = false, message = "Table is part of a table group in an active session" });
          }
        }

        return Ok(new { success = true, message = "Table is available" });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error checking table active session");
        return StatusCode(500, "Internal Server Error");
      }
    }

    /// <summary>
    /// Puts a table into a table group at the same location.
    /// </summary>
    /// <param name="tableId">The table</param>
    /// <param name="tableGroupId">The group</param>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/tableentity/7/assign-to-group/3
    ///
    /// A table already in another group is moved to this one. Not allowed while the table has its
    /// own open dining session.
    /// </remarks>
    /// <response code="200">message and the updated table.</response>
    /// <response code="400">The table and group are at different locations, or the table is in an open session.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Not staff, or the table is at a location the caller doesn't work at.</response>
    /// <response code="404">The table or the group doesn't exist.</response>
    /// <response code="500">The change couldn't be saved.</response>
    [Authorize(Policy = "staffOnly")]
    [HttpPost("{tableId}/assign-to-group/{tableGroupId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AssignTableToGroup(int tableId, int tableGroupId)
    {
      try
      {
        var table = await _context.Tables
            .FirstOrDefaultAsync(t => t.Table_Id == tableId);

        if (table == null)
        {
          return NotFound("Table not found");
        }

        if (!await _locationAccess.CanAccessLocationAsync(User, table.Location_Id))
        {
          return LocationResults.OtherLocation();
        }

        var tableGroup = await _context.TableGroups
            .FirstOrDefaultAsync(tg => tg.TableGroup_Id == tableGroupId);

        if (tableGroup == null)
        {
          return NotFound("Table group not found");
        }

        // Validate that table and table group belong to same location
        if (table.Location_Id != tableGroup.Location_Id)
        {
          return BadRequest($"Table and table group must belong to the same location. Table is at location {table.Location_Id}, group is at location {tableGroup.Location_Id}");
        }

        var activeSession = await _context.DiningSessions
            .AnyAsync(ds => ds.Table_Id == tableId && ds.Ended_At == null);

        if (activeSession)
        {
          return BadRequest("Cannot assign table to group while it's in an active session");
        }

        table.TableGroup_Id = tableGroupId;
        _context.Tables.Update(table);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Table assigned to group successfully", table = TableEntityResponseDto.From(table) });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error assigning table to group");
        return StatusCode(500, "Internal Server Error");
      }
    }

    /// <summary>
    /// Takes a table out of its table group.
    /// </summary>
    /// <param name="tableId">The table</param>
    /// <remarks>
    /// Sample request:
    ///
    ///     DELETE /api/tableentity/7/remove-from-group
    ///
    /// Not allowed while the group has an open dining session.
    /// </remarks>
    /// <response code="200">message and the updated table.</response>
    /// <response code="400">The table isn't in a group, or the group is in an open session.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Not staff, or the table is at a location the caller doesn't work at.</response>
    /// <response code="404">The table doesn't exist.</response>
    /// <response code="500">The change couldn't be saved.</response>
    [Authorize(Policy = "staffOnly")]
    [HttpDelete("{tableId}/remove-from-group")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RemoveTableFromGroup(int tableId)
    {
      try
      {
        var table = await _context.Tables
            .Include(t => t.TableGroup)
            .FirstOrDefaultAsync(t => t.Table_Id == tableId);

        if (table == null)
        {
          return NotFound("Table not found");
        }

        if (!await _locationAccess.CanAccessLocationAsync(User, table.Location_Id))
        {
          return LocationResults.OtherLocation();
        }

        if (!table.TableGroup_Id.HasValue)
        {
          return BadRequest("Table is not assigned to any group");
        }

        // Check if the table group is in an active session
        var activeSession = await _context.DiningSessions
            .AnyAsync(ds => ds.TableGroup_Id == table.TableGroup_Id && ds.Ended_At == null);

        if (activeSession)
        {
          return BadRequest("Cannot remove table from group while the group is in an active session");
        }

        table.TableGroup_Id = null;
        _context.Tables.Update(table);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Table removed from group successfully", table = TableEntityResponseDto.From(table) });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error removing table from group");
        return StatusCode(500, "Internal Server Error");
      }
    }

  }
}