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
  public class TableGroupController : ControllerBase
  {
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TableGroupController> _logger;
    private readonly ILocationAccessService _locationAccess;

    public TableGroupController(ApplicationDbContext context, ILogger<TableGroupController> logger, ILocationAccessService locationAccess)
    {
      _context = context;
      _logger = logger;
      _locationAccess = locationAccess;
    }

    /// <summary>
    /// Creates a table group at a location, for seating a big party across several tables.
    /// </summary>
    /// <param name="groupData">The group's name, active flag and location</param>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/tablegroup
    ///     { "group_name": "Patio", "is_active": true, "location_id": 1 }
    ///
    /// - Group names must be unique within a location.
    /// - is_active defaults to true. The group starts with no tables; add them with
    ///   POST /api/tablegroup/{tableGroupId}/tables/{tableId}.
    /// </remarks>
    /// <response code="200">message and the new group.</response>
    /// <response code="400">The location doesn't exist, or the name is already used there.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Not staff, or the location isn't one the caller works at.</response>
    /// <response code="500">The group couldn't be saved.</response>
    [Authorize(Policy = "staffOnly")]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateTableGroup([FromBody] TableGroupCreateDto groupData)
    {
      try
      {
        // Validate location exists
        var location = await _context.Locations
            .FirstOrDefaultAsync(l => l.Location_Id == groupData.LocationId);

        if (location == null)
        {
          return BadRequest("Location does not exist");
        }

        if (!await _locationAccess.CanAccessLocationAsync(User, groupData.LocationId))
        {
          return LocationResults.OtherLocation();
        }

        // Check if group name already exists at this location
        var existingGroup = await _context.TableGroups
            .FirstOrDefaultAsync(tg => tg.Group_Name == groupData.GroupName &&
                                       tg.Location_Id == groupData.LocationId);

        if (existingGroup != null)
        {
          return BadRequest("Table group with this name already exists at this location");
        }

        var newTableGroup = new TableGroup
        {
          Group_Name = groupData.GroupName,
          Is_Active = groupData.IsActive ?? true,
          Location_Id = groupData.LocationId,
          Created_At = DateTime.UtcNow
        };

        _context.TableGroups.Add(newTableGroup);
        await _context.SaveChangesAsync();

        return Ok(new
        {
          message = "Table group created successfully",
          table_group = TableGroupResponseDto.From(newTableGroup)
        });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error creating table group");
        return StatusCode(500, new { message = "Error creating table group" });
      }
    }

    /// <summary>
    /// Every table group with its tables, by location then name. Staff get their own location.
    /// </summary>
    /// <param name="activeOnly">true to leave out inactive groups</param>
    /// <param name="locationId">Optional location filter</param>
    /// <remarks>
    /// Sample request:
    ///
    ///     GET /api/tablegroup?active_only=true&amp;location_id=1
    /// </remarks>
    /// <response code="200">The groups (can be empty).</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Not staff, or a staff member with no working location.</response>
    /// <response code="500">The groups couldn't be loaded.</response>
    [Authorize(Policy = "staffOnly")]
    [HttpGet]
    [ProducesResponseType(typeof(List<TableGroupResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<TableGroupResponseDto>>> GetAllTableGroups([FromQuery(Name = "active_only")] bool? activeOnly, [FromQuery(Name = "location_id")] int? locationId)
    {
      try
      {
        var scope = await _locationAccess.ResolveLocationAsync(User, locationId);
        if (scope.IsForbidden) return LocationResults.NoLocation();
        locationId = scope.LocationId;

        var query = _context.TableGroups
            .Include(tg => tg.Tables)
            .Include(tg => tg.Location)
            .AsQueryable();

        if (activeOnly.HasValue && activeOnly.Value)
        {
          query = query.Where(tg => tg.Is_Active);
        }

        if (locationId.HasValue)
        {
          query = query.Where(tg => tg.Location_Id == locationId.Value);
        }

        var tableGroups = await query
            .OrderBy(tg => tg.Location_Id)
            .ThenBy(tg => tg.Group_Name)
            .ToListAsync();

        var response = tableGroups.Select(TableGroupResponseDto.From).ToList();

        return Ok(response);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting table groups");
        return StatusCode(500, "Internal Server Error");
      }
    }

    /// <summary>
    /// Active table groups with no open dining session, by name. Staff get their own location.
    /// </summary>
    /// <param name="locationId">Optional location filter</param>
    /// <remarks>
    /// Sample request:
    ///
    ///     GET /api/tablegroup/available?location_id=1
    ///
    /// A group counts as available even if one of its tables is seated on its own.
    /// </remarks>
    /// <response code="200">The available groups (can be empty).</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Not staff, or a staff member with no working location.</response>
    /// <response code="500">The groups couldn't be loaded.</response>
    [Authorize(Policy = "staffOnly")]
    [HttpGet("available")]
    [ProducesResponseType(typeof(List<TableGroupResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<TableGroupResponseDto>>> GetAvailableTableGroups([FromQuery(Name = "location_id")] int? locationId)
    {
      try
      {
        var scope = await _locationAccess.ResolveLocationAsync(User, locationId);
        if (scope.IsForbidden) return LocationResults.NoLocation();
        locationId = scope.LocationId;

        var query = _context.TableGroups
            .Include(tg => tg.Tables)
            .Include(tg => tg.Location)
            .Where(tg => tg.Is_Active &&
                        !tg.DiningSessions.Any(ds => ds.Ended_At == null));

        if (locationId.HasValue)
        {
          query = query.Where(tg => tg.Location_Id == locationId.Value);
        }

        var availableGroups = await query
            .OrderBy(tg => tg.Group_Name)
            .ToListAsync();

        var response = availableGroups.Select(TableGroupResponseDto.From).ToList();

        return Ok(response);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting available table groups");
        return StatusCode(500, "Internal Server Error");
      }
    }

    /// <summary>
    /// One table group, with its tables and whether it's in an open dining session.
    /// </summary>
    /// <param name="tableGroupId">The group</param>
    /// <remarks>
    /// Sample request:
    ///
    ///     GET /api/tablegroup/3
    ///
    /// total_seats adds up the seats of every table in the group, active or not.
    /// </remarks>
    /// <response code="200">The group, its tables (with QR codes) and is_in_active_session.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Not staff, or the group is at a location the caller doesn't work at.</response>
    /// <response code="404">The group doesn't exist.</response>
    /// <response code="500">The group couldn't be loaded.</response>
    [Authorize(Policy = "staffOnly")]
    [HttpGet("{tableGroupId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetTableGroup(int tableGroupId)
    {
      try
      {
        var tableGroup = await _context.TableGroups
            .Include(tg => tg.Tables)
            .Include(tg => tg.DiningSessions.Where(ds => ds.Ended_At == null))
            .FirstOrDefaultAsync(tg => tg.TableGroup_Id == tableGroupId);

        if (tableGroup == null)
        {
          return NotFound("Table group not found");
        }

        if (!await _locationAccess.CanAccessLocationAsync(User, tableGroup.Location_Id))
        {
          return LocationResults.OtherLocation();
        }

        var response = new
        {
          table_group_id = tableGroup.TableGroup_Id,
          group_name = tableGroup.Group_Name,
          is_active = tableGroup.Is_Active,
          created_at = tableGroup.Created_At,
          table_count = tableGroup.Tables.Count,
          total_seats = tableGroup.Tables.Sum(t => t.seat_count),
          is_in_active_session = tableGroup.DiningSessions.Any(),
          tables = tableGroup.Tables.Select(t => new
          {
            table_id = t.Table_Id,
            table_number = t.table_number,
            seat_count = t.seat_count,
            is_active = t.is_active,
            qr_code = t.QR_Code
          }).ToList()
        };

        return Ok(response);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting table group");
        return StatusCode(500, "Internal Server Error");
      }
    }

    /// <summary>
    /// Renames a table group, or turns it on or off.
    /// </summary>
    /// <param name="tableGroupId">The group</param>
    /// <param name="groupData">The fields to change; leave out the ones to keep</param>
    /// <remarks>
    /// Sample request:
    ///
    ///     PUT /api/tablegroup/3
    ///     { "group_name": "Patio", "is_active": false }
    ///
    /// - A new name must be unused at the group's location.
    /// - A group can't be turned off while it has an open dining session.
    /// </remarks>
    /// <response code="200">message and the updated group.</response>
    /// <response code="400">The name is already used at this location, or the group is in an open session.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Not staff, or the group is at a location the caller doesn't work at.</response>
    /// <response code="404">The group doesn't exist.</response>
    /// <response code="500">The group couldn't be saved.</response>
    [Authorize(Policy = "staffOnly")]
    [HttpPut("{tableGroupId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateTableGroup(int tableGroupId, [FromBody] TableGroupUpdateDto groupData)
    {
      try
      {
        var tableGroup = await _context.TableGroups
            .Include(tg => tg.Tables)
            .Include(tg => tg.Location)
            .FirstOrDefaultAsync(tg => tg.TableGroup_Id == tableGroupId);

        if (tableGroup == null)
        {
          return NotFound("Table group not found");
        }

        if (!await _locationAccess.CanAccessLocationAsync(User, tableGroup.Location_Id))
        {
          return LocationResults.OtherLocation();
        }

        // Check if group name is being changed and if new name already exists
        if (!string.IsNullOrEmpty(groupData.GroupName) &&
            tableGroup.Group_Name != groupData.GroupName)
        {
          var existingGroup = await _context.TableGroups
              .FirstOrDefaultAsync(tg => tg.Group_Name == groupData.GroupName &&
                               tg.Location_Id == tableGroup.Location_Id &&
                               tg.TableGroup_Id != tableGroupId);

          if (existingGroup != null)
          {
            return BadRequest("Table group with this name already exists");
          }

          tableGroup.Group_Name = groupData.GroupName;
        }

        if (groupData.IsActive.HasValue)
        {
          // Check if trying to deactivate a group that's in an active session
          if (!groupData.IsActive.Value)
          {
            var isInActiveSession = await _context.DiningSessions
                .AnyAsync(ds => ds.TableGroup_Id == tableGroupId && ds.Ended_At == null);

            if (isInActiveSession)
            {
              return BadRequest("Cannot deactivate table group that is in an active session");
            }
          }

          tableGroup.Is_Active = groupData.IsActive.Value;
        }

        _context.TableGroups.Update(tableGroup);
        await _context.SaveChangesAsync();

        return Ok(new
        {
          message = "Table group updated successfully",
          table_group = TableGroupResponseDto.From(tableGroup)
        });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error updating table group");
        return StatusCode(500, "Internal Server Error");
      }
    }

    /// <summary>
    /// Deletes a table group that has no tables and has never been used.
    /// </summary>
    /// <param name="tableGroupId">The group</param>
    /// <remarks>
    /// Sample request:
    ///
    ///     DELETE /api/tablegroup/3
    ///
    /// A group that has ever had a dining session can't be deleted, so its history stays intact;
    /// turn it off instead. Remove its tables first.
    /// </remarks>
    /// <response code="200">The group was deleted.</response>
    /// <response code="400">The group is in use, has been used before, or still has tables.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Not staff, or the group is at a location the caller doesn't work at.</response>
    /// <response code="404">The group doesn't exist.</response>
    /// <response code="500">The group couldn't be deleted.</response>
    [Authorize(Policy = "staffOnly")]
    [HttpDelete("{tableGroupId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteTableGroup(int tableGroupId)
    {
      try
      {
        var tableGroup = await _context.TableGroups
            .Include(tg => tg.Tables)
            .Include(tg => tg.DiningSessions)
            .FirstOrDefaultAsync(tg => tg.TableGroup_Id == tableGroupId);

        if (tableGroup == null)
        {
          return NotFound("Table group not found");
        }

        if (!await _locationAccess.CanAccessLocationAsync(User, tableGroup.Location_Id))
        {
          return LocationResults.OtherLocation();
        }

        // Check if group is in an active session
        var isInActiveSession = tableGroup.DiningSessions.Any(ds => ds.Ended_At == null);

        if (isInActiveSession)
        {
          return BadRequest("Cannot delete table group that is in an active session");
        }

        // Check if group is in an active session
        var isInEndedSession = tableGroup.DiningSessions.Any(ds => ds.Ended_At != null);

        if (isInEndedSession)
        {
          return BadRequest("Cannot delete table group that is in an ended session");
        }

        // Check if group has tables assigned
        if (tableGroup.Tables.Any())
        {
          return BadRequest($"Cannot delete table group with {tableGroup.Tables.Count} assigned tables. Remove tables from group first.");
        }

        _context.TableGroups.Remove(tableGroup);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Table group deleted successfully" });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error deleting table group");
        return StatusCode(500, "Internal Server Error");
      }
    }

    /// <summary>
    /// Adds a table to a table group at the same location.
    /// </summary>
    /// <param name="tableGroupId">The group</param>
    /// <param name="tableId">The table</param>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/tablegroup/3/tables/7
    ///
    /// The table can't already be in a group (remove it from the other group first), and can't be
    /// seated on its own right now.
    /// </remarks>
    /// <response code="200">message, table_group_id and table_id.</response>
    /// <response code="400">Different locations, the table is already in this or another group, or the
    /// table is in an open session.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Not staff, or the group is at a location the caller doesn't work at.</response>
    /// <response code="404">The group or the table doesn't exist.</response>
    /// <response code="500">The change couldn't be saved.</response>
    [Authorize(Policy = "staffOnly")]
    [HttpPost("{tableGroupId}/tables/{tableId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AddTableToGroup(int tableGroupId, int tableId)
    {
      try
      {
        var tableGroup = await _context.TableGroups
            .Include(tg => tg.Tables)
            .FirstOrDefaultAsync(tg => tg.TableGroup_Id == tableGroupId);

        if (tableGroup == null)
        {
          return NotFound("Table group not found");
        }

        if (!await _locationAccess.CanAccessLocationAsync(User, tableGroup.Location_Id))
        {
          return LocationResults.OtherLocation();
        }

        var table = await _context.Tables
            .FirstOrDefaultAsync(t => t.Table_Id == tableId);

        if (table == null)
        {
          return NotFound("Table not found");
        }

        // Validate that table and table group belong to same location
        if (table.Location_Id != tableGroup.Location_Id)
        {
          return BadRequest($"Table and table group must belong to the same location. Table is at location {table.Location_Id}, group is at location {tableGroup.Location_Id}");
        }

        if (table.TableGroup_Id.HasValue && table.TableGroup_Id.Value != tableGroupId)
        {
          return BadRequest($"Table is already assigned to another group (Group ID: {table.TableGroup_Id})");
        }

        if (table.TableGroup_Id == tableGroupId)
        {
          return BadRequest("Table is already in this group");
        }

        var isTableInActiveSession = await _context.DiningSessions
            .AnyAsync(ds => ds.Table_Id == tableId && ds.Ended_At == null);

        if (isTableInActiveSession)
        {
          return BadRequest("Cannot add table to group while it's in an active session");
        }

        table.TableGroup_Id = tableGroupId;
        _context.Tables.Update(table);
        await _context.SaveChangesAsync();

        return Ok(new
        {
          message = "Table added to group successfully",
          table_group_id = tableGroupId,
          table_id = tableId
        });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error adding table to group");
        return StatusCode(500, "Internal Server Error");
      }
    }

    /// <summary>
    /// Takes a table out of a table group.
    /// </summary>
    /// <param name="tableGroupId">The group the table is in</param>
    /// <param name="tableId">The table</param>
    /// <remarks>
    /// Sample request:
    ///
    ///     DELETE /api/tablegroup/3/tables/7
    ///
    /// Not allowed while the group has an open dining session.
    /// </remarks>
    /// <response code="200">message and table_id.</response>
    /// <response code="400">The table isn't in this group, or the group is in an open session.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Not staff, or the table is at a location the caller doesn't work at.</response>
    /// <response code="404">The table doesn't exist.</response>
    /// <response code="500">The change couldn't be saved.</response>
    [Authorize(Policy = "staffOnly")]
    [HttpDelete("{tableGroupId}/tables/{tableId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RemoveTableFromGroup(int tableGroupId, int tableId)
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

        if (table.TableGroup_Id != tableGroupId)
        {
          return BadRequest("Table is not in this group");
        }

        // Check if the table group is in an active session
        var isGroupInActiveSession = await _context.DiningSessions
            .AnyAsync(ds => ds.TableGroup_Id == tableGroupId && ds.Ended_At == null);

        if (isGroupInActiveSession)
        {
          return BadRequest("Cannot remove table from group while the group is in an active session");
        }

        table.TableGroup_Id = null;
        _context.Tables.Update(table);
        await _context.SaveChangesAsync();

        return Ok(new
        {
          message = "Table removed from group successfully",
          table_id = tableId
        });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error removing table from group");
        return StatusCode(500, "Internal Server Error");
      }
    }

    /// <summary>
    /// Switches a table group between active and inactive.
    /// </summary>
    /// <param name="tableGroupId">The group</param>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/tablegroup/3/toggle-status
    ///
    /// A group can't be turned off while it has an open dining session; turning it on is always allowed.
    /// </remarks>
    /// <response code="200">message and the group with its new is_active value.</response>
    /// <response code="400">The group is in an open session.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Not staff, or the group is at a location the caller doesn't work at.</response>
    /// <response code="404">The group doesn't exist.</response>
    /// <response code="500">The change couldn't be saved.</response>
    [Authorize(Policy = "staffOnly")]
    [HttpPost("{tableGroupId}/toggle-status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ToggleTableGroupStatus(int tableGroupId)
    {
      try
      {
        var tableGroup = await _context.TableGroups
            .Include(tg => tg.Tables)
            .Include(tg => tg.Location)
            .FirstOrDefaultAsync(tg => tg.TableGroup_Id == tableGroupId);

        if (tableGroup == null)
        {
          return NotFound("Table group not found");
        }

        if (!await _locationAccess.CanAccessLocationAsync(User, tableGroup.Location_Id))
        {
          return LocationResults.OtherLocation();
        }

        // If trying to deactivate, check if in active session
        if (tableGroup.Is_Active)
        {
          var isInActiveSession = await _context.DiningSessions
              .AnyAsync(ds => ds.TableGroup_Id == tableGroupId && ds.Ended_At == null);

          if (isInActiveSession)
          {
            return BadRequest("Cannot deactivate table group that is in an active session");
          }
        }

        tableGroup.Is_Active = !tableGroup.Is_Active;
        _context.TableGroups.Update(tableGroup);
        await _context.SaveChangesAsync();

        return Ok(new
        {
          message = $"Table group {(tableGroup.Is_Active ? "activated" : "deactivated")} successfully",
          table_group = TableGroupResponseDto.From(tableGroup)
        });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error toggling table group status");
        return StatusCode(500, "Internal Server Error");
      }
    }

    /// <summary>
    /// Whether a table group is free: no open dining session on the group.
    /// </summary>
    /// <param name="tableGroupId">The group</param>
    /// <remarks>
    /// Sample request:
    ///
    ///     GET /api/tablegroup/3/active-session
    ///
    /// success is true when the group is free, false when it's in use; message says which.
    /// Only sessions on the group count, not tables in it seated on their own.
    /// </remarks>
    /// <response code="200">success and message.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">The group is at a location the caller doesn't work at.</response>
    /// <response code="404">The group doesn't exist.</response>
    /// <response code="500">The check couldn't be done.</response>
    [Authorize]
    [HttpGet("{tableGroupId}/active-session")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CheckTableGroupActiveSession(int tableGroupId)
    {
      try
      {
        var tableGroup = await _context.TableGroups
            .FirstOrDefaultAsync(tg => tg.TableGroup_Id == tableGroupId);

        if (tableGroup == null)
        {
          return NotFound("Table group not found");
        }

        if (!await _locationAccess.CanAccessLocationAsync(User, tableGroup.Location_Id))
        {
          return LocationResults.OtherLocation();
        }

        var isInActiveSession = await _context.DiningSessions
            .AnyAsync(ds => ds.TableGroup_Id == tableGroupId && ds.Ended_At == null);

        if (isInActiveSession)
        {
          return Ok(new
          {
            success = false,
            message = "Table group is in an active session"
          });
        }

        return Ok(new
        {
          success = true,
          message = "Table group is available"
        });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error checking table group active session");
        return StatusCode(500, "Internal Server Error");
      }
    }
  }
}