using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.Dtos;
using Microsoft.AspNetCore.Authorization;
using back_end.Helpers;

namespace back_end.Controllers
{
    [ApiController]
    [Route("api/[controller]")]

    public class MenuController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MenuController> _logger;

        public MenuController(ApplicationDbContext context, ILogger<MenuController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Creates a new menu.
        /// </summary>
        /// <remarks>
        /// Sample request:
        ///
        ///     POST /api/menu/createmenu
        ///     { "name": "Dinner", "description": "Evening menu", "start_time": "17:00:00",
        ///       "end_time": "22:00:00", "is_active": true }
        ///
        /// Menu names must be unique. The new menu isn't offered anywhere until it's added to a 
        /// location (POST /api/location/{id}/menus).
        /// </remarks>
        /// <response code="200">Returns the new menu.</response>
        /// <response code="400">The body is missing or invalid.</response>
        /// <response code="401">Not signed in.</response>
        /// <response code="403">Not an admin.</response>
        /// <response code="409">A menu with that name already exists.</response>
        /// <response code="500">An unexpected error occurred.</response>
        [Authorize(Policy = "adminOnly")]
        [HttpPost("createmenu")]
        [ProducesResponseType(typeof(MenuResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateMenu(
            MenuCreateDto itemData
        )
        {
            try
            {
                if (itemData is null)
                {
                    return BadRequest("Item Data not Sent");
                }
                var isExist = await _context.Menus.FirstOrDefaultAsync(m => m.Name == itemData.Name);
                if (isExist != null)
                {
                    return Conflict("Menu Name and item Exist");
                }
                var newMenu = new Menu
                {
                    Name = itemData.Name,
                    Description = itemData.Description ?? string.Empty,
                    Start_time = itemData.StartTime ?? new TimeOnly(0, 0, 1),
                    End_time = itemData.EndTime ?? new TimeOnly(23, 59, 59),
                    Is_active = itemData.IsActive
                };
                _context.Add(newMenu);
                await _context.SaveChangesAsync();
                return Ok(MenuResponseDto.From(newMenu));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating menu item");
                return StatusCode(500, "Internal Server Error");
            }
        }

        /// <summary>
        /// Retrieves every menu, active or not, with the locations each is offered at.
        /// </summary>
        /// <response code="200">Returns every menu, active or not</response>
        /// <response code="500">If an internal error occurs while retrieving menus</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     GET /api/menu
        ///
        /// Returns all menus; is_active shows which are in use. For a location's active menus, 
        /// use GET /api/menu/location/{id}.
        /// </remarks>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<MenuResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<MenuResponseDto>>> GetAllMenu()
        {
            try
            {
                var menus = await _context.Menus.Include(m => m.MenuLocations).ToListAsync();

                return Ok(menus.Select(MenuResponseDto.From).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all Menus");
                return StatusCode(500, "Internal Server Error");
            }
        }

        /// <summary>
        /// Retrieves a specific menu by its ID.
        /// </summary>
        /// <param name="id">The unique identifier of the menu</param>
        /// <response code="200">Returns the menu with the specified ID</response>
        /// <response code="404">If the menu is not found</response>
        /// <response code="500">If an internal error occurs while retrieving the menu</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     GET /api/menu/123
        ///
        /// Returns the complete menu details for the specified ID.
        /// </remarks>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(MenuResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<MenuResponseDto>> GetMenuByID(int id)
        {
            try
            {
                var menu = await _context.Menus.Include(m => m.MenuLocations).FirstOrDefaultAsync(m => m.Menu_id == id);

                if (menu != null)
                {
                    return Ok(MenuResponseDto.From(menu));
                }

                return NotFound(new { message = $"Can't find menu ID: {id}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error finding menu with ID {id}");
                return StatusCode(500, "Internal Server Error");
            }
        }

        /// <summary>
        /// Retrieves all available items from a specific menu with their details, prices, categories, and tags.
        /// </summary>
        /// <param name="menu">The unique identifier of the menu</param>
        /// <param name="locationId">The optional location ID to filter menu items by location</param>
        /// <response code="200">Returns the list of available menu items with details, prices, categories, and tags</response>
        /// <response code="404">If the menu is not found</response>
        /// <response code="500">If an internal error occurs while retrieving menu items</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     GET /api/menu/123/item?location_id=4
        ///
        /// Returns only items with 'Available' status.
        /// Each item includes:
        /// - Item details (ID, name, description, image URL)
        /// - Price for this specific menu
        /// - Category and item limits per person / per order
        /// - Associated tags with names and color codes
        /// </remarks>
        [HttpGet("{menu}/item")]
        [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<object>>> GetItemsByMenu(int menu, [FromQuery(Name = "location_id")] int? locationId)
        {
            try
            {
                var currentMenu = await _context.Menus.FindAsync(menu);

                if (currentMenu == null)
                {
                    return NotFound(new { message = $"Can't find Menu with ID:{menu}" });
                }
                else
                {
                    var rows = await _context.MenuItemAssignments
                            .ForLocation(menu, locationId)
                            .Include(m => m.MenuItem).ThenInclude(me => me.Category)
                            .Include(m => m.MenuItem).ThenInclude(me => me.MenuItemTags).ThenInclude(me => me.Tag)
                            .ToListAsync();

                    // Resolve first, THEN filter, so an "Unavailable" override hides the item at that location only
                    var itemInfo = rows.Effective()
                            .Where(m => m.Status == MenuItemStatus.Available)
                            .ToList();

                    //Arrange details into a user friendly fashion
                    //Only takes tag name and color
                    var menuItems = itemInfo.Select(m => new
                    {
                        item_id = m.Item_Id,
                        name = m.MenuItem.Name,
                        description = m.MenuItem.Description,
                        category = m.MenuItem.Category.Category_name,
                        price = m.Price,
                        category_id = m.MenuItem.Category_id,
                        category_limits = new
                        {
                            adult = m.MenuItem.Category.adult_limit,
                            senior = m.MenuItem.Category.senior_limit,
                            child = m.MenuItem.Category.child_limit,
                            toddler = LimitRules.EffectToddler(m.MenuItem.Category.toddler_limit, m.MenuItem.Category.child_limit),
                            per_order = m.MenuItem.Category.per_order_limit,
                            per_order_per_guest = m.MenuItem.Category.per_order_per_guest,
                            over_limit_behavior = m.MenuItem.Category.over_limit_behavior,
                        },
                        item_limits = new
                        {
                            adult = m.Adult_Limit,
                            senior = m.Senior_limit,
                            child = m.Child_limit,
                            toddler = LimitRules.EffectToddler(m.Tot_Limit, m.Child_limit),
                            per_order = m.Per_Order_Limit,
                            over_limit_behavior = m.Over_Limit_Behavior,
                        },
                        is_add_on = m.Is_Add_On,
                        item_image_url = m.MenuItem.image_url,
                        tags = m.MenuItem.MenuItemTags.
                            Select(me => new
                            {
                                name = me.Tag.tag_name,
                                color = me.Tag.tag_color
                            }).ToList()
                    }).ToList();

                    return Ok(menuItems);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $" Can't get items for menu {menu}");
                return StatusCode(500, "Internal Server Error");
            }
        }

        /// <summary>
        /// Retrieves all active menus for a specific location.
        /// </summary>
        /// <param name="location">The unique identifier of the location</param>
        /// <response code="200">Returns the list of active menus for the specified location</response>
        /// <response code="404">If the location is not found</response>
        /// <response code="500">If an internal error occurs while retrieving menus</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     GET /api/menu/location/123
        ///
        /// Returns only active menus associated with the specified location.
        /// </remarks>
        [HttpGet("location/{location}")]
        [ProducesResponseType(typeof(IEnumerable<MenuResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]

        public async Task<ActionResult<IEnumerable<MenuResponseDto>>> GetMenuByLocation(int location)
        {
            try
            {
                var locationID = await _context.Locations.FindAsync(location);

                if (locationID == null)
                {

                    return NotFound(new { message = $"Can't find location with ID: {location}" });
                }

                var menus = await _context.Menus
                                .Include(m => m.MenuLocations)
                                .Where(m => m.Is_active && m.MenuLocations.Any(ml => ml.Location_Id == location))
                                .ToListAsync();

                return Ok(menus.Select(MenuResponseDto.From).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Can't get menus for location at ID: {location}");
                return StatusCode(500, "Internal Server Error");
            }
        }



        /// <summary>
        /// Updates an existing menu.
        /// </summary>
        /// <param name="menuId">The unique identifier of the menu to update</param>
        /// <param name="menuData">The updated menu data</param>
        /// <response code="200">Returns the updated menu</response>
        /// <response code="401">If the user is not authenticated</response>
        /// <response code="403">If the user is not authorized</response>
        /// <response code="404">If the menu is not found</response>
        /// <response code="409">If a menu with the same name already exists</response>
        /// <response code="500">If an internal error occurs while updating the menu</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     PUT /api/menu/123
        ///     {
        ///         "name": "Updated Dinner Menu",
        ///         "description": "Evening dining options",
        ///         "start_time": "17:00:00",
        ///         "end_time": "22:00:00",
        ///         "is_active": true
        ///     }
        ///
        /// This endpoint requires Admin role authorization.
        /// All fields in the request body are optional - only provided fields will be updated.
        /// Menu names must be unique.
        /// </remarks>
        [Authorize(Policy = "adminOnly")]
        [HttpPut("{menuId}")]
        [ProducesResponseType(typeof(MenuResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateMenu(
            int menuId,
            MenuUpdateDto menuData
        )
        {
            try
            {
                var menu = await _context.Menus.Include(m => m.MenuLocations).FirstOrDefaultAsync(m => m.Menu_id == menuId);
                if (menu == null)
                {
                    return NotFound("Menu Id was not Found");
                }
                if ((menuData.Name != null) && (menuData.Name != menu.Name))
                {
                    var existingMenu = await _context.Menus.FirstOrDefaultAsync(m => m.Name == menuData.Name && m.Menu_id != menuId);
                    if (existingMenu != null)
                    {
                        return Conflict("Menu with this name already Exists");
                    }
                }
                ;
                if (menuData.Name != null)
                {
                    menu.Name = menuData.Name;
                }
                if (menuData.Description != null)
                {
                    menu.Description = menuData.Description;
                }
                if (menuData.StartTime.HasValue)
                {
                    menu.Start_time = (TimeOnly)menuData.StartTime;
                }
                if (menuData.EndTime.HasValue)
                {
                    menu.End_time = (TimeOnly)menuData.EndTime;
                }
                if (menuData.IsActive.HasValue)
                {
                    menu.Is_active = (bool)menuData.IsActive;
                }
                _context.Update(menu);
                await _context.SaveChangesAsync();
                return Ok(MenuResponseDto.From(menu));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Can't update the menu: {menuData.Name}");
                return StatusCode(500, "Internal Server Error");
            }
        }


        /// <summary>
        /// Deletes a menu from the system.
        /// </summary>
        /// <param name="menuId">The unique identifier of the menu to delete</param>
        /// <response code="200">Returns a success message when the menu is deleted</response>
        /// <response code="401">If the user is not authenticated</response>
        /// <response code="403">If the user is not authorized</response>
        /// <response code="404">If the menu is not found</response>
        /// <response code="409">If the menu has past dining sessions or orders</response>
        /// <response code="500">If an internal error occurs while deleting the menu</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     DELETE /api/menu/123
        ///
        /// This endpoint requires Admin role authorization.
        /// Permanently removes a menu that has never been used.
        /// A menu with past sessions or orders can't be deleted; deactivate it instead.
        /// </remarks>
        [Authorize(Policy = "adminOnly")]
        [HttpDelete("{menuId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteMenu(
            int menuId
        )
        {
            try
            {
                var menuToDelete = await _context.Menus.FirstOrDefaultAsync(m => m.Menu_id == menuId);
                if (menuToDelete == null)
                {
                    return NotFound("Could Not Find the Menu To Delete");
                }
                var hasHistory = await _context.DiningSessions.AnyAsync(s => s.Menu_Id == menuId)
                    || await _context.OrderItems.AnyAsync(o => o.Menu_Id == menuId);
                if (hasHistory)
                {
                    return Conflict("This menu has past sessions or orders, so it can't be deleted. Deactivate it instead.");
                }
                _context.Remove(menuToDelete);
                await _context.SaveChangesAsync();
                return Ok("Menu Was deleted");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Can't update the menu Id: {menuId}");
                return StatusCode(500, "Internal Server Error");
            }
        }
    }
}