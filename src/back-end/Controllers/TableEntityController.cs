using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using Microsoft.AspNetCore.Authorization;
using back_end.DTO.TableEntityDTOs;
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
    /// Creates a new table.
    /// </summary>
    /// <param name="table_data">The table payload including table number, seat count, active flag, and optional QR code URL.</param>
    /// <remarks>
    /// Returns <c>201 Created</c> with the created table and a <c>Location</c> header pointing to <see cref="GetTable(int)"/>.
    /// Table numbers must be unique.
    /// </remarks>
    /// <response code="201">The table was created successfully.</response>
    /// <response code="400">The request body is invalid or missing required fields.</response>
    /// <response code="401">The user is not authenticated.</response>
    /// <response code="403">The user is not authorized to create tables.</response>
    /// <response code="409">A table with the specified number already exists.</response>
    /// <response code="500">An unexpected error occurred while creating the table.</response>
    [Authorize(Policy = "staffOnly")]
    [HttpPost()]
    public async Task<IActionResult> CreateTable(TableEntityCreateDTO table_data)
    {
      try
      {
        // Validate location exists
        var location = await _context.Locations
            .FirstOrDefaultAsync(l => l.Location_Id == table_data.Location_Id);

        if (location == null)
        {
          return BadRequest("Location does not exist");
        }

        if (!await _locationAccess.CanAccessLocationAsync(User, table_data.Location_Id))
        {
          return LocationResults.OtherLocation();
        }

        // Check if table number already exists at this location
        var existingTable = await _context.Tables
            .FirstOrDefaultAsync(t => t.table_number == table_data.Table_Number &&
                                      t.Location_Id == table_data.Location_Id);

        if (existingTable != null)
        {
          return BadRequest("Table number already exists at this location");
        }

        var newTable = new TableEntity
        {
          table_number = table_data.Table_Number,
          QR_Code = table_data.Qr_Code_Url ?? string.Empty,
          seat_count = table_data.Seat_Count,
          is_active = table_data.Is_Active,
          Location_Id = table_data.Location_Id
        };

        _context.Tables.Add(newTable);
        await _context.SaveChangesAsync();
        return Ok(newTable);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error creating table");
        return StatusCode(500, new { detail = "Error creating table" });
      }
    }

    /// <summary>
    /// Retrieves all tables from the system.
    /// </summary>
    /// <remarks>
    /// Returns a list of all tables, regardless of whether they are active or in use.  
    /// Requires authentication.
    /// </remarks>
    /// <response code="200">A list of all tables was returned successfully.</response>
    /// <response code="500">An internal server error occurred while retrieving tables.</response>
    [Authorize]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TableEntity>>> GetAllTables([FromQuery] int? locationId)
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
        return Ok(tables);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting tables");
        return StatusCode(500, "Internal Server Error");
      }
    }

    /// <summary>
    /// Lists all active tables that are not currently in an ongoing dining session.
    /// </summary>
    /// <remarks>
    /// A table is considered <em>empty</em> if it is marked as active and it is not part of any dining session
    /// where <c>Ended_At == null</c>.
    /// </remarks>
    /// <response code="200">A list of empty (available) tables was returned successfully.</response>
    /// <response code="500">An unexpected error occurred while retrieving the tables.</response>
    [Authorize]
    [HttpGet("empty")]
    public async Task<ActionResult<IEnumerable<TableEntity>>> ListEmptyTables([FromQuery] int? locationId)
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
        return Ok(emptyTables);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting empty tables");
        return StatusCode(500, "Internal Server Error");
      }
    }

    /// <summary>
    /// Retrieves a table by its unique identifier.
    /// </summary>
    /// <param name="table_id">The unique identifier of the table to retrieve.</param>
    /// <remarks>
    /// Returns the table’s complete record, including its number, seat count, active state, and QR code information.
    /// Requires authorization.
    /// </remarks>
    /// <response code="200">The table was found and returned successfully.</response>
    /// <response code="404">No table exists with the specified <paramref name="table_id"/>.</response>
    /// <response code="500">An unexpected error occurred while retrieving the table.</response>
    [Authorize]
    [HttpGet("{table_id}")]
    public async Task<IActionResult> GetTable(int table_id)
    {
      try
      {
        var table = await _context.Tables
            .Include(t => t.TableGroup)
            .FirstOrDefaultAsync(t => t.Table_Id == table_id);

        if (table is null)
        {
          return NotFound("Table not found");
        }

        if (!await _locationAccess.CanAccessLocationAsync(User, table.Location_Id))
        {
          return LocationResults.OtherLocation();
        }

        return Ok(table);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting table");
        return StatusCode(500, "Internal Server Error");
      }
    }

    /// <summary>
    /// Updates a table by its identifier.
    /// </summary>
    /// <param name="table_id">The unique identifier of the table to update.</param>
    /// <param name="table_data">Fields to update for the table (partial updates supported).</param>
    /// <remarks>
    /// This endpoint performs a <em>partial</em> update using <c>PUT</c> for convenience:
    /// only the non-null properties in <c>TableEntityUpdateDTO</c> are applied.
    /// <br/><br/>
    /// - If <c>Table_Number</c> is provided and differs from the current value, it must be unique.
    /// - Returns the updated table in the response body.
    /// </remarks>
    /// <response code="200">The table was updated successfully.</response>
    /// <response code="400">Invalid request body or no updatable fields provided.</response>
    /// <response code="404">No table exists with the specified <paramref name="table_id"/>.</response>
    /// <response code="409">A different table already uses the requested table number.</response>
    /// <response code="500">An unexpected error occurred while updating the table.</response>
    [Authorize(Policy = "staffOnly")]
    [HttpPut("{table_id}")]
    public async Task<IActionResult> UpdateTable(int table_id, TableEntityUpdateDTO table_data)
    {
      try
      {
        var table = await _context.Tables
            .FirstOrDefaultAsync(t => t.Table_Id == table_id);

        if (table is null)
        {
          return NotFound("Table not found");
        }

        if (!await _locationAccess.CanAccessLocationAsync(User, table.Location_Id))
        {
          return LocationResults.OtherLocation();
        }

        // Check if table number is being changed
        if (table_data.Table_Number.HasValue && table.table_number != table_data.Table_Number)
        {
          var existingTable = await _context.Tables
              .FirstOrDefaultAsync(t => t.table_number == table_data.Table_Number && t.Table_Id != table_id);

          if (existingTable != null)
          {
            return BadRequest("Table number already exists");
          }
          table.table_number = table_data.Table_Number.Value;
        }

        if (table_data.Seat_Count.HasValue)
        {
          table.seat_count = table_data.Seat_Count.Value;
        }

        if (table_data.Is_Active.HasValue)
        {
          table.is_active = table_data.Is_Active.Value;
        }

        if (table_data.Qr_Code_Url != null)
        {
          table.QR_Code = table_data.Qr_Code_Url;
        }

        _context.Tables.Update(table);
        await _context.SaveChangesAsync();
        return Ok(table);
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
    /// <param name="table_id">The unique identifier of the table to delete.</param>
    /// <remarks>
    /// Returns <c>204 No Content</c> on successful deletion.  
    /// If the table is associated with an active dining session (<c>Ended_At == null</c>), deletion is blocked and a <c>409 Conflict</c> is returned.
    /// </remarks>
    /// <response code="204">The table was deleted successfully.</response>
    /// <response code="404">No table exists with the specified <paramref name="table_id"/>.</response>
    /// <response code="403">Only admins can delete tables.</response>
    /// <response code="409">The table cannot be deleted because it is currently in use.</response>
    /// <response code="500">An unexpected error occurred while deleting the table.</response>
    [Authorize(Policy = "adminOnly")]
    [HttpDelete("{table_id}")]
    public async Task<IActionResult> DeleteTable(int table_id)
    {
      try
      {
        var table = await _context.Tables
            .Include(t => t.TableGroup)
            .FirstOrDefaultAsync(t => t.Table_Id == table_id);

        if (table is null)
        {
          return NotFound("Could not find a table to delete");
        }

        // Check if table is in an active session (directly assigned)
        var activeSessionDirect = await _context.DiningSessions
            .AnyAsync(ds => ds.Table_Id == table_id && ds.Ended_At == null);

        if (activeSessionDirect)
        {
          return BadRequest("Cannot delete a table that is currently in use in an active session");
        }

        // Check if table is in an ended session (directly assigned)
        var inactiveSessionDirect = await _context.DiningSessions
            .AnyAsync(ds => ds.Table_Id == table_id && ds.Ended_At != null);

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
    /// <param name="table_id">The unique identifier of the table.</param>
    /// <remarks>
    /// - If the table is <c>inactive</c>, this endpoint **activates** it.  
    /// - If the table is <c>active</c>, this endpoint attempts to **deactivate** it.  
    /// - Deactivation is **blocked** when the table is part of an active dining session (<c>Ended_At == null</c>).  
    /// Returns the new status in the response body.
    /// </remarks>
    /// <response code="200">The table status was toggled; response includes the current status.</response>
    /// <response code="404">No table exists with the specified <paramref name="table_id"/>.</response>
    /// <response code="409">The table cannot be deactivated because it is currently in use.</response>
    /// <response code="500">An unexpected error occurred while toggling the table status.</response>
    [Authorize(Policy = "staffOnly")]
    [HttpPost("{table_id}/toggle-status")]
    public async Task<IActionResult> ToggleTableStatus(int table_id)
    {
      try
      {
        var table = await _context.Tables
            .Include(t => t.TableGroup)
            .FirstOrDefaultAsync(t => t.Table_Id == table_id);

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
            .AnyAsync(ds => ds.Table_Id == table_id && ds.Ended_At == null);

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
        return Ok(table);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error toggling table status");
        return StatusCode(500, "Internal Server Error");
      }
    }

    /// <summary>
    /// Checks whether a table currently has an active dining session.
    /// </summary>
    /// <param name="table_id">The unique identifier of the table.</param>
    /// <remarks>
    /// Returns <c>success=true</c> if the table has **no** active session (available), otherwise <c>success=false</c>.
    /// An active session is defined as a <c>DiningSession</c> where <c>Ended_At == null</c> and the session includes the given table.
    /// </remarks>
    /// <response code="200">Request succeeded; response indicates availability via the <c>success</c> flag.</response>
    /// <response code="403">The user is not authorized to perform this action.</response>
    /// <response code="500">An unexpected error occurred while checking availability.</response>
    [Authorize]
    [HttpPost("{table_id}/active-session")]
    public async Task<IActionResult> CheckTableActiveSession(int table_id)
    {
      try
      {
        var table = await _context.Tables
            .Include(t => t.TableGroup)
            .FirstOrDefaultAsync(t => t.Table_Id == table_id);

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
            .AnyAsync(ds => ds.Table_Id == table_id && ds.Ended_At == null);

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

    [Authorize(Policy = "staffOnly")]
    [HttpPost("{table_id}/assign-to-group/{table_group_id}")]
    public async Task<IActionResult> AssignTableToGroup(int table_id, int table_group_id)
    {
      try
      {
        var table = await _context.Tables
            .FirstOrDefaultAsync(t => t.Table_Id == table_id);

        if (table == null)
        {
          return NotFound("Table not found");
        }

        if (!await _locationAccess.CanAccessLocationAsync(User, table.Location_Id))
        {
          return LocationResults.OtherLocation();
        }

        var tableGroup = await _context.TableGroups
            .FirstOrDefaultAsync(tg => tg.TableGroup_Id == table_group_id);

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
            .AnyAsync(ds => ds.Table_Id == table_id && ds.Ended_At == null);

        if (activeSession)
        {
          return BadRequest("Cannot assign table to group while it's in an active session");
        }

        table.TableGroup_Id = table_group_id;
        _context.Tables.Update(table);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Table assigned to group successfully", table });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error assigning table to group");
        return StatusCode(500, "Internal Server Error");
      }
    }

    [Authorize(Policy = "staffOnly")]
    [HttpDelete("{table_id}/remove-from-group")]
    public async Task<IActionResult> RemoveTableFromGroup(int table_id)
    {
      try
      {
        var table = await _context.Tables
            .Include(t => t.TableGroup)
            .FirstOrDefaultAsync(t => t.Table_Id == table_id);

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

        return Ok(new { message = "Table removed from group successfully", table });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error removing table from group");
        return StatusCode(500, "Internal Server Error");
      }
    }

  }
}