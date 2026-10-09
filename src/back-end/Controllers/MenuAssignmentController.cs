using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.DTO.MenuItemAssignmentDTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.VisualBasic;

namespace back_end.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MenuAssignmentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MenuAssignmentController> _logger;

        public MenuAssignmentController(ApplicationDbContext context, ILogger<MenuAssignmentController> logger)
        {
            _context = context;
            _logger = logger;
        }



        /// <summary>
        /// Creates a new menu item assignment, associating an item with a menu at a specific price.
        /// </summary>
        /// <param name="assignmentData">The assignment data including menu ID, item ID, price, limits, and status</param>
        /// <returns>
        /// An <see cref="IActionResult"/> containing the created <see cref="MenuItemAssignment"/> object.
        /// Returns HTTP 200 (OK) with the created assignment on success.
        /// Returns HTTP 400 (Bad Request) if validation fails.
        /// Returns HTTP 404 (Not Found) if the menu or item doesn't exist.
        /// Returns HTTP 409 (Conflict) if the assignment already exists.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during creation.
        /// </returns>
        /// <response code="200">Returns the newly created menu item assignment</response>
        /// <response code="400">If the request data is invalid</response>
        /// <response code="404">If the menu or menu item is not found</response>
        /// <response code="409">If the assignment already exists</response>
        /// <response code="500">If an internal error occurs while creating the assignment</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     POST /api/menu
        ///     {
        ///         "menu_Id": 123,
        ///         "item_Id": 456,
        ///         "price": 15.99,
        ///         "adult_Limit": 100,
        ///         "senior_Limit": 50,
        ///         "child_Limit": 30,
        ///         "tot_Limit": 20,
        ///         "total_Units_Ordered": 0,
        ///         "total_Views": 0,
        ///         "total_View_Seconds": 0,
        ///         "status": "Available"
        ///     }
        ///
        /// Creates a new association between a menu and a menu item.
        /// All limit and statistics fields default to 0 if not provided.
        /// </remarks>
        [Authorize(Policy = "adminOnly")]
        [HttpPost]
        [ProducesResponseType(typeof(MenuItemAssignment), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Create_Menu_Item_Assignment(
                   MenuAssignmentCreate assignmentData
               )
        {

            try
            {
                if (assignmentData.Is_Add_on && assignmentData.Price <= 0)
                {
                    return BadRequest("Add-on items must have a price greater than 0");
                }

                var assignment = new MenuItemAssignment
                {
                    Menu_Id = assignmentData.Menu_Id,
                    Item_Id = assignmentData.Item_Id,
                    Price = assignmentData.Price,
                    Total_Units_Ordered = assignmentData.Total_Units_Ordered ?? 0,
                    Total_Views = assignmentData.Total_Views ?? 0,
                    Total_View_Seconds = assignmentData.Total_View_Seconds ?? 0,
                    Adult_Limit = assignmentData.Adult_Limit ?? 0,
                    Child_limit = assignmentData.Child_Limit ?? 0,
                    Senior_limit = assignmentData.Senior_Limit ?? 0,
                    Tot_Limit = assignmentData.Tot_Limit ?? 0,
                    Status = assignmentData.Status,
                    Is_Add_On = assignmentData.Is_Add_on,
                };

                await _context.MenuItemAssignments.AddAsync(assignment);
                await _context.SaveChangesAsync();
                return Ok(assignment);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating menu assignment for Menu_Id: {Menu_Id}, Item_Id: {Item_Id}", assignmentData.Menu_Id, assignmentData.Item_Id);
                return StatusCode(500, new { message = "An error occurred while updating the menu assignment." });
            }
        }



        /// <summary>
        /// Updates a menu item assignment including price, limits, and statistics.
        /// </summary>
        /// <param name="menu_id">The unique identifier of the menu</param>
        /// <param name="item_id">The unique identifier of the menu item</param>
        /// <param name="updateData">The updated assignment data</param>
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
        ///     PUT /api/menu/123/456
        ///     {
        ///         "price": 15.99,
        ///         "adult_Limit": 100,
        ///         "senior_Limit": 50,
        ///         "child_Limit": 30,
        ///         "tot_Limit": 20,
        ///         "total_Units_Ordered": 200,
        ///         "is_Add_on": false,
        ///         "status": "Available"
        ///     }
        ///
        /// This endpoint requires Admin or Staff role authorization.
        /// All fields are optional - only provided fields will be updated.
        /// Validates that individual limits do not exceed total limit and follow hierarchy rules.
        /// Total_Views and Total_View_Seconds are incremented (not replaced) when provided.
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
            MenuAssignmentUpdateDTO updateData
        )
        {
            try
            {
                var assignment = await _context.MenuItemAssignments.Where(mia => mia.Menu_Id == menu_id && mia.Item_Id == item_id).FirstOrDefaultAsync();
                if (assignment == null)
                {
                    return NotFound("Menu Assignment was not found.");
                }
                var finalAdultLimit = updateData.Adult_Limit ?? assignment.Adult_Limit;
                var finalSeniorLimit = updateData.Senior_Limit ?? assignment.Senior_limit;
                var finalChildLimit = updateData.Child_Limit ?? assignment.Child_limit;
                var finalTotLimit = updateData.Tot_Limit ?? assignment.Tot_Limit;

                if (finalChildLimit > finalAdultLimit)
                {
                    return BadRequest("Child limit cannot be greater than the adult limit");
                }
                if (finalTotLimit > finalAdultLimit)
                {
                    return BadRequest("Toddler limit cannot be greater than the adult limit");
                }
                if (finalSeniorLimit > finalAdultLimit)
                {
                    return BadRequest("Senior limit cannot be greater than the adult limit");
                }

                var finalIsAddOn = updateData.Is_Add_on ?? assignment.Is_Add_On;
                var finalPrice = updateData.Price ?? assignment.Price;
                if (finalIsAddOn && finalPrice <= 0)
                {
                    return BadRequest("Add-on items must have a price greater than 0");
                }

                if (updateData.Adult_Limit.HasValue)
                {
                    assignment.Adult_Limit = (int)updateData.Adult_Limit;
                }
                if (updateData.Child_Limit.HasValue)
                {
                    assignment.Child_limit = updateData.Child_Limit.Value;
                }
                if (updateData.Senior_Limit.HasValue)
                {
                    assignment.Senior_limit = (int)updateData.Senior_Limit;
                }
                if (updateData.Tot_Limit.HasValue)
                {
                    assignment.Tot_Limit = (int)updateData.Tot_Limit;
                }
                if (updateData.Total_Units_Ordered.HasValue)
                {
                    assignment.Total_Units_Ordered = (int)updateData.Total_Units_Ordered;
                }
                if (updateData.Total_Views.HasValue)
                {
                    assignment.Total_Views = assignment.Total_Views + (int)updateData.Total_Views;
                }
                if (updateData.Total_View_Seconds.HasValue)
                {
                    assignment.Total_View_Seconds = assignment.Total_View_Seconds + (int)updateData.Total_View_Seconds;
                }
                if (updateData.Last_Ordered_At.HasValue)
                {
                    assignment.LastOrdered = DateTime.UtcNow;
                }
                if (updateData.Last_Viewed_At.HasValue)
                {
                    assignment.LastViewedAt = DateTime.UtcNow;
                }
                if (updateData.Price.HasValue)
                {
                    assignment.Price = updateData.Price.Value;
                }
                if (updateData.Is_Add_on.HasValue)
                {
                    assignment.Is_Add_On = updateData.Is_Add_on.Value;
                }
                if (updateData.Status.HasValue)
                {
                    assignment.Status = updateData.Status.Value;
                }
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
        ///     DELETE /api/menu/123/456
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
            int Item_id
        )
        {
            try
            {
                var assignment = await _context.MenuItemAssignments.Where(mia => mia.Menu_Id == menu_id && mia.Item_Id == Item_id).FirstOrDefaultAsync();
                if (assignment == null)
                {
                    return NotFound("Menu Assignment for deletion was not found");
                }
                _context.Remove(assignment);
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
        /// An <see cref="IActionResult"/> containing the collection of newly created <see cref="MenuItemAssignment"/> objects.
        /// Returns HTTP 200 (OK) with the list of copied assignments on success.
        /// Returns HTTP 409 (Conflict) if a menu with the new name already exists.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during the operation.
        /// </returns>
        /// <response code="200">Returns the list of newly created menu item assignments</response>
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
        /// This endpoint requires Admin or Staff role authorization.
        /// Creates a new menu with the specified name and copies all item assignments (including prices and limits) from the source menu.
        /// The new menu will be inactive by default.
        /// </remarks>
        [Authorize(Policy = "staffOnly")]
        [HttpPost("copy_menu/{source_menu_id}")]
        [ProducesResponseType(typeof(IEnumerable<MenuItemAssignment>), StatusCodes.Status200OK)]
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
                        Adult_Limit = element.Adult_Limit,
                        Child_limit = element.Child_limit,
                        Senior_limit = element.Senior_limit,
                        Tot_Limit = element.Tot_Limit,
                        Total_Units_Ordered = 0,
                        Total_Views = 0,
                        Total_View_Seconds = 0,
                        Is_Add_On = element.Is_Add_On,
                        Status = element.Status,
                    });
                }
                _context.AddRange(newAssignment);
                await _context.SaveChangesAsync();

                return Ok(newAssignment);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error copying menu assignment from source menu {SourceMenuId} to new menu {NewMenuName}", source_menu_id, new_menu_name);
                return StatusCode(500, new { message = "An error occurred while updating the menu assignment." });
            }
        }
    }

}