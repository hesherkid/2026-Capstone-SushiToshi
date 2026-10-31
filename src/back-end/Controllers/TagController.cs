using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.DTO.TagDTOs;
using Swashbuckle.AspNetCore.Annotations;
using back_end.Helpers;

namespace back_end.Controllers
{
  [ApiController]
  [Route("api/[controller]")]
  [Authorize]
  public class TagController : Controller
  {
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TagController> _logger;

    public TagController(ApplicationDbContext context, ILogger<TagController> logger)
    {
      _context = context;
      _logger = logger;
    }

    /// <summary>
    /// Creates a new tag.
    /// </summary>
    /// <param name="tagData">The tag payload containing the name and optional color.</param>
    /// <remarks>
    /// Requires <c>Admin</c> role.  
    /// Returns <c>201 Created</c> with the created tag and a <c>Location</c> header that points to <see cref="GetTagById(int)"/>.
    /// Duplicate tag names are rejected.
    /// </remarks>
    /// <response code="201">Tag created successfully.</response>
    /// <response code="400">The request body is invalid or missing required fields.</response>
    /// <response code="403">The user does not have permission to create tags.</response>
    /// <response code="409">A tag with the same name already exists.</response>
    /// <response code="500">An unexpected error occurred while creating the tag.</response>
    [Authorize(Policy = "adminOnly")]
    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [SwaggerOperation(
        OperationId = "CreateTag",
        Summary = "Create a tag",
        Description = "Creates a new tag with a name and optional color. Requires Admin role."
    )]
    [ProducesResponseType(typeof(TagBaseDTO), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateTag([FromBody] TagBaseDTO tagData)
    {
      try
      {
        // Check if tag already exists
        var existingTag = await _context.Tags
          .FirstOrDefaultAsync(t => t.tag_name == tagData.Tag_Name);

        if (existingTag != null)
        {
          return Conflict(new { detail = "Tag already exists" });
        }

        var tag = new Tag
        {
          tag_name = tagData.Tag_Name,
          tag_color = tagData.Tag_Color
        };

        _context.Tags.Add(tag);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetTagById), new { tag_id = tag.tag_id }, new TagBaseDTO
        {
          Tag_Id = tag.tag_id,
          Tag_Name = tag.tag_name,
          Tag_Color = tag.tag_color
        });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error creating tag {@TagData}", tagData);
        return StatusCode(500, new { detail = "Error creating tag" });
      }
    }



    /// <summary>
    /// Retrieves all tags.
    /// </summary>
    /// <remarks>
    /// Returns every tag with its ID, name, and color, ordered by ID.
    /// Requires authentication.
    /// </remarks>
    /// <response code="200">The list of tags was returned successfully.</response>
    /// <response code="403">The user is not authorized to access this resource.</response>
    /// <response code="500">An internal server error occurred while retrieving tags.</response>
    [HttpGet]
    [Produces("application/json")]
    [SwaggerOperation(
        OperationId = "GetTags",
        Summary = "Retrieve all tags",
        Description = "Returns every tag with its ID, name, and color. Requires authentication."
    )]
    [ProducesResponseType(typeof(IEnumerable<TagBaseDTO>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTags()
    {
      try
      {
        var tags = await _context.Tags
          .AsNoTracking()
          .OrderBy(t => t.tag_id)
          .Select(t => new TagBaseDTO
          {
            Tag_Id = t.tag_id,
            Tag_Name = t.tag_name,
            Tag_Color = t.tag_color
          })
          .ToListAsync();

        return Ok(tags);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error retrieving tags");
        return StatusCode(500, new { detail = "Error retrieving tags" });
      }
    }

    /// <summary>
    /// Retrieves a tag by its unique identifier.
    /// </summary>
    /// <param name="tag_id">The unique identifier of the tag to retrieve.</param>
    /// <remarks>
    /// This endpoint returns a single tag with its ID, name, and color.
    /// Authorization is required.  
    /// If the tag does not exist, a <c>404 Not Found</c> response is returned.
    /// </remarks>
    /// <response code="200">The tag was found and returned successfully.</response>
    /// <response code="403">The user is not authorized to access this resource.</response>
    /// <response code="404">No tag exists with the specified <paramref name="tag_id"/>.</response>
    /// <response code="500">An internal server error occurred while retrieving the tag.</response>

    [HttpGet("{tag_id:int}")]
    [Produces("application/json")]
    [SwaggerOperation(
        OperationId = "GetTagById",
        Summary = "Retrieve a tag by ID",
        Description = "Fetches a single tag's details (ID, name, color) by its unique identifier. Requires authentication."
    )]
    [ProducesResponseType(typeof(TagBaseDTO), StatusCodes.Status200OK)]
    [SwaggerResponse(StatusCodes.Status200OK, "Tag retrieved successfully", typeof(TagBaseDTO))]
    public async Task<IActionResult> GetTagById(int tag_id)
    {
      try
      {
        var tag = await _context.Tags
          .FirstOrDefaultAsync(t => t.tag_id == tag_id);

        if (tag == null)
        {
          return NotFound(new { detail = "Tag not found" });
        }

        return Ok(new TagBaseDTO
        {
          Tag_Id = tag.tag_id,
          Tag_Name = tag.tag_name,
          Tag_Color = tag.tag_color
        });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error retrieving tag with ID {TagId}", tag_id);
        return StatusCode(500, new { detail = $"Error retrieving tag {tag_id}" });
      }
    }


    /// <summary>
    /// Updates an existing tag.
    /// </summary>
    /// <param name="tag_id">The unique identifier of the tag to update.</param>
    /// <param name="tagData">The fields to update for the tag.</param>
    /// <remarks>
    /// Requires <c>Admin</c> role.  
    /// Only non-null fields in <see cref="TagUpdateDTO"/> are applied.  
    /// Note: Although this is a <c>PUT</c>, this endpoint performs a partial update for convenience.
    /// </remarks>
    /// <response code="200">The tag was updated successfully and the updated tag is returned.</response>
    /// <response code="400">The request body is invalid, or no updatable fields were provided.</response>
    /// <response code="403">The user does not have permission to update tags.</response>
    /// <response code="404">A tag with the specified <paramref name="tag_id"/> does not exist.</response>
    /// <response code="409">Another tag already uses the new name.</response>
    /// <response code="500">An unexpected error occurred while updating the tag.</response>
    [Authorize(Policy = "adminOnly")]
    [HttpPut("{tag_id:int}")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [SwaggerOperation(
        OperationId = "UpdateTag",
        Summary = "Update a tag",
        Description = "Updates the name and/or color of a tag by ID. Requires Admin role."
    )]
    [ProducesResponseType(typeof(TagBaseDTO), StatusCodes.Status200OK)]
    [SwaggerResponse(StatusCodes.Status200OK, "Tag updated", typeof(TagBaseDTO))]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateTag(int tag_id, [FromBody] TagUpdateDTO tagData)
    {
      try
      {
        var tag = await _context.Tags
          .FirstOrDefaultAsync(t => t.tag_id == tag_id);

        if (tag is null)
        {
          return NotFound(new { detail = "Tag not found" });
        }

        // Another tag may not share the new name (renaming a tag to its own name is fine)
        if (tagData.Tag_Name != null)
        {
          var nameTaken = await _context.Tags
            .AnyAsync(t => t.tag_name == tagData.Tag_Name && t.tag_id != tag_id);

          if (nameTaken)
          {
            return Conflict(new { detail = "Tag already exists" });
          }
        }

        // Update only provided fields
        if (tagData.Tag_Name != null)
        {
          tag.tag_name = tagData.Tag_Name;
        }
        if (tagData.Tag_Color != null)
        {
          tag.tag_color = tagData.Tag_Color;
        }

        await _context.SaveChangesAsync();

        return Ok(new TagBaseDTO
        {
          Tag_Id = tag.tag_id,
          Tag_Name = tag.tag_name,
          Tag_Color = tag.tag_color
        });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error updating tag {TagId}", tag_id);
        return StatusCode(500, new { detail = $"Error updating tag {tag_id}" });
      }
    }

    /// <summary>
    /// Retrieves all menu items associated with a specific tag.
    /// </summary>
    /// <param name="tag_id">The unique identifier of the tag.</param>
    /// <remarks>
    /// This endpoint returns a list of all menu items linked to the specified tag.  
    /// Only users with the <c>Admin</c> role are permitted to access this resource.
    /// </remarks>
    /// <response code="200">Returns the list of menu items associated with the tag.</response>
    /// <response code="403">The user does not have permission to access this endpoint.</response>
    /// <response code="404">The specified tag was not found.</response>
    /// <response code="500">An unexpected server error occurred while retrieving data.</response>
    [Authorize(Policy = "adminOnly")]
    [HttpGet("{tag_id:int}/menu_items")]
    [Produces("application/json")]

    [SwaggerOperation(
        OperationId = "GetMenuItemsByTag",
        Summary = "Get menu items linked to a tag",
        Description = "Returns all menu items associated with the given tag. Requires Admin role."
    )]
    [SwaggerResponse(StatusCodes.Status200OK, "List of menu items returned successfully", typeof(IEnumerable<Menu_Item>))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Permission denied", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Tag not found", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "Server error", typeof(ProblemDetails))]
    public async Task<IActionResult> GetMenuItemsByTag(int tag_id)
    {
      try
      {
        var tag = await _context.Tags
          .Include(t => t.MenuItemTags)
            .ThenInclude(mit => mit.MenuItem)
          .FirstOrDefaultAsync(t => t.tag_id == tag_id);

        if (tag is null)
        {
          return NotFound(new { detail = "Tag not found" });
        }

        var menuItems = tag.MenuItemTags.Select(mit => mit.MenuItem).ToList();

        return Ok(menuItems);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error retrieving menu items by tag {TagId}", tag_id);
        return StatusCode(500, new { detail = "Error retrieving menu items" });
      }
    }
    /// <summary>
    /// Deletes a tag by its identifier.
    /// </summary>
    /// <param name="tag_id">The unique identifier of the tag to delete.</param>
    /// <remarks>
    /// Requires the user to be in the <c>Admin</c> role.  
    /// This operation clears all related <c>MenuItemTags</c> associations before removing the tag.
    /// </remarks>
    /// <response code="200">Tag was deleted successfully.</response>
    /// <response code="403">The current user does not have permission to delete tags.</response>
    /// <response code="404">No tag exists with the supplied <paramref name="tag_id"/>.</response>
    /// <response code="500">An unexpected error occurred while deleting the tag.</response>
    [Authorize(Policy = "adminOnly")]
    [HttpDelete("{tag_id:int}")]
    [Produces("application/json")]
    [SwaggerOperation(
        Summary = "Delete a tag",              // Short summary (shows in list)
        Description = "Deletes a tag by ID and removes all related MenuItemTags. Requires Admin role.", // Full description
        OperationId = "DeleteTag"              // Internal name in Swagger/OpenAPI
    )]
    public async Task<IActionResult> DeleteTag(int tag_id)
    {
      try
      {
        var tag = await _context.Tags
          .Include(t => t.MenuItemTags)
          .FirstOrDefaultAsync(t => t.tag_id == tag_id);

        if (tag == null)
        {
          return NotFound(new { detail = "Tag not found" });
        }

        // Clear menu item tags association
        tag.MenuItemTags.Clear();
        _context.Tags.Remove(tag);
        await _context.SaveChangesAsync();

        return Ok(new { detail = "Tag deleted" });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error deleting tag");
        return StatusCode(500, new { detail = "Error deleting tag" });
      }
    }
  }
}