using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.DTO.MenuItems;
using Microsoft.AspNetCore.Authorization;
using back_end.DTO.MenuDTO;
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


        [Authorize(Policy = "staffOnly")]
        [HttpPost("createmenu")]
        [ProducesResponseType(typeof(MenuItemResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Create_Menu(
            MenuCreateDTO item_data
        )
        {
            try
            {
                if (item_data is null)
                {
                    return BadRequest("Item Data not Sent");
                }
                var isExist = await _context.Menus.FirstOrDefaultAsync(m => m.Name == item_data.Name);
                if (isExist != null)
                {
                    return Conflict("Menu Name and item Exist");
                }
                var newMenu = new Menu
                {
                    Name = item_data.Name,
                    Description = item_data.Description,
                    Start_time = item_data.Start_Time ?? new TimeOnly(0, 0, 1),
                    End_time = item_data.End_Time ?? new TimeOnly(23, 59, 59),
                    Is_active = item_data.Is_Active
                };
                _context.Add(newMenu);
                await _context.SaveChangesAsync();
                return Ok(newMenu);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating menu item");
                return StatusCode(500, "Internal Server Error");
            }
        }

        /// <summary>
        /// Creates a new menu item with optional tags.
        /// </summary>
        /// <param name="item_data">The menu item data including name, description, category, image URL, and tag IDs</param>
        /// <returns>
        /// An <see cref="IActionResult"/> containing the created <see cref="MenuItemResponseDTO"/> object.
        /// Returns HTTP 200 (OK) with the created menu item on success.
        /// Returns HTTP 409 (Conflict) if a menu item with the same name already exists.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during creation.
        /// </returns>
        /// <response code="200">Returns the newly created menu item</response>
        /// <response code="409">If a menu item with the same name already exists</response>
        /// <response code="500">If an internal error occurs while creating the menu item</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     POST /api/menu
        ///     {
        ///         "name": "Grilled Salmon",
        ///         "description": "Fresh Atlantic salmon with lemon butter sauce",
        ///         "category_Id": 2,
        ///         "item_Image_Url": "https://example.com/images/salmon.jpg",
        ///         "tag_Ids": [1, 3, 5]
        ///     }
        ///
        /// This endpoint requires Admin or Staff role authorization.
        /// The menu item is automatically created with 'Available' status.
        /// Tags are optional and will be associated with the item if provided.
        /// </remarks>
        [Authorize(Policy = "staffOnly")]
        [HttpPost]
        [ProducesResponseType(typeof(MenuItemResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Create_Menu_Item(
            MenuItemCreateDTO item_data
        )
        {
            try
            {
                // Validate required fields
                if (string.IsNullOrWhiteSpace(item_data.Name))
                    return BadRequest("Name is required.");

                if (item_data.Category_Id <= 0)
                    return BadRequest("Category_Id is required.");

                var existing = await _context.MenuItems
                    .Where(mi => mi.Name == item_data.Name)
                    .FirstOrDefaultAsync();

                if (existing != null)
                    return Conflict($"Menu Item with Name Exists: {item_data.Name}");

                // Use fallbacks for nullable strings to satisfy non-nullable entity properties
                var new_item = new Menu_Item
                {
                    Name = item_data.Name!,
                    Description = item_data.Description ?? string.Empty,
                    Category_id = item_data.Category_Id,
                    image_url = item_data.Item_Image_Url ?? string.Empty,
                    Status = MenuItemStatus.Available,
                    // Ensure collection is initialized before adding tags (if your entity doesn’t do it)
                    MenuItemTags = new List<MenuItemTag>()
                };

                // Handle possible null Tag_Ids
                if (item_data.Tag_Ids != null && item_data.Tag_Ids.Count > 0)
                {
                    var tags = await _context.Tags
                        .Where(t => item_data.Tag_Ids.Contains(t.tag_id))
                        .ToListAsync();

                    foreach (var tag in tags)
                    {
                        new_item.MenuItemTags.Add(new MenuItemTag { Tag = tag });
                    }
                }

                _context.Add(new_item);
                await _context.SaveChangesAsync();

                return Ok(new MenuItemResponseDTO
                {
                    Item_Id = new_item.item_id,
                    Name = new_item.Name,
                    Description = new_item.Description,
                    Category_Id = new_item.Category_id,
                    Item_Image_Url = new_item.image_url,
                    Status = new_item.Status,
                    Tags = new_item.MenuItemTags.Select(t => new FullTagResponseDTO
                    {
                        Tag_Id = t.Tag.tag_id,
                        Name = t.Tag.tag_name,
                        Color_Code = t.Tag.tag_color
                    }).ToList(),

                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating menu item");
                return StatusCode(500, "Internal Server Error");
            }
        }

        /// <summary>
        /// Retrieves all active menus from the database.
        /// </summary>
        /// <returns>
        /// An <see cref="ActionResult"/> containing a collection of <see cref="Menu"/> objects.
        /// Returns HTTP 200 (OK) with the list of active menus on success.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during retrieval.
        /// </returns>
        /// <response code="200">Returns the list of all active menus</response>
        /// <response code="500">If an internal error occurs while retrieving menus</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     GET /api/menu
        ///
        /// Returns only menus where Is_active is true.
        /// </remarks>
        //GET: api/menu
        //Get all Menus
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<Menu>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<Menu>>> GetAllMenu()
        {
            try
            {
                var menu = await _context.Menus.Include(m => m.MenuLocations).ToListAsync();

                return Ok(menu);
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
        /// <returns>
        /// An <see cref="ActionResult"/> containing the <see cref="Menu"/> object.
        /// Returns HTTP 200 (OK) with the menu details on success.
        /// Returns HTTP 404 (Not Found) if the menu doesn't exist.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during retrieval.
        /// </returns>
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
        //GET: api/menu/{id}
        //Get a specific menu by its ID number
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(Menu), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<Menu>> GetMenuByID(int id)
        {
            try
            {
                var menu = await _context.Menus.FindAsync(id);

                //See if menu exists
                if (menu != null)
                {
                    return Ok(menu);
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
        /// Retrieves all active menus available at a specific time.
        /// </summary>
        /// <param name="time">Optional time in HH:mm:ss format. If not provided, uses current time.</param>
        /// <returns>
        /// An <see cref="ActionResult"/> containing a collection of <see cref="Menu"/> objects.
        /// Returns HTTP 200 (OK) with the list of active menus on success.
        /// Returns HTTP 400 (Bad Request) if the time format is invalid.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during retrieval.
        /// </returns>
        /// <response code="200">Returns the list of active menus available at the specified time</response>
        /// <response code="400">If the time format is invalid</response>
        /// <response code="500">If an internal error occurs while retrieving menus</response>
        /// <remarks>
        /// Sample requests:
        ///
        ///     GET /api/menu/time
        ///     (Uses current time)
        ///     
        ///     GET /api/menu/time?time=18:30:00
        ///     (Uses specified time)
        ///
        /// Returns only active menus where the specified time falls within the menu's start and end times.
        /// Time format should be HH:mm:ss (e.g., "18:30:00" for 6:30 PM).
        /// </remarks>
        [HttpGet("time")]
        [ProducesResponseType(typeof(IEnumerable<Menu>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<Menu>>> GetMenuByTime([FromQuery] string? time)
        {
            TimeOnly currentTime;

            try
            {
                //Check if time was given
                if (string.IsNullOrEmpty(time))
                {
                    currentTime = TimeOnly.FromDateTime(DateTime.UtcNow);
                }
                //Check if time given is proper
                else if (!TimeOnly.TryParse(time, out currentTime))
                {
                    return BadRequest(new { message = "Improper time given." });
                }
                //Get the correct menu for time slot
                //*note to self* Test this part more
                var currentMenu = await _context.Menus.Where(m => m.Is_active &&
                                            m.Start_time <= currentTime &&
                                            m.End_time >= currentTime)
                                        .ToListAsync();

                return Ok(currentMenu);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting the current Menu");
                return StatusCode(500, "Internal Server Error");
            }
        }

        /// <summary>
        /// Retrieves all available items from a specific menu with their details, prices, categories, and tags.
        /// </summary>
        /// <param name="menu">The unique identifier of the menu</param>
        /// <returns>
        /// An <see cref="ActionResult"/> containing a collection of menu item objects with pricing and tag information.
        /// Returns HTTP 200 (OK) with the list of available menu items on success.
        /// Returns HTTP 404 (Not Found) if the menu doesn't exist.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during retrieval.
        /// </returns>
        /// <response code="200">Returns the list of available menu items with details, prices, categories, and tags</response>
        /// <response code="404">If the menu is not found</response>
        /// <response code="500">If an internal error occurs while retrieving menu items</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     GET /api/menu/123/item
        ///
        /// Returns only items with 'Available' status.
        /// Each item includes:
        /// - Item details (ID, name, description, image URL)
        /// - Price for this specific menu
        /// - Category information
        /// - Associated tags with names and color codes
        /// </remarks>
        //GET: api/menu/{menu}/items
        //Get items from a Menu
        //Display: price, category and diet tags
        [HttpGet("{menu}/item")]
        [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<object>>> GetItemsByMenu(int menu)
        {
            try
            {
                var currentMenu = await _context.Menus.FindAsync(menu);

                //Check if menu exists
                if (currentMenu == null)
                {
                    return NotFound(new { message = $"Can't find Menu with ID:{menu}" });
                }
                else
                {
                    //Get all details, category and tags for all menu items
                    var itemInfo = await _context.MenuItemAssignments
                            .Where(m => m.Menu_Id == menu && m.Status == MenuItemStatus.Available)
                            .Include(m => m.MenuItem)
                                .ThenInclude(me => me.Category)
                            .Include(m => m.MenuItem)
                                .ThenInclude(me => me.MenuItemTags)
                                .ThenInclude(me => me.Tag)
                            .ToListAsync();

                    //Arrange details into a user friendly fashion
                    //Only takes tag name and color
                    var menuItems = itemInfo.Select(m => new
                    {
                        itemId = m.Item_Id,
                        name = m.MenuItem.Name,
                        description = m.MenuItem.Description,
                        category = m.MenuItem.Category.Category_name,
                        price = m.Price,
                        categoryID = m.MenuItem.Category_id,
                        categoryLimits = new
                        {
                            adult = m.MenuItem.Category.adult_limit,
                            senior = m.MenuItem.Category.senior_limit,
                            child = m.MenuItem.Category.child_limit,
                            total = m.MenuItem.Category.total_limit,
                        },
                        isAddOn = m.Is_Add_On,
                        imageURL = m.MenuItem.image_url,
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
        /// <returns>
        /// An <see cref="ActionResult"/> containing a collection of <see cref="Menu"/> objects.
        /// Returns HTTP 200 (OK) with the list of active menus on success.
        /// Returns HTTP 404 (Not Found) if the location doesn't exist.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during retrieval.
        /// </returns>
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
        [ProducesResponseType(typeof(IEnumerable<Menu>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]

        public async Task<ActionResult<IEnumerable<Menu>>> GetMenuByLocation(int location)
        {
            try
            {
                var locationID = await _context.Locations.FindAsync(location);

                //Check if location exists
                if (locationID == null)
                {

                    return NotFound(new { message = $"Can't find location with ID: {location}" });
                }

                //Get all the menus at the inputted location
                var menus = await _context.MenuLocations
                                .Where(m => m.Location_Id == location)
                                .Include(m => m.Menu)
                                .Where(m => m.Menu.Is_active)
                                .Select(m => m.Menu)
                                .ToListAsync();

                return Ok(menus);
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
        /// <param name="menu_id">The unique identifier of the menu to update</param>
        /// <param name="menu_data">The updated menu data</param>
        /// <returns>
        /// An <see cref="IActionResult"/> containing the updated <see cref="MenuResponseDTO"/> object.
        /// Returns HTTP 200 (OK) with the updated menu on success.
        /// Returns HTTP 404 (Not Found) if the menu doesn't exist.
        /// Returns HTTP 409 (Conflict) if a menu with the new name already exists.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during the update.
        /// </returns>
        /// <response code="200">Returns the updated menu</response>
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
        ///         "start_Time": "17:00:00",
        ///         "end_Time": "22:00:00",
        ///         "is_Active": true
        ///     }
        ///
        /// This endpoint requires Admin role authorization.
        /// All fields in the request body are optional - only provided fields will be updated.
        /// Menu names must be unique.
        /// </remarks>
        [Authorize(Policy = "adminOnly")]
        [HttpPut("{menu_id}")]
        [ProducesResponseType(typeof(MenuResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Update_Menu(
            int menu_id,
            MenuUpdateDTO menu_data
        )
        {
            try
            {
                var menu = _context.Menus.FirstOrDefault(m => m.Menu_id == menu_id);
                if (menu == null)
                {
                    return NotFound("Menu Id was not Found");
                }
                if ((menu_data.Name != null) && (menu_data.Name != menu.Name))
                {
                    var existingMenu = await _context.Menus.FirstOrDefaultAsync(m => m.Name == menu_data.Name && m.Menu_id != menu_id);
                    if (existingMenu != null)
                    {
                        return Conflict("Menu with this name already Exists");
                    }
                }
                ;
                if (menu_data.Name != null)
                {
                    menu.Name = menu_data.Name;
                }
                if (menu_data.Description != null)
                {
                    menu.Description = menu_data.Description;
                }
                if (menu_data.Start_Time.HasValue)
                {
                    menu.Start_time = (TimeOnly)menu_data.Start_Time;
                }
                if (menu_data.End_Time.HasValue)
                {
                    menu.End_time = (TimeOnly)menu_data.End_Time;
                }
                if (menu_data.Is_Active.HasValue)
                {
                    menu.Is_active = (bool)menu_data.Is_Active;
                }
                _context.Update(menu);
                await _context.SaveChangesAsync();
                return Ok(new MenuResponseDTO
                {
                    Menu_Id = menu.Menu_id,
                    Name = menu.Name,
                    Description = menu.Description,
                    Start_Time = menu.Start_time,
                    End_Time = menu.End_time,
                    Is_Active = menu.Is_active
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Can't update the menu: {menu_data.Name}");
                return StatusCode(500, "Internal Server Error");
            }
        }


        /// <summary>
        /// Deletes a menu from the system.
        /// </summary>
        /// <param name="menu_id">The unique identifier of the menu to delete</param>
        /// <returns>
        /// An <see cref="IActionResult"/> indicating the result of the operation.
        /// Returns HTTP 200 (OK) with a success message when the menu is deleted.
        /// Returns HTTP 404 (Not Found) if the menu doesn't exist.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during deletion.
        /// </returns>
        /// <response code="200">Returns a success message when the menu is deleted</response>
        /// <response code="404">If the menu is not found</response>
        /// <response code="500">If an internal error occurs while deleting the menu</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     DELETE /api/menu/123
        ///
        /// This endpoint requires Admin role authorization.
        /// Permanently removes the menu from the database.
        /// </remarks>
        [Authorize(Policy = "adminOnly")]
        [HttpDelete("{menu_id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Delete_Menu(
            int menu_id
        )
        {
            try
            {
                var menuToDelete = await _context.Menus.FirstOrDefaultAsync(m => m.Menu_id == menu_id);
                if (menuToDelete == null)
                {
                    return NotFound("Could Not Find the Menu To Delete");
                }
                _context.Remove(menuToDelete);
                await _context.SaveChangesAsync();
                return Ok("Menu Was deleted");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Can't update the menu Id: {menu_id}");
                return StatusCode(500, "Internal Server Error");
            }
        }



    }
}