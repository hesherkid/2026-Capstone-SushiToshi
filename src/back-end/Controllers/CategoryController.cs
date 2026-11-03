using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.DTO.Category;
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
            Category_Id = c.Category_id,
            Category_Name = c.Category_name,
            Description = c.Description,
            Adult_Limit = c.adult_limit,
            Child_Limit = c.child_limit,
            Senior_Limit = c.senior_limit,
            Toddler_Limit = c.toddler_limit,
            Per_Order_Limit = c.per_order_limit,
            Over_Limit_Behavior = c.over_limit_behavior,
        };


        /// <summary>
        /// Retrieves all categories from the database.
        /// </summary>
        /// <returns>
        /// An <see cref="ActionResult"/> containing a collection of <see cref="Category"/> objects.
        /// Returns HTTP 200 (OK) with the list of all categories on success.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during retrieval.
        /// </returns>
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
        /// <returns>
        /// An <see cref="IActionResult"/> containing the created <see cref="Category"/> object.
        /// Returns HTTP 200 (OK) with the created category on success.
        /// Returns HTTP 409 (Conflict) if a category with the same name already exists.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during creation.
        /// </returns>
        /// <response code="200">Returns the newly created category</response>
        /// <response code="409">If a category with the same name already exists</response>
        /// <response code="400">If the provided limits are invalid</response>
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
        ///         "over_limit_behavior": "Block"
        ///     }
        ///
        /// Creates a new category with the provided information.
        /// Category names must be unique.
        /// adult_limit, child_limit, and senior_limit are required (0 = unlimited).
        /// toddler_limit is optional: 0 or omitted = same as child_limit.
        /// Rules: child and senior ≤ adult, toddler ≤ child.
        /// per_order_limit: max combined units from this category per order for the whole table (0 = no limit cap, always blocks).
        /// over_limit_behavior ("Block" or "Charge", default "Block") controls what happens when a limit is exceeded;
        /// "Charge" bills each item's price from its menu assignment.
        /// </remarks>
        [Authorize(Policy = "adminOnly")]
        [HttpPost]
        [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateCategory(CreateCategory category)
        {
            try
            {
                var error = LimitRules.Validate(category.Adult_Limit!.Value, category.Child_Limit!.Value,
                                  category.Senior_Limit!.Value, category.Toddler_Limit, category.Per_Order_Limit);
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
                    adult_limit = category.Adult_Limit!.Value,
                    child_limit = category.Child_Limit!.Value,
                    senior_limit = category.Senior_Limit!.Value,
                    toddler_limit = category.Toddler_Limit,
                    per_order_limit = category.Per_Order_Limit,
                    over_limit_behavior = category.Over_Limit_Behavior
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
        /// <param name="_categoryId">The unique identifier of the category</param>
        /// <returns>
        /// An <see cref="IActionResult"/> containing the <see cref="Category"/> object.
        /// Returns HTTP 200 (OK) with the category details on success.
        /// Returns HTTP 404 (Not Found) if the category doesn't exist.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during retrieval.
        /// </returns>
        /// <response code="200">Returns the category with the specified ID</response>
        /// <response code="404">If the category is not found</response>
        /// <response code="500">If an internal error occurs while retrieving the category</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     GET /api/category/get_category/123
        ///
        /// Returns the complete category details for the specified ID.
        /// </remarks>
        [HttpGet("get_category/{_categoryId}")]
        [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetCategoryById(int _categoryId)
        {
            try
            {
                var category = await _context.Categories
                    .FirstOrDefaultAsync(c => c.Category_id == _categoryId);

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
        /// <param name="_categoryId">The unique identifier of the category to update</param>
        /// <param name="updatedCategory">The updated category data</param>
        /// <returns>
        /// An <see cref="IActionResult"/> containing the updated category object.
        /// Returns HTTP 200 (OK) with the updated category on success.
        /// Returns HTTP 404 (Not Found) if the category doesn't exist.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during the update.
        /// </returns>
        /// <response code="200">Returns the updated category</response>
        /// <response code="404">If the category is not found</response>
        /// <response code="500">If an internal error occurs while updating the category</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     PUT /api/category/update_category/123
        ///     {
        ///         "name": "Appetizers",
        ///         "description": "Starter dishes and small plates",
        ///         "adult_limit": 6,
        ///         "child_limit": 4,
        ///         "senior_limit": 5,
        ///         "toddler_limit": 2,
        ///         "per_order_limit": 0,
        ///         "over_limit_behavior": "Block"
        ///     }
        ///
        /// Updates the category information and limits.
        /// Name and description are optional - if not provided, existing values are retained.
        /// A limit of 0 means unlimited, except toddler_limit: 0 means "same as child_limit".
        /// Rules: child and senior ≤ adult, toddler ≤ child.
        /// per_order_limit: max combined units from this category per order for the whole table (0 = no limit cap, always blocks).
        /// Over limit behavior determines how the system handles situations where the specified limits are exceeded. Accepts Block or Charge.
        /// </remarks>
        [Authorize(Policy = "adminOnly")]
        [HttpPut("update_category/{_categoryId}")]
        [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateCategory(int _categoryId, UpdateCategory updatedCategory)
        {
            try
            {
                var existingCategory = await _context.Categories
                    .FirstOrDefaultAsync(c => c.Category_id == _categoryId);

                if (existingCategory == null)
                {
                    return NotFound("Category not found.");
                }

                var adult = updatedCategory.Adult_Limit ?? existingCategory.adult_limit;
                var child = updatedCategory.Child_Limit ?? existingCategory.child_limit;
                var senior = updatedCategory.Senior_Limit ?? existingCategory.senior_limit;
                var toddler = updatedCategory.Toddler_Limit ?? existingCategory.toddler_limit;
                var perOrder = updatedCategory.Per_Order_Limit ?? existingCategory.per_order_limit;

                var error = LimitRules.Validate(adult, child, senior, toddler, perOrder);
                if (error != null) return BadRequest(error);

                existingCategory.adult_limit = adult;
                existingCategory.child_limit = child;
                existingCategory.senior_limit = senior;
                existingCategory.toddler_limit = toddler;
                existingCategory.per_order_limit = perOrder;
                existingCategory.Category_name = updatedCategory.Name ?? existingCategory.Category_name;
                existingCategory.Description = updatedCategory.Description ?? existingCategory.Description;
                existingCategory.over_limit_behavior = updatedCategory.Over_Limit_Behavior ?? existingCategory.over_limit_behavior;
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
        /// <param name="_categoryId">The unique identifier of the category to delete</param>
        /// <returns>
        /// An <see cref="IActionResult"/> indicating the result of the operation.
        /// Returns HTTP 200 (OK) with a success message when the category is deleted.
        /// Returns HTTP 404 (Not Found) if the category doesn't exist.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during deletion.
        /// </returns>
        /// <response code="200">Returns a success message when the category is deleted</response>
        /// <response code="404">If the category is not found</response>
        /// <response code="500">If an internal error occurs while deleting the category</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     DELETE /api/category/delete_category/123
        ///
        /// Permanently removes the category from the database.
        /// If the category has associated menu items, deletion will be blocked and a conflict response will be returned.
        /// </remarks>
        [Authorize(Policy = "adminOnly")]
        [HttpDelete("delete_category/{_categoryId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeleteCategory(int _categoryId)
        {
            try
            {
                var category = await _context.Categories
                    .FirstOrDefaultAsync(c => c.Category_id == _categoryId);

                if (category == null)
                {
                    return NotFound("Category not found.");
                }

                if (await _context.MenuItems.AnyAsync(m => m.Category_id == _categoryId))
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
        /// <param name="_categoryId">The unique identifier of the category</param>
        /// <param name="skip">The number of items to skip for pagination (default: 0)</param>
        /// <param name="limit">The maximum number of items to return (default: 10)</param>
        /// <returns>
        /// An <see cref="IActionResult"/> containing a collection of <see cref="Menu_Item"/> objects.
        /// Returns HTTP 200 (OK) with the list of menu items on success.
        /// Returns HTTP 404 (Not Found) if no menu items exist for the category.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during retrieval.
        /// </returns>
        /// <response code="200">Returns the list of menu items for the specified category</response>
        /// <response code="404">If no menu items are found for the category</response>
        /// <response code="500">If an internal error occurs while retrieving menu items</response>
        /// <remarks>
        /// Sample requests:
        ///
        ///     GET /api/category/get_menu_by_category/123
        ///     (Returns first 10 items)
        ///     
        ///     GET /api/category/get_menu_by_category/123?skip=10&amp;limit=20
        ///     (Returns items 11-30)
        ///
        /// Supports pagination through skip and limit parameters.
        /// Default pagination: skip=0, limit=10
        /// </remarks>
        [HttpGet("get_menu_by_category/{_categoryId}")]
        [Authorize(Policy = "adminOnly")]
        [ProducesResponseType(typeof(IEnumerable<Menu_Item>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetMenuItemsByCategory(int _categoryId, int skip = 0, int limit = 10)
        {
            try
            {
                var menuItems = await _context.MenuItems
                    .Where(m => m.Category_id == _categoryId)
                    .Skip(skip)
                    .Take(limit)
                    .ToListAsync();

                if (menuItems == null || menuItems.Count == 0)
                {
                    return NotFound("No menu items found for the specified category.");
                }

                return Ok(menuItems);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cannot get Menu Items by Category ID");
                return StatusCode(500, "Internal Server Error");
            }
        }
    }
}