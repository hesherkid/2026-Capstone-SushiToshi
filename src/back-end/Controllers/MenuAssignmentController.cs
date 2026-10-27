using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.DTO.MenuItemAssignmentDTO;
using Microsoft.AspNetCore.Authorization;
using back_end.Helpers;
using back_end.domain.enums;
using back_end.Services;

namespace back_end.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MenuAssignmentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MenuAssignmentController> _logger;
        private readonly ILocationAccessService _locationAccess;

        public MenuAssignmentController(ApplicationDbContext context, ILogger<MenuAssignmentController> logger, ILocationAccessService locationAccess)
        {
            _context = context;
            _logger = logger;
            _locationAccess = locationAccess;
        }

        // Staff may change only their own location's overrides. The default row (no location)
        // applies at every location, so only admins may change it
        private async Task<IActionResult?> DenyOtherLocationAsync(int? locationId)
        {
            if (locationId is null)
            {
                return _locationAccess.IsLimitedToOneLocation(User)
                    ? StatusCode(StatusCodes.Status403Forbidden, "Only admins can change the menu for every location.")
                    : null;
            }

            return await _locationAccess.CanAccessLocationAsync(User, locationId.Value)
                ? null
                : LocationResults.OtherLocation();
        }



        /// <summary>
        /// Creates a new menu item assignment, associating an item with a menu at a specific price.
        /// </summary>
        /// <param name="assignmentData">The assignment data including menu ID, item ID, price, limits, and status</param>
        /// <returns>
        /// An <see cref="IActionResult"/> containing the created <see cref="MenuItemAssignment"/> object.
        /// Returns HTTP 200 (OK) with the created assignment on success.
        /// Returns HTTP 400 (Bad Request) if validation fails.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during creation.
        /// </returns>
        /// <response code="200">Returns the newly created menu item assignment</response>
        /// <response code="400">If the request data is invalid</response>
        /// <response code="409">If there is a conflict, such as a duplicate assignment</response>
        /// <response code="500">If an internal error occurs while creating the assignment</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     POST /api/menuassignment
        ///     {
        ///         "menu_Id": 123,
        ///         "item_Id": 456,
        ///         "price": 15.99,
        ///         "over_Limit_Behavior": "Charge",
        ///         "adult_Limit": 100,
        ///         "senior_Limit": 50,
        ///         "child_Limit": 30,
        ///         "tot_Limit": 20,
        ///         "per_Order_Limit": 2,
        ///         "is_Add_on": true,
        ///         "status": "Available"
        ///     }
        ///
        /// Creates a new association between a menu and a menu item.
        /// adult_Limit, child_Limit, and senior_Limit are required (0 = unlimited).
        /// tot_Limit is optional: 0 or omitted = same as child_Limit.
        /// Rules: child and senior ≤ adult, toddler ≤ child.
        /// Over_Limit_Behavior defaults to "Block".
        /// over_Limit_Behavior "Charge" requires price greater than 0.
        /// Add-on items must have a price greater than 0.
        /// per_Order_Limit is the max units of this item per order for the whole table (0 = no limit cap, always blocks).
        /// Item views are recorded separately (POST api/menu-item-views).
        /// </remarks>
        [Authorize(Policy = "adminOnly")]
        [HttpPost]
        [ProducesResponseType(typeof(MenuItemAssignment), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Create_Menu_Item_Assignment(
                   MenuAssignmentCreate assignmentData
               )
        {
            try
            {
                var limitError = LimitRules.Validate(assignmentData.Adult_Limit ?? 0, assignmentData.Child_Limit ?? 0, assignmentData.Senior_Limit ?? 0, assignmentData.Tot_Limit ?? 0, assignmentData.Per_Order_Limit ?? 0);
                if (limitError != null) return BadRequest(limitError);

                var behavior = assignmentData.Over_Limit_Behavior ?? OverLimitBehavior.Block;
                if (behavior == OverLimitBehavior.Charge && assignmentData.Price <= 0)
                    return BadRequest("Items that charge over the limit need a full price greater than 0");

                if (assignmentData.Is_Add_on && assignmentData.Price <= 0)
                {
                    return BadRequest("Add-on items must have a price greater than 0");
                }

                if (assignmentData.Location_Id != null &&
                    !await _context.MenuItemAssignments.AnyAsync(a => a.Menu_Id == assignmentData.Menu_Id && a.Item_Id == assignmentData.Item_Id
                    && a.Location_Id == null))
                {
                    return BadRequest("Add the item to the menu (default price) before adding a location price");
                }

                var assignment = new MenuItemAssignment
                {
                    Menu_Id = assignmentData.Menu_Id,
                    Item_Id = assignmentData.Item_Id,
                    Price = assignmentData.Price,
                    Over_Limit_Behavior = behavior,
                    Adult_Limit = assignmentData.Adult_Limit!.Value,
                    Child_limit = assignmentData.Child_Limit!.Value,
                    Senior_limit = assignmentData.Senior_Limit!.Value,
                    Tot_Limit = assignmentData.Tot_Limit ?? 0,
                    Per_Order_Limit = assignmentData.Per_Order_Limit ?? 0,
                    Status = assignmentData.Status,
                    Is_Add_On = assignmentData.Is_Add_on,
                    Location_Id = assignmentData.Location_Id,
                };

                await _context.MenuItemAssignments.AddAsync(assignment);
                await _context.SaveChangesAsync();
                return Ok(assignment);

            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UX_assignment_menu_item_location") == true)
            {
                return Conflict("A price for this item already exists for that location");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating menu assignment for Menu_Id: {Menu_Id}, Item_Id: {Item_Id}", assignmentData.Menu_Id, assignmentData.Item_Id);
                return StatusCode(500, new { message = "An error occurred while updating the menu assignment." });
            }
        }



        /// <summary>
        /// Updates a menu item assignment including price, limits, and over limit behavior.
        /// </summary>
        /// <param name="menu_id" > The unique identifier of the menu</param>
        /// <param name="item_id">The unique identifier of the menu item</param>
        /// <param name="updateData">The updated assignment data</param>
        /// <param name="locationId">Optional location override; omit for the default (all locations) assignment</param>
        /// <returns>
        /// An <see cref="IActionResult"/> containing the updated <see cref="MenuItemAssignment"/> object.
        /// Returns HTTP 200 (OK) with the updated assignment on success.
        /// Returns HTTP 400 (Bad Request) if the limit validation fails.
        /// Returns HTTP 404 (Not Found) if the menu item assignment doesn't exist.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during the update.
        /// </returns>
        /// <response code="200">Returns the updated menu item assignment</response>
        /// <response code="400">If limit validation fails</response>
        /// <response code="404">If the menu item assignment is not found</response>
        /// <response code="500">If an internal error occurs while updating the assignment</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     PUT /api/menuassignment/123/456
        ///     {
        ///         "price": 15.99,
        ///         "adult_Limit": 1,
        ///         "child_Limit": 1,
        ///         "senior_Limit": 1,
        ///         "tot_Limit": 0,
        ///         "per_Order_Limit": 2,
        ///         "over_Limit_Behavior": "Charge",
        ///         "is_Add_on": false,
        ///         "status": "Available"
        ///     }
        ///
        /// This endpoint requires Admin or Staff role authorization.
        /// All fields are optional - fields not sent keep their current values.
        /// A limit of 0 means unlimited, except tot_Limit: 0 means "same as child_Limit".
        /// Rules: child and senior ≤ adult, toddler ≤ child.
        /// over_Limit_Behavior "Charge" requires price greater than 0.
        /// per_Order_Limit is the max units of this item per order for the whole table (0 = no limit cap, always blocks).
        /// Add-on items require price greater than 0.
        /// </remarks>
        [Authorize(Policy = "staffOnly")]
        [HttpPut("{menu_id}/{item_id}")]
        [ProducesResponseType(typeof(MenuItemAssignment), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Update_Menu_Item_Assignment(
            int menu_id,
            int item_id,
            MenuAssignmentUpdateDTO updateData,
            [FromQuery] int? locationId
        )
        {
            try
            {
                var denied = await DenyOtherLocationAsync(locationId);
                if (denied != null) return denied;

                var assignment = await _context.MenuItemAssignments
                    .Where(mia => mia.Menu_Id == menu_id && mia.Item_Id == item_id && mia.Location_Id == locationId)
                    .FirstOrDefaultAsync();

                if (assignment == null)
                {
                    return NotFound("Menu Assignment was not found.");
                }

                var finalAdultLimit = updateData.Adult_Limit ?? assignment.Adult_Limit;
                var finalChildLimit = updateData.Child_Limit ?? assignment.Child_limit;
                var finalSeniorLimit = updateData.Senior_Limit ?? assignment.Senior_limit;
                var finalTotLimit = updateData.Tot_Limit ?? assignment.Tot_Limit;
                var finalPerOrder = updateData.Per_Order_Limit ?? assignment.Per_Order_Limit;
                var finalBehavior = updateData.Over_Limit_Behavior ?? assignment.Over_Limit_Behavior;
                var finalPrice = updateData.Price ?? assignment.Price;
                var finalIsAddOn = updateData.Is_Add_on ?? assignment.Is_Add_On;

                var limitError = LimitRules.Validate(finalAdultLimit, finalChildLimit, finalSeniorLimit, finalTotLimit, finalPerOrder);
                if (limitError != null) return BadRequest(limitError);

                if (finalBehavior == OverLimitBehavior.Charge && finalPrice <= 0)
                    return BadRequest("Items that charge over the limit need a full price greater than 0");

                if (finalIsAddOn && finalPrice <= 0)
                    return BadRequest("Add-on items must have a price greater than 0");

                assignment.Adult_Limit = finalAdultLimit;
                assignment.Child_limit = finalChildLimit;
                assignment.Senior_limit = finalSeniorLimit;
                assignment.Tot_Limit = finalTotLimit;
                assignment.Per_Order_Limit = finalPerOrder;
                assignment.Over_Limit_Behavior = finalBehavior;
                assignment.Price = finalPrice;
                assignment.Is_Add_On = finalIsAddOn;
                assignment.Status = updateData.Status ?? assignment.Status;

                await _context.SaveChangesAsync();
                return Ok(assignment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating menu assignment {MenuId}/{ItemId}", menu_id, item_id);
                return StatusCode(500, new { message = "An error occurred while updating the menu assignment." });
            }
        }

        /// <summary>
        /// Removes a menu item assignment from a specific menu.
        /// </summary>
        /// <param name="menu_id">The unique identifier of the menu</param>
        /// <param name="Item_id">The unique identifier of the menu item</param>
        /// <returns>
        /// An <see cref="IActionResult"/> indicating the result of the operation.
        /// Returns HTTP 200 (OK) with a success message when the assignment is deleted.
        /// Returns HTTP 404 (Not Found) if the menu item assignment doesn't exist.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during deletion.
        /// </returns>
        /// <response code="200">Returns a success message when the assignment is deleted</response>
        /// <response code="404">If the menu item assignment is not found</response>
        /// <response code="500">If an internal error occurs while deleting the assignment</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     DELETE /api/menuassignment/123/456
        ///
        /// This endpoint requires Admin or Staff role authorization.
        /// Removes the association between the specified menu item and menu.
        /// </remarks>
        [Authorize(Policy = "staffOnly")]
        [HttpDelete("{menu_id}/{Item_id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Delete_Menu_Item_Assignment(
            int menu_id,
            int Item_id,
            [FromQuery] int? locationId
        )
        {
            try
            {
                var denied = await DenyOtherLocationAsync(locationId);
                if (denied != null) return denied;

                var rows = await _context.MenuItemAssignments
                    .Where(mia => mia.Menu_Id == menu_id && mia.Item_Id == Item_id && (locationId == null || mia.Location_Id == locationId))   // default → remove overrides too
                    .ToListAsync();
                if (rows.Count == 0)
                {
                    return NotFound("Menu Assignment for deletion was not found");
                }
                _context.RemoveRange(rows);
                await _context.SaveChangesAsync();
                return Ok("Menu Assignment was deleted Successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting menu assignment {MenuId}/{ItemId}", menu_id, Item_id);
                return StatusCode(500, new { message = "An error occurred while updating the menu assignment." });
            }
        }



        /// <summary>
        /// Creates a new menu by copying all item assignments from an existing menu.
        /// </summary>
        /// <param name="source_menu_id">The unique identifier of the menu to copy from</param>
        /// <param name="new_menu_name">The name for the new menu</param>
        /// <returns>
        /// An <see cref="IActionResult"/> summary of the new menu.
        /// Returns HTTP 200 (OK) with the list of copied assignments on success.
        /// Returns HTTP 409 (Conflict) if a menu with the new name already exists.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during the operation.
        /// </returns>
        /// <response code="200">Returns menu_id, name, assignments_copied</response>
        /// <response code="409">If a menu with the specified name already exists</response>
        /// <response code="500">If an internal error occurs while copying the menu</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     POST /api/menu/copy_menu/123
        ///     {
        ///         "new_menu_name": "Winter Menu 2025"
        ///     }
        ///
        /// Creates a new menu with the specified name and copies all item assignments
        /// (including prices, limits, full price, and over-limit behavior) from the source menu.
        /// </remarks>
        [Authorize(Policy = "adminOnly")]   // copies every location's overrides
        [HttpPost("copy_menu/{source_menu_id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Copy_Menu_Assignments(
            int source_menu_id,
            string new_menu_name
        )
        {
            try
            {
                var existingMenu = await _context.Menus.Where(m => m.Name == new_menu_name).FirstOrDefaultAsync();
                if (existingMenu != null)
                {
                    return Conflict("Menu Name Already Exist");
                }
                var new_menu = new Menu
                {
                    Name = new_menu_name,
                };
                _context.Add(new_menu);
                await _context.SaveChangesAsync();
                var sourceAssignments = await _context.MenuItemAssignments.Where(mia => mia.Menu_Id == source_menu_id).ToListAsync();

                var newAssignment = new List<MenuItemAssignment>();
                foreach (var element in sourceAssignments)
                {
                    newAssignment.Add(new MenuItemAssignment
                    {
                        Menu_Id = new_menu.Menu_id,
                        Item_Id = element.Item_Id,
                        Price = element.Price,
                        Over_Limit_Behavior = element.Over_Limit_Behavior,
                        Adult_Limit = element.Adult_Limit,
                        Child_limit = element.Child_limit,
                        Senior_limit = element.Senior_limit,
                        Tot_Limit = element.Tot_Limit,
                        Per_Order_Limit = element.Per_Order_Limit,
                        Is_Add_On = element.Is_Add_On,
                        Status = element.Status,
                        Location_Id = element.Location_Id,
                    });
                }
                _context.AddRange(newAssignment);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    menu_id = new_menu.Menu_id,
                    name = new_menu.Name,
                    assignments_copied = newAssignment.Count
                });

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error copying menu assignment from source menu {SourceMenuId} to new menu {NewMenuName}", source_menu_id, new_menu_name);
                return StatusCode(500, new { message = "An error occurred while updating the menu assignment." });
            }
        }
    }

}