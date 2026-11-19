using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.Dtos;
using Microsoft.AspNetCore.Authorization;
using back_end.Helpers;

namespace back_end.Controllers
{
    [ApiController]
    [Route("api/[controller]")]

    public class CategoryController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CategoryController> _logger;

        public CategoryController(ApplicationDbContext context, ILogger<CategoryController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private static CategoryResponse ToResponse(Category c) => new()
        {
            CategoryId = c.Category_id,
            CategoryName = c.Category_name,
            Description = c.Description,
            AdultLimit = c.adult_limit,
            ChildLimit = c.child_limit,
            SeniorLimit = c.senior_limit,
            ToddlerLimit = c.toddler_limit,
            PerOrderLimit = c.per_order_limit,
            PerOrderPerGuest = c.per_order_per_guest,
            OverLimitBehavior = c.over_limit_behavior,
        };


        /// <summary>
        /// Retrieves all categories from the database.
        /// </summary>
        /// <response code="200">Returns the list of all categories</response>
        /// <response code="500">If an internal error occurs while retrieving categories</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     GET /api/category
        ///
        /// Returns all categories in the system with their details including limits.
        /// </remarks>
        //GET api/category
        //Get all categories
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<CategoryResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<CategoryResponse>>> GetAllCategories()
        {
            try
            {
                var categories = await _context.Categories.ToListAsync();
                return Ok(categories.Select(ToResponse).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cannot get all Categories");
                return StatusCode(500, "Internal Server Error");
            }
        }

        /// <summary>
        /// Creates a new category in the system.
        /// </summary>
        /// <param name="category">The category data including name, description, and limits</param>
        /// <response code="200">Returns the newly created category</response>
        /// <response code="401">Not signed in.</response>
        /// <response code="403">Not an admin.</response>
        /// <response code="409">If a category with the same name already exists</response>
        /// <response code="400">If a required limit is missing, or the limits are invalid</response>
        /// <response code="500">If an internal error occurs while creating the category</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     POST /api/category
        ///     {
        ///         "name": "Appetizers",
        ///         "description": "Starter dishes and small plates",
        ///         "adult_limit": 6,
        ///         "child_limit": 4,
        ///         "senior_limit": 5,
        ///         "toddler_limit": 2,
        ///         "per_order_limit": 0,
        ///         "per_order_per_guest": false,
        ///         "over_limit_behavior": "Block"
        ///     }
        ///
        /// Creates a new category with the provided information.
        /// Category names must be unique.
        /// adult_limit, child_limit, and senior_limit are required (0 = unlimited).
        /// toddler_limit is optional: 0 or omitted = same as child_limit.
        /// Rules: child and senior ≤ adult, toddler ≤ child.
        /// Because 0 means unlimited, a capped adult_limit needs capped child and senior limits too.
        /// per_order_limit = units from this category in one order for the whole table (0 = no cap);
        /// going over it always blocks, even when over_limit_behavior is Charge.
        /// over_limit_behavior: "Block" (default) refuses units past a per-guest limit; "Charge" bills them
        /// at the item's menu price on a separate over-limit line (refused if the item has no price).
        /// per_order_per_guest = true makes per_order_limit a cap for each guest. 
        /// The orders cap becomes per_order_limit * number of guests. 
        /// </remarks>
        [Authorize(Policy = "adminOnly")]
        [HttpPost]
        [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateCategory(CreateCategory category)
        {
            try
            {
                var error = LimitRules.Validate(category.AdultLimit!.Value, category.ChildLimit!.Value,
                                  category.SeniorLimit!.Value, category.ToddlerLimit, category.PerOrderLimit);
                if (error != null) return BadRequest(error);
                var newCategory = await _context.Categories.FirstOrDefaultAsync(c => c.Category_name == category.Name);
                if (newCategory != null)
                {
                    return Conflict("Category with the same name already exists.");
                }

                var categoryEntity = new Category
                {
                    Category_name = category.Name ?? string.Empty,
                    Description = category.Description ?? string.Empty,
                    adult_limit = category.AdultLimit!.Value,
                    child_limit = category.ChildLimit!.Value,
                    senior_limit = category.SeniorLimit!.Value,
                    toddler_limit = category.ToddlerLimit,
                    per_order_limit = category.PerOrderLimit,
                    per_order_per_guest = category.PerOrderPerGuest,
                    over_limit_behavior = category.OverLimitBehavior
                };

                _context.Categories.Add(categoryEntity);
                await _context.SaveChangesAsync();

                return Ok(ToResponse(categoryEntity));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cannot create Category");
                return StatusCode(500, "Internal Server Error");
            }


        }

        /// <summary>
        /// Retrieves a specific category by its ID.
        /// </summary>
        /// <param name="categoryId">The unique identifier of the category</param>
        /// <response code="200">Returns the category with the specified ID</response>
        /// <response code="404">If the category is not found</response>
        /// <response code="500">If an internal error occurs while retrieving the category</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     GET /api/category/get-category/123
        ///
        /// Returns the complete category details for the specified ID.
        /// </remarks>
        [HttpGet("get-category/{categoryId}")]
        [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetCategoryById(int categoryId)
        {
            try
            {
                var category = await _context.Categories
                    .FirstOrDefaultAsync(c => c.Category_id == categoryId);

                if (category == null)
                {
                    return NotFound("Category not found.");
                }

                return Ok(ToResponse(category));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cannot get Category by ID");
                return StatusCode(500, "Internal Server Error");
            }
        }

        /// <summary>
        /// Updates an existing category's information and limits.
        /// </summary>
        /// <param name="categoryId">The unique identifier of the category to update</param>
        /// <param name="updatedCategory">The updated category data</param>
        /// <response code="200">Returns the updated category</response>
        /// <response code="401">Not signed in.</response>
        /// <response code="403">Not an admin.</response>
        /// <response code="400">If the request data is invalid</response>
        /// <response code="404">If the category is not found</response>
        /// <response code="500">If an internal error occurs while updating the category</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     PUT /api/category/update-category/123
        ///     {
        ///         "name": "Appetizers",
        ///         "description": "Starter dishes and small plates",
        ///         "adult_limit": 6,
        ///         "child_limit": 4,
        ///         "senior_limit": 5,
        ///         "toddler_limit": 2,
        ///         "per_order_limit": 0,
        ///         "per_order_per_guest": false,
        ///         "over_limit_behavior": "Block"
        ///     }
        ///
        /// Updates the category information and limits.
        /// Every field is optional: anything left out keeps its current value.
        /// A limit of 0 means unlimited, except toddler_limit: 0 means "same as child_limit".
        /// Rules: child and senior ≤ adult, toddler ≤ child.
        /// Because 0 means unlimited, a capped adult_limit needs capped child and senior limits too.
        /// per_order_limit = units from this category in one order for the whole table (0 = no cap);
        /// going over it always blocks, even when over_limit_behavior is Charge.
        /// over_limit_behavior: "Block" refuses units past a per-guest limit; "Charge" bills them
        /// at the item's menu price on a separate over-limit line (refused if the item has no price).
        /// per_order_per_guest = true makes per_order_limit a cap for each guest. 
        /// The orders cap becomes per_order_limit * number of guests. 
        /// </remarks>
        [Authorize(Policy = "adminOnly")]
        [HttpPut("update-category/{categoryId}")]
        [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateCategory(int categoryId, UpdateCategory updatedCategory)
        {
            try
            {
                var existingCategory = await _context.Categories
                    .FirstOrDefaultAsync(c => c.Category_id == categoryId);

                if (existingCategory == null)
                {
                    return NotFound("Category not found.");
                }

                var adult = updatedCategory.AdultLimit ?? existingCategory.adult_limit;
                var child = updatedCategory.ChildLimit ?? existingCategory.child_limit;
                var senior = updatedCategory.SeniorLimit ?? existingCategory.senior_limit;
                var toddler = updatedCategory.ToddlerLimit ?? existingCategory.toddler_limit;
                var perOrder = updatedCategory.PerOrderLimit ?? existingCategory.per_order_limit;

                var error = LimitRules.Validate(adult, child, senior, toddler, perOrder);
                if (error != null) return BadRequest(error);

                existingCategory.adult_limit = adult;
                existingCategory.child_limit = child;
                existingCategory.senior_limit = senior;
                existingCategory.toddler_limit = toddler;
                existingCategory.per_order_limit = perOrder;
                existingCategory.per_order_per_guest = updatedCategory.PerOrderPerGuest ?? existingCategory.per_order_per_guest;
                existingCategory.Category_name = updatedCategory.Name ?? existingCategory.Category_name;
                existingCategory.Description = updatedCategory.Description ?? existingCategory.Description;
                existingCategory.over_limit_behavior = updatedCategory.OverLimitBehavior ?? existingCategory.over_limit_behavior;
                _context.Categories.Update(existingCategory);
                await _context.SaveChangesAsync();
                return Ok(ToResponse(existingCategory));


            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cannot update Category");
                return StatusCode(500, "Internal Server Error");
            }
        }

        /// <summary>
        /// Deletes a category from the system.
        /// </summary>
        /// <param name="categoryId">The unique identifier of the category to delete</param>
        /// <response code="200">Returns a success message when the category is deleted</response>
        /// <response code="401">Not signed in.</response>
        /// <response code="403">Not an admin.</response>
        /// <response code="404">If the category is not found</response>
        /// <response code="409">If the category has associated menu items and cannot be deleted</response>
        /// <response code="500">If an internal error occurs while deleting the category</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     DELETE /api/category/delete-category/123
        ///
        /// Permanently removes the category from the database.
        /// If the category has associated menu items, deletion will be blocked and a conflict response will be returned.
        /// </remarks>
        [Authorize(Policy = "adminOnly")]
        [HttpDelete("delete-category/{categoryId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteCategory(int categoryId)
        {
            try
            {
                var category = await _context.Categories
                    .FirstOrDefaultAsync(c => c.Category_id == categoryId);

                if (category == null)
                {
                    return NotFound("Category not found.");
                }

                if (await _context.MenuItems.AnyAsync(m => m.Category_id == categoryId))
                    return Conflict("Cannot delete category, it has associated menu items. Remove or move them first.");

                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();

                return Ok("Category deleted successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cannot delete Category");
                return StatusCode(500, "Internal Server Error");
            }
        }

        /// <summary>
        /// Retrieves menu items for a specific category with pagination support.
        /// </summary>
        /// <param name="categoryId">The unique identifier of the category</param>
        /// <param name="skip">The number of items to skip for pagination (default: 0)</param>
        /// <param name="limit">The maximum number of items to return (default: 10)</param>
        /// <response code="200">Returns the list of menu items for the specified category</response>
        /// <response code="401">Not signed in.</response>
        /// <response code="403">Not an admin.</response>
        /// <response code="404">If no menu items are found for the category</response>
        /// <response code="500">If an internal error occurs while retrieving menu items</response>
        /// <remarks>
        /// Sample requests:
        ///
        ///     GET /api/category/get-menu-by-category/123
        ///     (Returns first 10 items)
        ///     
        ///     GET /api/category/get-menu-by-category/123?skip=10&amp;limit=20
        ///     (Returns items 11-30)
        ///
        /// Supports pagination through skip and limit parameters.
        /// Default pagination: skip=0, limit=10
        /// </remarks>
        [HttpGet("get-menu-by-category/{categoryId}")]
        [Authorize(Policy = "adminOnly")]
        [ProducesResponseType(typeof(IEnumerable<MenuItemResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetMenuItemsByCategory(int categoryId, int skip = 0, int limit = 10)
        {
            try
            {
                var menuItems = await _context.MenuItems
                    .Include(m => m.MenuItemTags)
                        .ThenInclude(mit => mit.Tag)
                    .Where(m => m.Category_id == categoryId)
                    .Skip(skip)
                    .Take(limit)
                    .ToListAsync();

                if (menuItems == null || menuItems.Count == 0)
                {
                    return NotFound("No menu items found for the specified category.");
                }

                return Ok(menuItems.Select(MenuItemResponseDto.From).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cannot get Menu Items by Category ID");
                return StatusCode(500, "Internal Server Error");
            }
        }
    }
}