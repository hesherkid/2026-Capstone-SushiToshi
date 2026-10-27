using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using Microsoft.AspNetCore.Authorization;
using back_end.DTO.TableGroupDTOs;
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

    [Authorize(Policy = "staffOnly")]
    [HttpPost]
    public async Task<IActionResult> CreateTableGroup([FromBody] TableGroupCreateDTO groupData)
    {
      try
      {
        // Validate location exists
        var location = await _context.Locations
            .FirstOrDefaultAsync(l => l.Location_Id == groupData.Location_Id);

        if (location == null)
        {
          return BadRequest("Location does not exist");
        }

        if (!await _locationAccess.CanAccessLocationAsync(User, groupData.Location_Id))
        {
          return LocationResults.OtherLocation();
        }

        // Check if group name already exists at this location
        var existingGroup = await _context.TableGroups
            .FirstOrDefaultAsync(tg => tg.Group_Name == groupData.Group_Name &&
                                       tg.Location_Id == groupData.Location_Id);

        if (existingGroup != null)
        {
          return BadRequest("Table group with this name already exists at this location");
        }

        var newTableGroup = new TableGroup
        {
          Group_Name = groupData.Group_Name,
          Is_Active = groupData.Is_Active ?? true,
          Location_Id = groupData.Location_Id,
          Created_At = DateTime.UtcNow
        };

        _context.TableGroups.Add(newTableGroup);
        await _context.SaveChangesAsync();

        return Ok(new
        {
          message = "Table group created successfully",
          tableGroup = newTableGroup
        });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error creating table group");
        return StatusCode(500, new { detail = "Error creating table group" });
      }
    }



    [Authorize(Policy = "staffOnly")]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TableGroup>>> GetAllTableGroups([FromQuery] bool? activeOnly, [FromQuery] int? locationId)
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

        var response = tableGroups.Select(tg => new
        {
          tableGroup_Id = tg.TableGroup_Id,
          group_Name = tg.Group_Name,
          is_Active = tg.Is_Active,
          location_Id = tg.Location_Id,
          location_Name = tg.Location?.Name,
          created_At = tg.Created_At,
          table_Count = tg.Tables.Count,
          tables = tg.Tables.Select(t => new
          {
            table_Id = t.Table_Id,
            table_number = t.table_number,
            seat_count = t.seat_count,
            is_active = t.is_active
          }).ToList()
        }).ToList();

        return Ok(response);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting table groups");
        return StatusCode(500, "Internal Server Error");
      }
    }

    [Authorize(Policy = "staffOnly")]
    [HttpGet("available")]
    public async Task<ActionResult<IEnumerable<TableGroup>>> GetAvailableTableGroups([FromQuery] int? locationId)
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

        var response = availableGroups.Select(tg => new
        {
          tableGroup_Id = tg.TableGroup_Id,
          group_Name = tg.Group_Name,
          is_Active = tg.Is_Active,
          location_Id = tg.Location_Id,
          location_Name = tg.Location?.Name,
          table_Count = tg.Tables.Count,
          total_Seats = tg.Tables.Sum(t => t.seat_count),
          tables = tg.Tables.Select(t => new
          {
            table_Id = t.Table_Id,
            table_number = t.table_number,
            seat_count = t.seat_count
          }).ToList()
        }).ToList();

        return Ok(response);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error getting available table groups");
        return StatusCode(500, "Internal Server Error");
      }
    }


    [Authorize(Policy = "staffOnly")]
    [HttpGet("{table_group_id}")]
    public async Task<IActionResult> GetTableGroup(int table_group_id)
    {
      try
      {
        var tableGroup = await _context.TableGroups
            .Include(tg => tg.Tables)
            .Include(tg => tg.DiningSessions.Where(ds => ds.Ended_At == null))
            .FirstOrDefaultAsync(tg => tg.TableGroup_Id == table_group_id);

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
          tableGroup_Id = tableGroup.TableGroup_Id,
          group_Name = tableGroup.Group_Name,
          is_Active = tableGroup.Is_Active,
          created_At = tableGroup.Created_At,
          table_Count = tableGroup.Tables.Count,
          total_Seats = tableGroup.Tables.Sum(t => t.seat_count),
          is_In_Active_Session = tableGroup.DiningSessions.Any(),
          tables = tableGroup.Tables.Select(t => new
          {
            table_Id = t.Table_Id,
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

    [Authorize(Policy = "staffOnly")]
    [HttpPut("{table_group_id}")]
    public async Task<IActionResult> UpdateTableGroup(int table_group_id, [FromBody] TableGroupUpdateDTO groupData)
    {
      try
      {
        var tableGroup = await _context.TableGroups
            .FirstOrDefaultAsync(tg => tg.TableGroup_Id == table_group_id);

        if (tableGroup == null)
        {
          return NotFound("Table group not found");
        }

        if (!await _locationAccess.CanAccessLocationAsync(User, tableGroup.Location_Id))
        {
          return LocationResults.OtherLocation();
        }

        // Check if group name is being changed and if new name already exists
        if (!string.IsNullOrEmpty(groupData.Group_Name) &&
            tableGroup.Group_Name != groupData.Group_Name)
        {
          var existingGroup = await _context.TableGroups
              .FirstOrDefaultAsync(tg => tg.Group_Name == groupData.Group_Name &&
                                         tg.TableGroup_Id != table_group_id);

          if (existingGroup != null)
          {
            return BadRequest("Table group with this name already exists");
          }

          tableGroup.Group_Name = groupData.Group_Name;
        }

        if (groupData.Is_Active.HasValue)
        {
          // Check if trying to deactivate a group that's in an active session
          if (!groupData.Is_Active.Value)
          {
            var isInActiveSession = await _context.DiningSessions
                .AnyAsync(ds => ds.TableGroup_Id == table_group_id && ds.Ended_At == null);

            if (isInActiveSession)
            {
              return BadRequest("Cannot deactivate table group that is in an active session");
            }
          }

          tableGroup.Is_Active = groupData.Is_Active.Value;
        }

        _context.TableGroups.Update(tableGroup);
        await _context.SaveChangesAsync();

        return Ok(new
        {
          message = "Table group updated successfully",
          tableGroup
        });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error updating table group");
        return StatusCode(500, "Internal Server Error");
      }
    }

    [Authorize(Policy = "staffOnly")]
    [HttpDelete("{table_group_id}")]
    public async Task<IActionResult> DeleteTableGroup(int table_group_id)
    {
      try
      {
        var tableGroup = await _context.TableGroups
            .Include(tg => tg.Tables)
            .Include(tg => tg.DiningSessions)
            .FirstOrDefaultAsync(tg => tg.TableGroup_Id == table_group_id);

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

    [Authorize(Policy = "staffOnly")]
    [HttpPost("{table_group_id}/tables/{table_id}")]
    public async Task<IActionResult> AddTableToGroup(int table_group_id, int table_id)
    {
      try
      {
        var tableGroup = await _context.TableGroups
            .Include(tg => tg.Tables)
            .FirstOrDefaultAsync(tg => tg.TableGroup_Id == table_group_id);

        if (tableGroup == null)
        {
          return NotFound("Table group not found");
        }

        if (!await _locationAccess.CanAccessLocationAsync(User, tableGroup.Location_Id))
        {
          return LocationResults.OtherLocation();
        }

        var table = await _context.Tables
            .FirstOrDefaultAsync(t => t.Table_Id == table_id);

        if (table == null)
        {
          return NotFound("Table not found");
        }

        // Validate that table and table group belong to same location
        if (table.Location_Id != tableGroup.Location_Id)
        {
          return BadRequest($"Table and table group must belong to the same location. Table is at location {table.Location_Id}, group is at location {tableGroup.Location_Id}");
        }

        if (table.TableGroup_Id.HasValue && table.TableGroup_Id.Value != table_group_id)
        {
          return BadRequest($"Table is already assigned to another group (Group ID: {table.TableGroup_Id})");
        }

        if (table.TableGroup_Id == table_group_id)
        {
          return BadRequest("Table is already in this group");
        }

        var isTableInActiveSession = await _context.DiningSessions
            .AnyAsync(ds => ds.Table_Id == table_id && ds.Ended_At == null);

        if (isTableInActiveSession)
        {
          return BadRequest("Cannot add table to group while it's in an active session");
        }

        table.TableGroup_Id = table_group_id;
        _context.Tables.Update(table);
        await _context.SaveChangesAsync();

        return Ok(new
        {
          message = "Table added to group successfully",
          tableGroup_Id = table_group_id,
          table_Id = table_id
        });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error adding table to group");
        return StatusCode(500, "Internal Server Error");
      }
    }

    [Authorize(Policy = "staffOnly")]
    [HttpDelete("{table_group_id}/tables/{table_id}")]
    public async Task<IActionResult> RemoveTableFromGroup(int table_group_id, int table_id)
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

        if (table.TableGroup_Id != table_group_id)
        {
          return BadRequest("Table is not in this group");
        }

        // Check if the table group is in an active session
        var isGroupInActiveSession = await _context.DiningSessions
            .AnyAsync(ds => ds.TableGroup_Id == table_group_id && ds.Ended_At == null);

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
          table_Id = table_id
        });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error removing table from group");
        return StatusCode(500, "Internal Server Error");
      }
    }

    [Authorize(Policy = "staffOnly")]
    [HttpPost("{table_group_id}/toggle-status")]
    public async Task<IActionResult> ToggleTableGroupStatus(int table_group_id)
    {
      try
      {
        var tableGroup = await _context.TableGroups
            .FirstOrDefaultAsync(tg => tg.TableGroup_Id == table_group_id);

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
              .AnyAsync(ds => ds.TableGroup_Id == table_group_id && ds.Ended_At == null);

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
          tableGroup
        });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error toggling table group status");
        return StatusCode(500, "Internal Server Error");
      }
    }

    [Authorize]
    [HttpGet("{table_group_id}/active-session")]
    public async Task<IActionResult> CheckTableGroupActiveSession(int table_group_id)
    {
      try
      {
        var tableGroup = await _context.TableGroups
            .FirstOrDefaultAsync(tg => tg.TableGroup_Id == table_group_id);

        if (tableGroup == null)
        {
          return NotFound("Table group not found");
        }

        if (!await _locationAccess.CanAccessLocationAsync(User, tableGroup.Location_Id))
        {
          return LocationResults.OtherLocation();
        }

        var isInActiveSession = await _context.DiningSessions
            .AnyAsync(ds => ds.TableGroup_Id == table_group_id && ds.Ended_At == null);

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