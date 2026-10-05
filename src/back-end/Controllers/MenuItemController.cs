using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using System.Linq.Expressions;
using back_end.DTO.MenuItems;
using Microsoft.AspNetCore.Authorization;
using back_end.DTO.MenuDTO;
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
        ///     POST /api/menuitem
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
                if (await _context.MenuItems.AnyAsync(mi => mi.Name == item_data.Name))
                {
                    return Conflict("Menu Item already Exist");
                }
                Menu_Item newMenuItem = new Menu_Item
                {
                    Name = item_data.Name,
                    Description = item_data.Description ?? string.Empty,
                    Category_id = item_data.Category_Id,
                    image_url = item_data.Item_Image_Url,
                    Status = MenuItemStatus.Available
                };
                if (item_data.Tag_Ids.Count > 0)
                {
                    var tags = await _context.Tags.Where(t => item_data.Tag_Ids.Contains(t.tag_id)).ToListAsync();
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
                return Ok(new MenuItemResponseDTO
                {
                    Name = newMenuItem.Name,
                    Description = newMenuItem.Description,
                    Category_Id = newMenuItem.Category_id,
                    Item_Image_Url = newMenuItem.image_url,
                    Status = newMenuItem.Status,
                    Item_Id = newMenuItem.item_id,
                    Tags = newMenuItem.MenuItemTags.Select(t => new FullTagResponseDTO
                    {
                        Tag_Id = t.Tag.tag_id,
                        Name = t.Tag.tag_name,
                        Color_Code = t.Tag.tag_color
                    }).ToList(),
                });
            }
            catch (Exception ex)
            {


                _logger.LogError(ex, "Error creating menu item {MenuItemName}", item_data.Name);
                return StatusCode(500, new { message = "An error occurred while creating the menu item", error = ex.Message });
            }
        }

        /// <summary>
        /// Retrieves a specific menu item by its ID.
        /// </summary>
        /// <param name="item_id">The unique identifier of the menu item</param>
        /// <returns>
        /// An <see cref="IActionResult"/> containing the <see cref="Menu_Item"/> object.
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

        [HttpGet("{item_id}")]
        [ProducesResponseType(typeof(Menu_Item), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Get_Menu_Item(
            int item_id
        )
        {
            try
            {
                var item = await _context.MenuItems
                    .Include(mi => mi.Category)  // Include Category details
                    .Include(mi => mi.MenuAssignments)
                        .ThenInclude(ma => ma.Menu)  // Navigate through MenuAssignments to get Menu
                    .Include(mi => mi.MenuItemTags)
                        .ThenInclude(mit => mit.Tag)  // If you also need the Tag details
                    .FirstOrDefaultAsync(mi => mi.item_id == item_id);
                if (item == null)
                {
                    return NotFound(new { message = "Menu item was not found" });
                }
                return Ok(item);
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
        /// An <see cref="IActionResult"/> containing a collection of <see cref="Menu_Item"/> objects.
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
        [ProducesResponseType(typeof(IEnumerable<Menu_Item>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> List_Menu_Items()
        {
            try
            {
                var allItems = await _context.MenuItems
                    .Include(mi => mi.MenuItemTags)
                        .ThenInclude(mit => mit.Tag)
                    .OrderBy(mi => mi.Category_id)
                    .ThenBy(mi => mi.Name)
                    .ToListAsync();

                var result = allItems.Select(item => new MenuItemResponseDTO
                {
                    Item_Id = item.item_id,
                    Name = item.Name,
                    Description = item.Description,
                    Category_Id = item.Category_id,
                    Item_Image_Url = item.image_url,
                    Status = item.Status,

                    Tags = item.MenuItemTags.Select(mit => new FullTagResponseDTO
                    {
                        Tag_Id = mit.Tag.tag_id,
                        Name = mit.Tag.tag_name,
                        Color_Code = mit.Tag.tag_color
                    }).ToList()
                }).ToList();

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
        /// <param name="item_id">The unique identifier of the menu item to update</param>
        /// <param name="menuItemUpdate">The updated menu item data</param>
        /// <returns>
        /// An <see cref="IActionResult"/> containing the updated <see cref="MenuItemResponseDTO"/> object.
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
        ///         "category_Id": 2,
        ///         "item_Image_Url": "https://example.com/images/salmon-new.jpg",
        ///         "status": "Available",
        ///         "tag_Ids": [1, 3, 5, 7]
        ///     }
        ///
        /// This endpoint requires Admin or Staff role authorization.
        /// All fields in the request body are optional - only provided fields will be updated.
        /// Tag IDs will be added to existing tags (not replaced).
        /// </remarks>
        [Authorize(Policy = "staffOnly")]
        [HttpPut("{item_id}")]
        [ProducesResponseType(typeof(MenuItemResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Update_Menu_Item(
            int item_id,
            MenuItemUpdateDTO menuItemUpdate
        )
        {
            try
            {
                var itemUpdate = await _context.MenuItems.FirstOrDefaultAsync(mi => mi.item_id == item_id);
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
                if (menuItemUpdate.Category_Id != null)
                {
                    itemUpdate.Category_id = (int)menuItemUpdate.Category_Id;
                }
                if (menuItemUpdate.Item_Image_Url != null)
                {
                    itemUpdate.image_url = menuItemUpdate.Item_Image_Url;
                }
                if (menuItemUpdate.Status != null)
                {
                    itemUpdate.Status = (MenuItemStatus)menuItemUpdate.Status;
                }
                if (menuItemUpdate != null && menuItemUpdate.Tag_Ids != null && menuItemUpdate.Tag_Ids.Count > 0)
                {
                    await _context.Entry(itemUpdate).Collection(i => i.MenuItemTags).LoadAsync();
                    foreach (var tagId in menuItemUpdate.Tag_Ids.Distinct())
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
                return Ok(new MenuItemResponseDTO
                {
                    Name = itemUpdate.Name,
                    Description = itemUpdate.Description,
                    Category_Id = itemUpdate.Category_id,
                    Item_Image_Url = itemUpdate.image_url,
                    Status = itemUpdate.Status,
                    Item_Id = itemUpdate.item_id,
                    Tags = itemUpdate.MenuItemTags.Select(t => new FullTagResponseDTO
                    {
                        Tag_Id = t.Tag.tag_id,
                        Name = t.Tag.tag_name,
                        Color_Code = t.Tag.tag_color
                    }).ToList()
                });

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
        /// <param name="item_id">The unique identifier of the menu item</param>
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
        [HttpPost("{item_id:int}/image")]
        // [RequestSizeLimit(5 * 1024 * 1024)]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Upload_Item_Image(
            int item_id,
            IFormFile file,
            CancellationToken ct)
        {
            try
            {
                // 1) Fetch item
                var item = await _context.MenuItems
                    .FirstOrDefaultAsync(mi => mi.item_id == item_id, ct);
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

                _logger.LogInformation("Image uploaded successfully for item {ItemId}: {ImageUrl}", item_id, publicUrl);

                return Ok(new { item_id, image_url = publicUrl });
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
        /// <param name="item_id">The unique identifier of the menu item to delete</param>
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
        [HttpDelete("{item_id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Delete_Menu_item(
            int item_id
        )
        {
            try
            {
                var item = await _context.MenuItems.FirstAsync(mi => mi.item_id == item_id);
                if (item is null)
                {
                    return NotFound("Item was Not found");
                }
                if (item.MenuAssignments.Count > 0)
                {
                    return BadRequest("Can not Delete Item if there are Items Ordered Check Orders");
                }
                var backendRoot = _env.ContentRootPath; // .../src/back-end
                var frontEndFolder = Path.GetFullPath(
                    Path.Combine(backendRoot, "..", "front-end", "public", "menu-items")
                );
                if (item.image_url != null)
                {
                    string fileName = Path.GetFileName(item.image_url);
                    var ImagePath = Path.Combine(frontEndFolder, fileName);
                    if (!System.IO.File.Exists(ImagePath))
                    {
                        return BadRequest(ImagePath);
                    }
                    else
                    {
                        System.IO.File.Delete(ImagePath);
                    }
                }

                _context.Remove(item);
                await _context.SaveChangesAsync();
                return Ok("Image Was Deleted");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error Deleting the File selected");
                return StatusCode(500, new { message = "An error occurred while processing your request.", error = ex.Message });
            }

        }



        /// <summary>
        /// Updates the status of a menu item.
        /// </summary>
        /// <param name="item_id">The unique identifier of the menu item</param>
        /// <param name="menu_status">The new status to assign to the menu item</param>
        /// <returns>
        /// An <see cref="IActionResult"/> containing the updated <see cref="Menu_Item"/> object.
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
        ///     PUT /api/menuitem/123
        ///     {
        ///         "menu_status": "Unavailable"
        ///     }
        ///
        /// This endpoint requires Admin or Staff role authorization.
        /// Valid status values: Available, Unavailable, Discontinued
        /// </remarks>
        [Authorize(Policy = "staffOnly")]
        [HttpPut("{item_id}/status")]
        [ProducesResponseType(typeof(Menu_Item), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Update_Item_Status(
            int item_id,
            MenuItemStatus menu_status
        )
        {
            try
            {
                var item = await _context.MenuItems.FirstOrDefaultAsync(mi => mi.item_id == item_id);
                if (item is null)
                {
                    return NotFound("menu item was not found");
                }
                item.Status = menu_status;
                _context.Update(item);
                await _context.SaveChangesAsync();
                return Ok(item);
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
        /// <param name="item_id">The unique identifier of the menu item</param>
        /// <returns>
        /// An <see cref="IActionResult"/> containing a collection of <see cref="TagResponseDTO"/> objects.
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

        [HttpGet("{item_id}/tags")]
        [ProducesResponseType(typeof(IEnumerable<TagResponseDTO>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Get_Item_Tags(
            int item_id,
            MenuItemStatus menu_status
        )
        {
            try
            {
                var menu_item = _context.MenuItems.FirstOrDefault(mi => mi.item_id == item_id);
                if (menu_item is null)
                {
                    return NotFound("Menu Item was not found");
                }
                var tags = await _context.Tags.Where(t => t.MenuItemTags.Any(mt => mt.Menu_item_id == item_id)).OrderBy(t => t.tag_name).ToListAsync();
                var response = tags.Select(t => new TagResponseDTO
                {
                    Name = t.tag_name,
                    Tag_Id = t.tag_id
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
        /// <param name="item_id">The unique identifier of the menu item</param>
        /// <returns>
        /// An <see cref="IActionResult"/> containing a collection of <see cref="FullTagResponseDTO"/> objects.
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
        [HttpGet("{item_id}/tags-with-colors")]
        [ProducesResponseType(typeof(IEnumerable<FullTagResponseDTO>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Add_Tag_To_Menu(
                   int item_id,
                   int tag_id
               )
        {
            try
            {
                var menu_item = await _context.MenuItems.Include(mi => mi.MenuItemTags).FirstOrDefaultAsync(mi => mi.item_id == item_id); ;
                if (menu_item is null)
                {
                    return NotFound("Menu Item was not found");
                }
                var returnedTag = await _context.Tags.FirstOrDefaultAsync(t => t.tag_id == tag_id);
                if (returnedTag is null)
                {
                    return NotFound("Item Tag Was not found");
                }
                if (menu_item.MenuItemTags.Any(mt => mt.Tag_id == returnedTag.tag_id))
                {
                    return Conflict("Can not Assign tag to same Menu Item");
                }
                menu_item.MenuItemTags.Add(new MenuItemTag
                {
                    Menu_item_id = menu_item.item_id,
                    Tag_id = returnedTag.tag_id,
                    Tag = returnedTag
                });
                await _context.SaveChangesAsync();
                return Ok(menu_item);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error Getting Menu Item Tags with Colors");
                return StatusCode(500, new { message = "An error occurred while processing your request.", error = ex.Message });
            }
        }

        [Authorize(Policy = "staffOnly")]
        [HttpPost("{item_id}/tags/{tag_id}")]
        [ProducesResponseType(typeof(Menu_Item), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AddTagToMenuItem(
            int item_id,
            int tag_id
        )
        {
            try
            {
                var item = await _context.MenuItems.Include(m => m.MenuItemTags).ThenInclude(mit => mit.Tag).FirstOrDefaultAsync(m => m.item_id == item_id);
                if (item is null)
                {
                    return NotFound("Item you were searching for was not found.");
                }
                var tag = await _context.Tags.FirstOrDefaultAsync(t => t.tag_id == tag_id);
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
                return Ok(item);

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
        /// <param name="item_id">The unique identifier of the menu item</param>
        /// <param name="tag_id">The unique identifier of the tag to remove</param>
        /// <returns>
        /// An <see cref="IActionResult"/> containing the updated <see cref="Menu_Item"/> object.
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
        [HttpDelete("{item_id}/tags/{tag_id}")]
        [ProducesResponseType(typeof(Menu_Item), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Remove_Tag_To_Menu(
            int item_id,
            int tag_id
        )
        {
            try
            {
                var menu_item = await _context.MenuItems.Include(mi => mi.MenuItemTags).FirstOrDefaultAsync(mi => mi.item_id == item_id); ;
                if (menu_item is null)
                {
                    return NotFound("Menu Item was not found");
                }
                var returnedTag = await _context.Tags.FirstOrDefaultAsync(t => t.tag_id == tag_id);
                if (returnedTag is null)
                {
                    return NotFound("Item Tag Was not found");
                }
                if (!menu_item.MenuItemTags.Any(mt => mt.Tag_id == returnedTag.tag_id))
                {
                    return Conflict("Item does not exist on menu");
                }
                var itemToRemote = menu_item.MenuItemTags.FirstOrDefault(mi => mi.Tag_id == tag_id);
                if (itemToRemote is null)
                {
                    return Conflict("Item does not have this tag");
                }
                menu_item.MenuItemTags.Remove(itemToRemote);
                await _context.SaveChangesAsync();
                return Ok(menu_item);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error Getting Menu Item Tags with Colors");
                return StatusCode(500, new { message = "An error occurred while processing your request.", error = ex.Message });
            }
        }


    }

}