using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.Dtos;
using back_end.Helpers;
using System.Net;

namespace back_end.Controllers
{
  [ApiController]
  [Route("api/[controller]")]

  public class LocationController : ControllerBase
  {
    private readonly ApplicationDbContext _context;
    private readonly ILogger<LocationController> _logger;

    public LocationController(ApplicationDbContext context, ILogger<LocationController> logger)
    {
      _context = context;
      _logger = logger;
    }

    /// <summary>
    /// Retrieves all locations from the database.
    /// </summary>
    /// <response code="200">Returns the list of all locations</response>
    /// <response code="500">If an internal error occurs while retrieving locations</response>
    /// <remarks>
    /// Sample request:
    ///
    ///     GET /api/location
    ///
    /// Returns all locations in the system.
    /// </remarks>
    //GET api/location
    //Get all locations
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<LocationResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<LocationResponseDto>>> GetAllLocations()
    {
      try
      {
        var locations = await _context.Locations.OrderBy(l => l.Location_Id).ToListAsync();
        return Ok(locations.Select(LocationResponseDto.From).ToList());
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Cannot get all locations");
        return StatusCode(500, new { message = "An error occurred while processing your request." });
      }
    }

    /// <summary>
    /// Creates a new location in the system.
    /// </summary>
    /// <param name="createLocation">The location data including name, address, city, province, postal code, and phone number</param>
    /// <response code="200">Returns the newly created location</response>
    /// <response code="400">If the request data is invalid</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Not an admin.</response>
    /// <response code="500">If an internal error occurs while creating the location</response>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/location
    ///     {
    ///         "name": "Downtown Branch",
    ///         "address_one": "123 Main Street",
    ///         "address_two": "Suite 100",
    ///         "city": "Calgary",
    ///         "province": "Alberta",
    ///         "province_code": "AB",
    ///         "postal_code": "T2P 1A1",
    ///         "phone_number": "4035550100"
    ///     }
    ///
    /// Creates a new location with the provided information.
    /// </remarks>
    [Authorize(Policy = "adminOnly")]
    [HttpPost]
    [ProducesResponseType(typeof(LocationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateLocation(
        LocationCreateDto createLocation
    )
    {
      try
      {
        var newLocation = new Locations
        {
          Name = createLocation.Name,
          Address_Primary = createLocation.AddressOne,
          Address_Secondary = createLocation.AddressTwo,
          City = createLocation.City,
          Province = createLocation.Province,
          Province_Code = createLocation.ProvinceCode ?? "AB",
          Postal_Code = createLocation.PostalCode,
          Phone_Number = createLocation.PhoneNumber
        };
        _context.Locations.Add(newLocation);
        await _context.SaveChangesAsync();
        return Ok(LocationResponseDto.From(newLocation));
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "An error occurred while creating a location.");
        return StatusCode(500, new { message = "An error occurred while processing your request." });
      }
    }


    /// <summary>
    /// Retrieves a specific location by its ID.
    /// </summary>
    /// <param name="locationId">The unique identifier of the location</param>
    /// <response code="200">Returns the location with the specified ID</response>
    /// <response code="404">If the location is not found</response>
    /// <response code="500">If an internal error occurs while retrieving the location</response>
    /// <remarks>
    /// Sample request:
    ///
    ///     GET /api/location/123
    ///
    /// Returns the complete location details for the specified ID.
    /// </remarks>
    [HttpGet("{locationId}")]
    [ProducesResponseType(typeof(LocationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetLocations(
        int locationId
    )
    {
      try
      {
        var location = await _context.Locations.Where(l => l.Location_Id == locationId).FirstOrDefaultAsync();
        if (location == null)
        {
          return NotFound("Location was not Found");
        }
        return Ok(LocationResponseDto.From(location));


      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "An error occurred while retrieving the location {LocationId}.", locationId);
        return StatusCode(500, new { message = "An error occurred while processing your request." });
      }
    }



    /// <summary>
    /// Updates an existing location's information.
    /// </summary>
    /// <param name="locationId">The unique identifier of the location to update</param>
    /// <param name="locationUpdate">The updated location data</param>
    /// <response code="200">Returns the updated location</response>
    /// <response code="400">If the request data is invalid</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Not an admin.</response>
    /// <response code="404">If the location is not found</response>
    /// <response code="500">If an internal error occurs while updating the location</response>
    /// <remarks>
    /// Sample request:
    ///
    ///     PUT /api/location/123
    ///     {
    ///         "name": "Downtown Branch",
    ///         "address_one": "123 Main Street",
    ///         "address_two": "Suite 100",
    ///         "city": "Calgary",
    ///         "province": "Alberta",
    ///         "province_code": "AB",
    ///         "postal_code": "T2P 1A1",
    ///         "phone_number": "4035550100"
    ///     }
    ///
    /// All fields in the request body are optional - only provided fields will be updated.
    /// </remarks>
    [Authorize(Policy = "adminOnly")]
    [HttpPut("{locationId}")]
    [ProducesResponseType(typeof(LocationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateLocation(
        int locationId,
        LocationUpdateDto locationUpdate
    )
    {
      try
      {
        var location = await _context.Locations.Where(l => l.Location_Id == locationId).FirstOrDefaultAsync();
        if (location == null)
        {
          return NotFound("Location to be updated was not found.");
        }
        if (!string.IsNullOrEmpty(locationUpdate.Name))
        {
          location.Name = locationUpdate.Name;
        }
        if (!string.IsNullOrEmpty(locationUpdate.AddressOne))
        {
          location.Address_Primary = locationUpdate.AddressOne;
        }
        if (!string.IsNullOrEmpty(locationUpdate.AddressTwo))
        {
          location.Address_Secondary = locationUpdate.AddressTwo;
        }
        if (!string.IsNullOrEmpty(locationUpdate.City))
        {
          location.City = locationUpdate.City;
        }
        if (!string.IsNullOrEmpty(locationUpdate.Province))
        {
          location.Province = locationUpdate.Province;
        }
        if (!string.IsNullOrEmpty(locationUpdate.ProvinceCode))
        {
          location.Province_Code = locationUpdate.ProvinceCode;
        }
        if (!string.IsNullOrEmpty(locationUpdate.PostalCode))
        {
          location.Postal_Code = locationUpdate.PostalCode;
        }
        if (!string.IsNullOrEmpty(locationUpdate.PhoneNumber))
        {
          location.Phone_Number = locationUpdate.PhoneNumber;
        }

        _context.Update(location);
        await _context.SaveChangesAsync();
        return Ok(LocationResponseDto.From(location));
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "An error occurred while updating the location {LocationId}.", locationId);
        return StatusCode(500, new { message = "An error occurred while processing your request." });
      }
    }

    /// <summary>
    /// The location's Wi-Fi check: the networks guests must join a table from.
    /// </summary>
    /// <remarks>
    /// Also returns the admin's own address and network as the server sees them, so an admin at the
    /// restaurant can add its Wi-Fi without looking it up. Admins only: the networks are the
    /// restaurant's public addresses, so they aren't in the public location list.
    /// </remarks>
    /// <response code="200">The location's networks (empty when the check is off).</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Not an admin.</response>
    /// <response code="404">The location doesn't exist.</response>
    [Authorize(Policy = "adminOnly")]
    [HttpGet("{locationId}/wifi-check")]
    [ProducesResponseType(typeof(WifiCheckResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWifiCheck(int locationId)
    {
      var location = await _context.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Location_Id == locationId);
      if (location == null)
      {
        return NotFound(new { message = "Location was not found." });
      }

      return Ok(WifiCheckFor(location));
    }

    /// <summary>
    /// Sets the networks guests must join a table from. An empty list turns the check off.
    /// </summary>
    /// <remarks>
    /// Each entry is an address (203.0.113.10) or a network (192.168.1.0/24, 2001:db8:1:2::/64).
    /// Sample request:
    ///
    ///     PUT /api/location/2/wifi-check
    ///     { "allowed_networks": ["203.0.113.10"] }
    /// </remarks>
    /// <response code="200">Saved; returns the location's networks.</response>
    /// <response code="400">An entry isn't a valid address or network, or there are too many.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Not an admin.</response>
    /// <response code="404">The location doesn't exist.</response>
    [Authorize(Policy = "adminOnly")]
    [HttpPut("{locationId}/wifi-check")]
    [ProducesResponseType(typeof(WifiCheckResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateWifiCheck(int locationId, WifiCheckUpdateDto request)
    {
      var location = await _context.Locations.FirstOrDefaultAsync(l => l.Location_Id == locationId);
      if (location == null)
      {
        return NotFound(new { message = "Location was not found." });
      }

      var (stored, error) = RestaurantNetwork.Normalize(request.AllowedNetworks);
      if (error != null)
      {
        return BadRequest(new { message = error });
      }

      location.Allowed_Networks = stored;
      location.Updated_At = DateTime.UtcNow;
      await _context.SaveChangesAsync();

      _logger.LogInformation("Wi-Fi check for location {LocationId} set to {Networks}", locationId, stored ?? "off");
      return Ok(WifiCheckFor(location));
    }

    private WifiCheckResponseDto WifiCheckFor(Locations location)
    {
      var address = HttpContext?.Connection.RemoteIpAddress;
      if (address?.IsIPv4MappedToIPv6 == true) address = address.MapToIPv4();

      return new WifiCheckResponseDto
      {
        AllowedNetworks = RestaurantNetwork.Split(location.Allowed_Networks),
        YourAddress = address?.ToString(),
        YourNetwork = RestaurantNetwork.SuggestedNetwork(address),
        CanSeeClientAddresses = address != null && !IPAddress.IsLoopback(address)
      };
    }

    /// <summary>
    /// Deletes a location from the system.
    /// </summary>
    /// <param name="locationId">The unique identifier of the location to delete</param>
    /// <response code="200">Returns a success message when the location is deleted</response>
    /// <response code="400">The location has an open dining session, tables, or table groups.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Not an admin.</response>
    /// <response code="404">If the location is not found</response>
    /// <response code="500">If an internal error occurs while deleting the location</response>
    /// <remarks>
    /// Sample request:
    ///
    ///     DELETE /api/location/123
    ///
    /// Permanently removes the location from the database.
    /// </remarks>
    [Authorize(Policy = "adminOnly")]
    [HttpDelete("{locationId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteLocation(int locationId)
    {
      try
      {
        var deleteLocation = await _context.Locations
            .FirstOrDefaultAsync(l => l.Location_Id == locationId);

        if (deleteLocation == null)
        {
          return NotFound("Location was not found to delete");
        }

        // Check for active dining sessions
        var hasActiveSessions = await _context.DiningSessions
            .AnyAsync(ds => ds.Location_Id == locationId && ds.Ended_At == null);

        if (hasActiveSessions)
        {
          return BadRequest("Cannot delete location with active dining sessions");
        }

        // Check for tables
        var hasTables = await _context.Tables
            .AnyAsync(t => t.Location_Id == locationId);

        if (hasTables)
        {
          return BadRequest("Cannot delete location with existing tables. Delete or reassign tables first.");
        }

        // Check for table groups
        var hasTableGroups = await _context.TableGroups
            .AnyAsync(tg => tg.Location_Id == locationId);

        if (hasTableGroups)
        {
          return BadRequest("Cannot delete location with existing table groups. Delete or reassign table groups first.");
        }

        // Anyone working here moves to another location they're assigned to, or to none.
        // (Their assignment to this location is deleted with it.)
        var workingHere = await _context.Users.Where(u => u.Location_id == locationId).ToListAsync();
        foreach (var user in workingHere)
        {
          user.Location_id = await _context.StaffLocations
              .Where(sl => sl.User_Id == user.User_id && sl.Location_Id != locationId)
              .OrderBy(sl => sl.Location_Id)
              .Select(sl => (int?)sl.Location_Id)
              .FirstOrDefaultAsync();
        }

        _context.Remove(deleteLocation);
        await _context.SaveChangesAsync();
        return Ok("Location was deleted from the system.");
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "An error occurred while deleting the location {LocationId}.", locationId);
        return StatusCode(500, new { message = "An error occurred while processing your request." });
      }
    }



    /// <summary>
    /// Associates a menu with a specific location.
    /// </summary>
    /// <param name="locationId">The unique identifier of the location</param>
    /// <param name="menuLocationCreate">The menu location data containing the menu ID</param>
    /// <response code="200">Returns a success message when the menu is added to the location</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Not an admin.</response>
    /// <response code="404">If the location is not found</response>
    /// <response code="500">If an internal error occurs while adding the menu to the location</response>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/location/123/menus
    ///     {
    ///         "menu_id": 456
    ///     }
    ///
    /// Creates an association between the specified menu and location.
    /// The location must exist. An unknown menu or one already assigned here will result in an error.
    /// </remarks>
    [Authorize(Policy = "adminOnly")]
    [HttpPost("{locationId}/menus")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AddMenuToLocation(
       int locationId,
       MenuLocationCreateDto menuLocationCreate
   )
    {
      try
      {
        var location = _context.Locations.Where(l => l.Location_Id == locationId).FirstOrDefault();
        if (location == null)
        {
          return NotFound("Location was not found");
        }
        _context.MenuLocations.Add(new MenuLocations
        {
          Location_Id = locationId,
          Menu_Id = menuLocationCreate.MenuId
        });
        await _context.SaveChangesAsync();
        return Ok("Menu Location was Added Successfully ");


      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "An error occurred while adding menu {MenuId} to location {LocationId}.", menuLocationCreate.MenuId, locationId);
        return StatusCode(500, new { message = "An error occurred while processing your request." });
      }


    }

    /// <summary>
    /// Removes a menu from a specific location.
    /// </summary>
    /// <param name="locationId">The unique identifier of the location</param>
    /// <param name="menuId">The unique identifier of the menu</param>
    /// <response code="200">Returns a success message when the menu is removed from the location</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Not an admin.</response>
    /// <response code="404">If the menu-location association is not found</response>
    /// <response code="500">If an internal error occurs while removing the menu from the location</response>
    /// <remarks>
    /// Sample request:
    ///
    ///     DELETE /api/location/123/menus/456
    ///
    /// Removes the association between the specified menu and location.
    /// The menu itself is not deleted, only its assignment to this location.
    /// </remarks>
    [Authorize(Policy = "adminOnly")]
    [HttpDelete("{locationId}/menus/{menuId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RemoveMenuFromLocation(
       int locationId,
       int menuId
   )
    {
      try
      {
        var menuDeletion = await _context.MenuLocations.Where(ml => ml.Location_Id == locationId && ml.Menu_Id == menuId).FirstOrDefaultAsync();
        if (menuDeletion == null)
        {
          return NotFound("Menu Location was not found.");
        }
        _context.Remove(menuDeletion);
        await _context.SaveChangesAsync();
        return Ok("Menu was Deleted From the location.");

      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error occurred while removing menu {MenuId} from location {LocationId}.", menuId, locationId);
        return StatusCode(500, new { message = "An error occurred while processing your request." });
      }
    }

    /// <summary>
    /// Retrieves all menu IDs associated with a specific location.
    /// </summary>
    /// <param name="locationId">The unique identifier of the location</param>
    /// <response code="200">Returns the list of menu IDs for the specified location</response>
    /// <response code="404">If the location is not found</response>
    /// <response code="500">If an internal error occurs while retrieving menu IDs</response>
    /// <remarks>
    /// Sample request:
    ///
    ///     GET /api/location/123/menus
    ///
    /// Returns an array of menu IDs associated with the specified location.
    /// </remarks>
    [HttpGet("{locationId}/menus")]
    [ProducesResponseType(typeof(IEnumerable<int>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ListLocationMenus(
        int locationId
    )
    {
      try
      {
        var location = await _context.Locations.Where(l => l.Location_Id == locationId).FirstOrDefaultAsync();
        if (location == null)
        {
          return NotFound("Location was not found.");
        }
        var menuIds = await _context.MenuLocations
            .Where(ml => ml.Location_Id == locationId)
            .Select(ml => ml.Menu_Id)  // Only select the Menu_Id
            .ToListAsync();

        return Ok(menuIds);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error occurred while listing menus for location {LocationId}.", locationId);
        return StatusCode(500, new { message = "An error occurred while processing your request." });
      }
    }
  }
}