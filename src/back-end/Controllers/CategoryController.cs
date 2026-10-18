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
        /// Returns all categories in the system with their details including limits and statistics.
        /// </remarks>
        //GET api/category
        //Get all categories
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<Category>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<Category>>> GetAllCategories()
        {
            try
            {
                var category = await _context.Categories.ToListAsync();
                return Ok(category);
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
        ///         "total_limit": 0,
        ///         "over_limit_behavior": "Block"
        ///     }
        ///
        /// Creates a new category with the provided information.
        /// Category names must be unique.
        /// Last viewed timestamp is automatically set to current UTC time.
        /// A limit of 0 means unlimited, except toddler_limit: 0 means "same as child_limit".
        /// Rules: child and senior ≤ adult, toddler ≤ child, adult ≤ total
        /// (total is an optional hard ceiling for the table).
        /// Over limit behavior determines how the system handles situations where the specified limits are exceeded. Accepts "Block" or "Charge".
        /// </remarks>
        [Authorize(Policy = "adminOnly")]
        [HttpPost]
        [ProducesResponseType(typeof(Category), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateCategory(CreateCategory category)
        {
            try
            {
                var error = LimitRules.Validate(category.Adult_Limit, category.Child_Limit, category.Senior_Limit, category.Toddler_Limit, category.Total_Limit);
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
                    adult_limit = category.Adult_Limit,
                    child_limit = category.Child_Limit,
                    senior_limit = category.Senior_Limit,
                    toddler_limit = category.Toddler_Limit,
                    total_limit = category.Total_Limit,
                    over_limit_behavior = category.Over_Limit_Behavior,
                    last_viewed_at = DateTime.UtcNow
                };

                _context.Categories.Add(categoryEntity);
                await _context.SaveChangesAsync();

                return Ok(categoryEntity);
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
        [ProducesResponseType(typeof(Category), StatusCodes.Status200OK)]
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

                return Ok(category);
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
        ///         "total_limit": 0,
        ///         "over_limit_behavior": "Block"
        ///     }
        ///
        /// Updates the category information and limits.
        /// Name and description are optional - if not provided, existing values are retained.
        /// Last viewed timestamp is automatically updated to current UTC time.
        /// A limit of 0 means unlimited, except toddler_limit: 0 means "same as child_limit".
        /// Rules: child and senior ≤ adult, toddler ≤ child, adult ≤ total
        /// (total is an optional hard ceiling for the table).
        /// Over limit behavior determines how the system handles situations where the specified limits are exceeded. Accepts Block or Charge.
        /// </remarks>
        [Authorize(Policy = "adminOnly")]
        [HttpPut("update_category/{_categoryId}")]
        [ProducesResponseType(typeof(Category), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
                var total = updatedCategory.Total_Limit ?? existingCategory.total_limit;


                var error = LimitRules.Validate(adult, child, senior, toddler, total);
                if (error != null) return BadRequest(error);

                existingCategory.adult_limit = adult;
                existingCategory.child_limit = child;
                existingCategory.senior_limit = senior;
                existingCategory.toddler_limit = toddler;
                existingCategory.total_limit = total;
                existingCategory.last_viewed_at = DateTime.UtcNow;
                existingCategory.Category_name = updatedCategory.Name ?? existingCategory.Category_name;
                existingCategory.Description = updatedCategory.Description ?? existingCategory.Description;
                existingCategory.over_limit_behavior = updatedCategory.Over_Limit_Behavior ?? existingCategory.over_limit_behavior;
                _context.Categories.Update(existingCategory);
                await _context.SaveChangesAsync();
                return Ok(existingCategory);


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
        /// This may affect menu items associated with this category depending on cascade settings.
        /// </remarks>
        [Authorize(Policy = "adminOnly")]
        [HttpDelete("delete_category/{_categoryId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
        [ProducesResponseType(typeof(IEnumerable<Menu_Item>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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

        /// <summary>
        /// Tracks a view event for a specific category, recording view duration and incrementing view count.
        /// </summary>
        /// <param name="_categoryId">The unique identifier of the category</param>
        /// <param name="viewSeconds">The number of seconds the category was viewed</param>
        /// <returns>
        /// An <see cref="IActionResult"/> indicating the result of the operation.
        /// Returns HTTP 200 (OK) with a success message when the view is tracked.
        /// Returns HTTP 400 (Bad Request) if view_seconds is negative.
        /// Returns HTTP 404 (Not Found) if the category doesn't exist.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during tracking.
        /// </returns>
        /// <response code="200">Returns a success message when the category view is tracked</response>
        /// <response code="400">If view_seconds is a negative value</response>
        /// <response code="404">If the category is not found</response>
        /// <response code="500">If an internal error occurs while tracking the view</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     POST /api/category/track_viewed/123?view_seconds=45
        ///
        /// Updates the category's tracking statistics:
        /// - Increments total view count by 1
        /// - Adds view_seconds to total view duration
        /// - Updates last viewed timestamp to current UTC time
        /// </remarks>
        [Authorize(Policy = "staffOnly")]
        [HttpPost("track_viewed/{_categoryId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> TrackCategoryView(int _categoryId, [FromQuery(Name = "view_seconds")] int viewSeconds)
        {
            try
            {
                if (viewSeconds < 0)
                {
                    return BadRequest("view_seconds must be a non-negative integer.");
                }
                var category = await _context.Categories
                    .FirstOrDefaultAsync(c => c.Category_id == _categoryId);

                if (category == null)
                {
                    return NotFound("Category not found.");
                }

                category.last_viewed_at = DateTime.UtcNow;
                category.total_view_seconds += viewSeconds;
                category.total_views += 1;
                _context.Categories.Update(category);
                await _context.SaveChangesAsync();
                return Ok("Category view tracked successfully.");

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cannot track Category view");
                return StatusCode(500, "Internal Server Error");
            }
        }

        //This endpoint is in the python project with notes that it needs parameters to be added to menu item model, comment out as 
        // it is not being used and would cause errors if called but included for future implementation.

        // [HttpPut("reorder-items{_categoryId}")]


        // public async Task<IActionResult> ReorderMenuItems(int categoryId, [FromBody] List<int> itemIds)
        // {
        //     if (itemIds == null || itemIds.Count == 0)
        //         return BadRequest("itemIds cannot be empty.");

        //     if (itemIds.Count != itemIds.Distinct().Count())
        //         return BadRequest("itemIds contains duplicates.");

        //     var categoryExists = await _context.Categories
        //         .AsNoTracking()
        //         .AnyAsync(c => c.Category_id == categoryId);

        //     if (!categoryExists)
        //         return NotFound("Category not found.");

        //     var fetchedItems = await _context.MenuItems
        //         .Where(mi => mi.Category_id == categoryId && itemIds.Contains(mi.item_id))
        //         .ToListAsync();

        //     if (fetchedItems.Count != itemIds.Count)
        //         return BadRequest("One or more itemIds are invalid or not in this category.");

        //     // Map ItemId -> index (0-based; use idx + 1 if you want 1-based)
        //     var orderMap = itemIds
        //         .Select((id, idx) => new { id, idx })
        //         .ToDictionary(x => x.id, x => x.idx);

        //     foreach (var item in fetchedItems)
        //         item.DisplayOrder = orderMap[item.item_id];

        //     try
        //     {
        //         await _context.SaveChangesAsync();
        //         return Ok(new { message = "Menu items reordered successfully", updated = fetchedItems.Count });
        //     }
        //     catch (DbUpdateException)
        //     {
        //         // If you added a unique index on (CategoryId, DisplayOrder), concurrency could trigger this
        //         return Conflict("Could not reorder due to a constraint/concurrency issue. Please retry.");
        //     }
        // }


    }
}