using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using System.Linq.Expressions;
using back_end.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using back_end.domain.Seeders;
using Pomelo.EntityFrameworkCore.MySql.Storage.Internal;
using System.Formats.Asn1;
using System.Security.Cryptography.X509Certificates;

namespace back_end.Controllers
{
    [ApiController]
    [Route("api/[controller]")]

    public class MenuItemController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MenuController> _logger;
        private readonly IWebHostEnvironment _env;

        public MenuItemController(ApplicationDbContext context, ILogger<MenuController> logger, IWebHostEnvironment env)
        {
            _context = context;
            _logger = logger;
            _env = env;
        }
        /// <summary>
        /// Creates a new menu item with optional tags.
        /// </summary>
        /// <param name="itemData">The menu item data including name, description, category, image URL, and tag IDs</param>
        /// <returns>
        /// An <see cref="IActionResult"/> containing the created <see cref="MenuItemResponseDto"/> object.
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
        ///     POST /api/menuitem
        ///     {
        ///         "name": "Grilled Salmon",
        ///         "description": "Fresh Atlantic salmon with lemon butter sauce",
        ///         "category_id": 2,
        ///         "item_image_url": "https://example.com/images/salmon.jpg",
        ///         "tag_ids": [1, 3, 5]
        ///     }
        ///
        /// This endpoint requires Admin or Staff role authorization.
        /// The menu item is automatically created with 'Available' status.
        /// Tags are optional and will be associated with the item if provided.
        /// </remarks>
        [Authorize(Policy = "staffOnly")]
        [HttpPost]
        [ProducesResponseType(typeof(MenuItemResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateMenuItem(
            MenuItemCreateDto itemData
        )
        {
            try
            {
                if (await _context.MenuItems.AnyAsync(mi => mi.Name == itemData.Name))
                {
                    return Conflict("Menu Item already Exist");
                }
                Menu_Item newMenuItem = new Menu_Item
                {
                    Name = itemData.Name,
                    Description = itemData.Description ?? string.Empty,
                    Category_id = itemData.CategoryId,
                    image_url = itemData.ItemImageUrl,
                    Status = MenuItemStatus.Available
                };
                if (itemData.TagIds.Count > 0)
                {
                    var tags = await _context.Tags.Where(t => itemData.TagIds.Contains(t.tag_id)).ToListAsync();
                    foreach (var tag in tags)
                    {
                        newMenuItem.MenuItemTags.Add(new MenuItemTag
                        {
                            Tag = tag,
                            MenuItem = newMenuItem
                        });
                    }
                }
                _context.Add(newMenuItem);
                await _context.SaveChangesAsync();
                return Ok(MenuItemResponseDto.From(newMenuItem));
            }
            catch (Exception ex)
            {


                _logger.LogError(ex, "Error creating menu item {MenuItemName}", itemData.Name);
                return StatusCode(500, new { message = "An error occurred while creating the menu item", error = ex.Message });
            }
        }

        /// <summary>
        /// Retrieves a specific menu item by its ID.
        /// </summary>
        /// <param name="itemId">The unique identifier of the menu item</param>
        /// <returns>
        /// An <see cref="IActionResult"/> containing the <see cref="MenuItemWithAssignmentsDto"/> object.
        /// Returns HTTP 200 (OK) with the menu item details on success.
        /// Returns HTTP 404 (Not Found) if the menu item doesn't exist.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during retrieval.
        /// </returns>
        /// <response code="200">Returns the menu item with associated tags</response>
        /// <response code="404">If the menu item is not found</response>
        /// <response code="500">If an internal error occurs while retrieving the menu item</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     GET /api/menuitem/123
        ///
        /// This endpoint requires authentication.
        /// Returns the menu item with all associated tags.
        /// </remarks>

        [HttpGet("{itemId}")]
        [ProducesResponseType(typeof(MenuItemWithAssignmentsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetMenuItem(
            int itemId
        )
        {
            try
            {
                var item = await _context.MenuItems
                    .Include(mi => mi.MenuAssignments)
                        .ThenInclude(ma => ma.Menu)  // Navigate through MenuAssignments to get Menu
                    .Include(mi => mi.MenuItemTags)
                        .ThenInclude(mit => mit.Tag)
                    .FirstOrDefaultAsync(mi => mi.item_id == itemId);
                if (item == null)
                {
                    return NotFound(new { message = "Menu item was not found" });
                }
                return Ok(MenuItemWithAssignmentsDto.From(item));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active session");
                return StatusCode(500, new { message = "An error occurred while processing your request.", error = ex.Message });
            }
        }



        /// <summary>
        /// Retrieves all menu items from the database.
        /// </summary>
        /// <returns>
        /// An <see cref="IActionResult"/> containing a collection of <see cref="MenuItemResponseDto"/> objects.
        /// Returns HTTP 200 (OK) with the list of all menu items on success.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during retrieval.
        /// </returns>
        /// <response code="200">Returns the list of all menu items with associated tags</response>
        /// <response code="500">If an internal error occurs while retrieving menu items</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     GET /api/menuitem
        ///
        /// This endpoint requires authentication.
        /// Returns all menu items in the system with their associated tags.
        /// </remarks>

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<MenuItemResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ListMenuItems()
        {
            try
            {
                var allItems = await _context.MenuItems
                    .Include(mi => mi.MenuItemTags)
                        .ThenInclude(mit => mit.Tag)
                    .OrderBy(mi => mi.Category_id)
                    .ThenBy(mi => mi.Name)
                    .ToListAsync();

                var result = allItems.Select(MenuItemResponseDto.From).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving menu items");
                return StatusCode(500, new { message = "An error occurred while processing your request.", error = ex.Message });
            }
        }

        /// <summary>
        /// Updates an existing menu item.
        /// </summary>
        /// <param name="itemId">The unique identifier of the menu item to update</param>
        /// <param name="menuItemUpdate">The updated menu item data</param>
        /// <returns>
        /// An <see cref="IActionResult"/> containing the updated <see cref="MenuItemResponseDto"/> object.
        /// Returns HTTP 200 (OK) with the updated menu item on success.
        /// Returns HTTP 404 (Not Found) if the menu item doesn't exist.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during the update.
        /// </returns>
        /// <response code="200">Returns the updated menu item</response>
        /// <response code="404">If the menu item is not found</response>
        /// <response code="500">If an internal error occurs while updating the menu item</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     PUT /api/menuitem/123
        ///     {
        ///         "name": "Updated Grilled Salmon",
        ///         "description": "Premium Atlantic salmon with garlic butter",
        ///         "category_id": 2,
        ///         "item_image_url": "https://example.com/images/salmon-new.jpg",
        ///         "status": "Available",
        ///         "tag_ids": [1, 3, 5, 7]
        ///     }
        ///
        /// This endpoint requires Admin or Staff role authorization.
        /// All fields in the request body are optional - only provided fields will be updated.
        /// Tag IDs will be added to existing tags (not replaced).
        /// </remarks>
        [Authorize(Policy = "staffOnly")]
        [HttpPut("{itemId}")]
        [ProducesResponseType(typeof(MenuItemResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateMenuItem(
            int itemId,
            MenuItemUpdateDto menuItemUpdate
        )
        {
            try
            {
                var itemUpdate = await _context.MenuItems.FirstOrDefaultAsync(mi => mi.item_id == itemId);
                if (itemUpdate == null)
                {
                    return NotFound("Menu Item was Not Found ");
                }
                if (menuItemUpdate.Name != null)
                {
                    itemUpdate.Name = menuItemUpdate.Name;
                }
                if (menuItemUpdate.Description != null)
                {
                    itemUpdate.Description = menuItemUpdate.Description;
                }
                if (menuItemUpdate.CategoryId != null)
                {
                    itemUpdate.Category_id = (int)menuItemUpdate.CategoryId;
                }
                if (menuItemUpdate.ItemImageUrl != null)
                {
                    itemUpdate.image_url = menuItemUpdate.ItemImageUrl;
                }
                if (menuItemUpdate.Status != null)
                {
                    itemUpdate.Status = (MenuItemStatus)menuItemUpdate.Status;
                }
                if (menuItemUpdate != null && menuItemUpdate.TagIds != null && menuItemUpdate.TagIds.Count > 0)
                {
                    await _context.Entry(itemUpdate).Collection(i => i.MenuItemTags).LoadAsync();
                    foreach (var tagId in menuItemUpdate.TagIds.Distinct())
                    {
                        itemUpdate.MenuItemTags.Add(new MenuItemTag
                        {
                            Menu_item_id = itemUpdate.item_id,
                            Tag_id = tagId
                        });
                    }
                    ;
                }
                ;
                _context.Update(itemUpdate);
                await _context.SaveChangesAsync();
                // The tags added above only have Tag_id, so load each one's Tag for the response
                await _context.Entry(itemUpdate).Collection(i => i.MenuItemTags).Query().Include(mit => mit.Tag).LoadAsync();
                return Ok(MenuItemResponseDto.From(itemUpdate));

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active session");
                return StatusCode(500, new { message = "An error occurred while processing your request.", error = ex.Message });
            }
        }


        /// <summary>
        /// Uploads an image for a specific menu item.
        /// </summary>
        /// <param name="itemId">The unique identifier of the menu item</param>
        /// <param name="file">The image file to upload (JPEG, PNG, or WebP format)</param>
        /// <param name="ct">Cancellation token for async operation</param>
        /// <returns>
        /// An <see cref="IActionResult"/> containing the item ID and public image URL.
        /// Returns HTTP 200 (OK) with the image URL on success.
        /// Returns HTTP 400 (Bad Request) if no file is uploaded or the file type is unsupported.
        /// Returns HTTP 404 (Not Found) if the menu item doesn't exist.
        /// Returns HTTP 413 (Payload Too Large) if the file exceeds 5 MB.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during upload.
        /// </returns>
        /// <response code="200">Returns the item ID and public image URL</response>
        /// <response code="400">If no file is uploaded or the file type is unsupported</response>
        /// <response code="404">If the menu item is not found</response>
        /// <response code="413">If the file size exceeds 5 MB</response>
        /// <response code="500">If an internal error occurs while uploading the image</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     POST /api/menuitem/123/image
        ///     Content-Type: multipart/form-data
        ///     
        ///     file: [image file]
        ///
        /// This endpoint requires Admin or Staff role authorization.
        /// Maximum file size: 5 MB
        /// Supported formats: JPEG, PNG, WebP
        /// The image will be saved to the front-end public directory and the URL will be stored in the database.
        /// </remarks>
        [Authorize(Policy = "staffOnly")]
        [HttpPost("{itemId:int}/image")]
        // [RequestSizeLimit(5 * 1024 * 1024)]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UploadItemImage(
            int itemId,
            IFormFile file,
            CancellationToken ct)
        {
            try
            {
                // 1) Fetch item
                var item = await _context.MenuItems
                    .FirstOrDefaultAsync(mi => mi.item_id == itemId, ct);
                if (item is null) return NotFound("Menu item not found.");

                // 2) Basic file checks
                if (file is null || file.Length == 0)
                    return BadRequest("No file uploaded.");

                const long MAX_BYTES = 5 * 1024 * 1024;
                if (file.Length > MAX_BYTES)
                    return StatusCode(StatusCodes.Status413PayloadTooLarge, "File too large.");

                // 3) Get file extension
                var originalFileName = Path.GetFileName(file.FileName);
                var extension = Path.GetExtension(originalFileName);
                if (string.IsNullOrWhiteSpace(extension))
                    return BadRequest("File must have an extension.");

                // 4) Create new filename: ItemName_Date.ext
                var safeItemName = string.Join("_", item.Name.Split(Path.GetInvalidFileNameChars()));
                var dateStamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var newFileName = $"{safeItemName}_{dateStamp}{extension}";

                // 5) Setup public folder path
                var backendRoot = _env.ContentRootPath;
                var publicRoot = Path.GetFullPath(
                    Path.Combine(backendRoot, "..", "front-end", "public")
                );
                var targetFolder = Path.Combine(publicRoot, "menu-items");

                if (!Directory.Exists(targetFolder))
                    Directory.CreateDirectory(targetFolder);

                // 6) Delete old image if it exists
                if (!string.IsNullOrEmpty(item.image_url))
                {
                    var oldImageRelativePath = item.image_url.TrimStart('/');
                    var oldImageFullPath = Path.Combine(publicRoot, oldImageRelativePath);

                    if (System.IO.File.Exists(oldImageFullPath))
                    {
                        try
                        {
                            System.IO.File.Delete(oldImageFullPath);
                            _logger.LogInformation("Deleted old image: {OldImage}", oldImageFullPath);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to delete old image: {OldImage}", oldImageFullPath);
                            // Continue even if deletion fails
                        }
                    }
                }

                // 7) Save uploaded file with new name
                newFileName = newFileName.Replace(' ', '_');
                var filePath = Path.Combine(targetFolder, newFileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream, ct);
                }

                // 8) Assign URL
                var publicUrl = $"/menu-items/{newFileName}";
                item.image_url = publicUrl;

                await _context.SaveChangesAsync(ct);

                _logger.LogInformation("Image uploaded successfully for item {ItemId}: {ImageUrl}", itemId, publicUrl);

                return Ok(new { item_id = itemId, image_url = publicUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error Saving the File selected");
                return StatusCode(500, new { message = "An error occurred while processing your request.", error = ex.Message });
            }
        }


        /// <summary>
        /// Deletes a menu item and its associated image file.
        /// </summary>
        /// <param name="itemId">The unique identifier of the menu item to delete</param>
        /// <returns>
        /// An <see cref="IActionResult"/> indicating the result of the operation.
        /// Returns HTTP 200 (OK) with a success message when the item is deleted.
        /// Returns HTTP 400 (Bad Request) if the item has active menu assignments.
        /// Returns HTTP 404 (Not Found) if the menu item doesn't exist.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during deletion.
        /// </returns>
        /// <response code="200">Returns a success message when the item and image are deleted</response>
        /// <response code="400">If the item has active menu assignments and cannot be deleted</response>
        /// <response code="404">If the menu item is not found</response>
        /// <response code="500">If an internal error occurs while deleting the menu item</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     DELETE /api/menuitem/123
        ///
        /// This endpoint requires Admin or Staff role authorization.
        /// The menu item can only be deleted if it has no active menu assignments.
        /// Both the database record and the associated image file will be deleted.
        /// </remarks>
        [Authorize(Policy = "staffOnly")]
        [HttpDelete("{itemId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteMenuItem(int itemId)
        {
            try
            {
                var item = await _context.MenuItems
                    .FirstOrDefaultAsync(mi => mi.item_id == itemId);

                if (item == null)
                {
                    return NotFound(new { message = "Menu item not found." });
                }

                // Prevent deletion if assigned to any menu.
                var hasAssignments = await _context.Set<MenuItemAssignment>()
                    .AnyAsync(a => a.Item_Id == itemId);

                if (hasAssignments)
                {
                    return Conflict(new
                    {
                        message = "Cannot delete this item because it is assigned to a menu. Remove its menu assignments first."
                    });
                }

                // Remove tag associations.
                var tags = await _context.Set<MenuItemTag>()
                    .Where(t => t.Menu_item_id == itemId)
                    .ToListAsync();

                _context.RemoveRange(tags);

                // Remember image path before deleting the item.
                string? imagePath = null;

                if (!string.IsNullOrWhiteSpace(item.image_url))
                {
                    var fileName = Path.GetFileName(item.image_url);
                    var imageFolder = Path.GetFullPath(
                        Path.Combine(
                            _env.ContentRootPath,
                            "..",
                            "front-end",
                            "public",
                            "menu-items"
                        )
                    );

                    imagePath = Path.Combine(imageFolder, fileName);
                }

                // Delete the database record.
                _context.MenuItems.Remove(item);

                await _context.SaveChangesAsync();

                // Delete image only after successful database deletion.
                if (imagePath != null && System.IO.File.Exists(imagePath))
                {
                    try
                    {
                        System.IO.File.Delete(imagePath);
                    }
                    catch (IOException ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Menu item {ItemId} deleted, but image removal failed.",
                            itemId
                        );
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "No permission to delete image for item {ItemId}.",
                            itemId
                        );
                    }
                }

                return Ok(new
                {
                    message = "Menu item deleted successfully.",
                    item_id = itemId
                });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(
                    ex,
                    "Database error deleting menu item {ItemId}",
                    itemId
                );

                return Conflict(new
                {
                    message = "Cannot delete this item because another database record references it."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error deleting menu item {ItemId}",
                    itemId
                );

                return StatusCode(500, new
                {
                    message = "An error occurred while deleting the menu item."
                });
            }
        }

        /// <summary>
        /// Updates the status of a menu item.
        /// </summary>
        /// <param name="itemId">The unique identifier of the menu item</param>
        /// <param name="menuStatus">The new status to assign to the menu item</param>
        /// <returns>
        /// An <see cref="IActionResult"/> containing the updated <see cref="MenuItemResponseDto"/> object.
        /// Returns HTTP 200 (OK) with the updated menu item on success.
        /// Returns HTTP 404 (Not Found) if the menu item doesn't exist.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during the update.
        /// </returns>
        /// <response code="200">Returns the updated menu item</response>
        /// <response code="404">If the menu item is not found</response>
        /// <response code="500">If an internal error occurs while updating the menu item status</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     PUT /api/menuitem/123/status?menu_status=Unavailable
        ///
        /// This endpoint requires Admin or Staff role authorization.
        /// Valid status values: Available, Unavailable, Discontinued
        /// </remarks>
        [Authorize(Policy = "staffOnly")]
        [HttpPut("{itemId}/status")]
        [ProducesResponseType(typeof(MenuItemResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateItemStatus(
            int itemId,
[FromQuery(Name = "menu_status")]             MenuItemStatus menuStatus
        )
        {
            try
            {
                var item = await _context.MenuItems.Include(mi => mi.MenuItemTags).ThenInclude(mit => mit.Tag).FirstOrDefaultAsync(mi => mi.item_id == itemId);
                if (item is null)
                {
                    return NotFound("menu item was not found");
                }
                item.Status = menuStatus;
                _context.Update(item);
                await _context.SaveChangesAsync();
                return Ok(MenuItemResponseDto.From(item));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error Updating the menu Item Status");
                return StatusCode(500, new { message = "An error occurred while processing your request.", error = ex.Message });
            }
        }



        /// <summary>
        /// Retrieves all tags associated with a specific menu item.
        /// </summary>
        /// <param name="itemId">The unique identifier of the menu item</param>
        /// <returns>
        /// An <see cref="IActionResult"/> containing a collection of <see cref="TagResponseDto"/> objects.
        /// Returns HTTP 200 (OK) with the list of tags sorted alphabetically on success.
        /// Returns HTTP 404 (Not Found) if the menu item doesn't exist.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during retrieval.
        /// </returns>
        /// <response code="200">Returns the list of tags associated with the menu item</response>
        /// <response code="404">If the menu item is not found</response>
        /// <response code="500">If an internal error occurs while retrieving tags</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     GET /api/menuitem/123/tags
        ///
        /// This endpoint requires authentication.
        /// Returns all tags for the specified menu item, sorted alphabetically by tag name.
        /// </remarks>

        [HttpGet("{itemId}/tags")]
        [ProducesResponseType(typeof(IEnumerable<TagResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetItemTags(
            int itemId,
[FromQuery(Name = "menu_status")]             MenuItemStatus menuStatus
        )
        {
            try
            {
                var menuItem = _context.MenuItems.FirstOrDefault(mi => mi.item_id == itemId);
                if (menuItem is null)
                {
                    return NotFound("Menu Item was not found");
                }
                var tags = await _context.Tags.Where(t => t.MenuItemTags.Any(mt => mt.Menu_item_id == itemId)).OrderBy(t => t.tag_name).ToListAsync();
                var response = tags.Select(t => new TagResponseDto
                {
                    Name = t.tag_name,
                    TagId = t.tag_id
                }).ToList();

                return Ok(response);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error Getting Menu Item Tags");
                return StatusCode(500, new { message = "An error occurred while processing your request.", error = ex.Message });
            }
        }




        /// <summary>
        /// Retrieves all tags with their color codes associated with a specific menu item.
        /// </summary>
        /// <param name="itemId">The unique identifier of the menu item</param>
        /// <returns>
        /// An <see cref="IActionResult"/> containing a collection of <see cref="FullTagResponseDto"/> objects.
        /// Returns HTTP 200 (OK) with the list of tags including color codes sorted alphabetically on success.
        /// Returns HTTP 404 (Not Found) if the menu item doesn't exist.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during retrieval.
        /// </returns>
        /// <response code="200">Returns the list of tags with color codes associated with the menu item</response>
        /// <response code="404">If the menu item is not found</response>
        /// <response code="500">If an internal error occurs while retrieving tags</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     GET /api/menuitem/123/tags-with-colors
        ///
        /// This endpoint requires authentication.
        /// Returns all tags for the specified menu item with their color codes, sorted alphabetically by tag name.
        /// </remarks>
        [Authorize(Policy = "staffOnly")]
        [HttpGet("{itemId}/tags-with-colors")]
        [ProducesResponseType(typeof(MenuItemResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AddTagToMenu(
                   int itemId,
[FromQuery(Name = "tag_id")]                    int tagId
               )
        {
            try
            {
                var menuItem = await _context.MenuItems.Include(mi => mi.MenuItemTags).ThenInclude(mit => mit.Tag).FirstOrDefaultAsync(mi => mi.item_id == itemId); ;
                if (menuItem is null)
                {
                    return NotFound("Menu Item was not found");
                }
                var returnedTag = await _context.Tags.FirstOrDefaultAsync(t => t.tag_id == tagId);
                if (returnedTag is null)
                {
                    return NotFound("Item Tag Was not found");
                }
                if (menuItem.MenuItemTags.Any(mt => mt.Tag_id == returnedTag.tag_id))
                {
                    return Conflict("Can not Assign tag to same Menu Item");
                }
                menuItem.MenuItemTags.Add(new MenuItemTag
                {
                    Menu_item_id = menuItem.item_id,
                    Tag_id = returnedTag.tag_id,
                    Tag = returnedTag
                });
                await _context.SaveChangesAsync();
                return Ok(MenuItemResponseDto.From(menuItem));

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error Getting Menu Item Tags with Colors");
                return StatusCode(500, new { message = "An error occurred while processing your request.", error = ex.Message });
            }
        }

        [Authorize(Policy = "staffOnly")]
        [HttpPost("{itemId}/tags/{tagId}")]
        [ProducesResponseType(typeof(MenuItemResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AddTagToMenuItem(
            int itemId,
            int tagId
        )
        {
            try
            {
                var item = await _context.MenuItems.Include(m => m.MenuItemTags).ThenInclude(mit => mit.Tag).FirstOrDefaultAsync(m => m.item_id == itemId);
                if (item is null)
                {
                    return NotFound("Item you were searching for was not found.");
                }
                var tag = await _context.Tags.FirstOrDefaultAsync(t => t.tag_id == tagId);
                if (tag is null)
                {
                    return BadRequest("Error happened in fetching tags");
                }

                // prevent assigning the same tag twice
                if (item.MenuItemTags.Any(mt => mt.Tag_id == tag.tag_id))
                {
                    return Conflict("Tag already assigned to this menu item");
                }

                // add association and save
                item.MenuItemTags.Add(new MenuItemTag
                {
                    Menu_item_id = item.item_id,
                    Tag_id = tag.tag_id,
                    Tag = tag
                });

                await _context.SaveChangesAsync();
                return Ok(MenuItemResponseDto.From(item));

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error Getting Menu Item Tags with Colors");
                return StatusCode(500, new { message = "An error occurred while processing your request.", error = ex.Message });
            }
        }

        /// <summary>
        /// Removes a tag from a menu item.
        /// </summary>
        /// <param name="itemId">The unique identifier of the menu item</param>
        /// <param name="tagId">The unique identifier of the tag to remove</param>
        /// <returns>
        /// An <see cref="IActionResult"/> containing the updated <see cref="MenuItemResponseDto"/> object.
        /// Returns HTTP 200 (OK) with the updated menu item on success.
        /// Returns HTTP 404 (Not Found) if the menu item or tag doesn't exist.
        /// Returns HTTP 409 (Conflict) if the tag is not associated with the menu item.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during removal.
        /// </returns>
        /// <response code="200">Returns the updated menu item after tag removal</response>
        /// <response code="404">If the menu item or tag is not found</response>
        /// <response code="409">If the tag is not associated with the menu item</response>
        /// <response code="500">If an internal error occurs while removing the tag</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     DELETE /api/menuitem/123/tags/456
        ///
        /// This endpoint requires Admin or Staff role authorization.
        /// Removes the association between the specified tag and menu item.
        /// </remarks>
        [Authorize(Policy = "staffOnly")]
        [HttpDelete("{itemId}/tags/{tagId}")]
        [ProducesResponseType(typeof(MenuItemResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> RemoveTagToMenu(
            int itemId,
            int tagId
        )
        {
            try
            {
                var menuItem = await _context.MenuItems.Include(mi => mi.MenuItemTags).ThenInclude(mit => mit.Tag).FirstOrDefaultAsync(mi => mi.item_id == itemId); ;
                if (menuItem is null)
                {
                    return NotFound("Menu Item was not found");
                }
                var returnedTag = await _context.Tags.FirstOrDefaultAsync(t => t.tag_id == tagId);
                if (returnedTag is null)
                {
                    return NotFound("Item Tag Was not found");
                }
                if (!menuItem.MenuItemTags.Any(mt => mt.Tag_id == returnedTag.tag_id))
                {
                    return Conflict("Item does not exist on menu");
                }
                var itemToRemote = menuItem.MenuItemTags.FirstOrDefault(mi => mi.Tag_id == tagId);
                if (itemToRemote is null)
                {
                    return Conflict("Item does not have this tag");
                }
                menuItem.MenuItemTags.Remove(itemToRemote);
                await _context.SaveChangesAsync();
                return Ok(MenuItemResponseDto.From(menuItem));

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error Getting Menu Item Tags with Colors");
                return StatusCode(500, new { message = "An error occurred while processing your request.", error = ex.Message });
            }
        }


    }

}