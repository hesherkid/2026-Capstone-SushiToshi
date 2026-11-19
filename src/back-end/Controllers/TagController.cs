using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.Dtos;
using Swashbuckle.AspNetCore.Annotations;
using back_end.Helpers;

namespace back_end.Controllers
{
  [ApiController]
  [Route("api/[controller]")]
  [Authorize]
  public class TagController : ControllerBase
  {
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TagController> _logger;

    public TagController(ApplicationDbContext context, ILogger<TagController> logger)
    {
      _context = context;
      _logger = logger;
    }

    /// <summary>
    /// Creates a tag (e.g. "Spicy", "Vegetarian") for labelling menu items.
    /// </summary>
    /// <param name="tagData">The tag's name and colour</param>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/tag
    ///     { "tag_name": "Spicy", "tag_color": "#FF5733" }
    ///
    /// - tag_name is required, up to 100 characters, and must be unused.
    /// - tag_color is required, as a #RRGGBB hex colour.
    /// - tag_id in the body is ignored.
    /// </remarks>
    /// <response code="201">The new tag; the Location header points to GET /api/tag/{tagId}.</response>
    /// <response code="400">tag_name or tag_color is missing or invalid.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Not an admin.</response>
    /// <response code="409">A tag with that name already exists.</response>
    /// <response code="500">The tag couldn't be saved.</response>
    [Authorize(Policy = "adminOnly")]
    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(TagBaseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateTag([FromBody] TagBaseDto tagData)
    {
      try
      {
        // Check if tag already exists
        var existingTag = await _context.Tags
          .FirstOrDefaultAsync(t => t.tag_name == tagData.TagName);

        if (existingTag != null)
        {
          return Conflict(new { detail = "Tag already exists" });
        }

        var tag = new Tag
        {
          tag_name = tagData.TagName,
          tag_color = tagData.TagColor
        };

        _context.Tags.Add(tag);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetTagById), new { tagId = tag.tag_id }, new TagBaseDto
        {
          TagId = tag.tag_id,
          TagName = tag.tag_name,
          TagColor = tag.tag_color
        });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error creating tag {@TagData}", tagData);
        return StatusCode(500, new { detail = "Error creating tag" });
      }
    }



    /// <summary>
    /// Every tag, by ID.
    /// </summary>
    /// <remarks>
    /// Sample request:
    ///
    ///     GET /api/tag
    /// </remarks>
    /// <response code="200">The tags with their IDs, names and colours (can be empty).</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="500">The tags couldn't be loaded.</response>
    [HttpGet]
    [Produces("application/json")]
    [ProducesResponseType(typeof(IEnumerable<TagBaseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetTags()
    {
      try
      {
        var tags = await _context.Tags
          .AsNoTracking()
          .OrderBy(t => t.tag_id)
          .Select(t => new TagBaseDto
          {
            TagId = t.tag_id,
            TagName = t.tag_name,
            TagColor = t.tag_color
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
    /// <param name="tagId">The tag</param>
    /// <response code="200">The tag's ID, name and colour.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="404">The tag doesn't exist.</response>
    /// <response code="500">The tag couldn't be loaded.</response>
    [HttpGet("{tagId:int}")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(TagBaseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetTagById(int tagId)
    {
      try
      {
        var tag = await _context.Tags
          .FirstOrDefaultAsync(t => t.tag_id == tagId);

        if (tag == null)
        {
          return NotFound(new { detail = "Tag not found" });
        }

        return Ok(new TagBaseDto
        {
          TagId = tag.tag_id,
          TagName = tag.tag_name,
          TagColor = tag.tag_color
        });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error retrieving tag with ID {TagId}", tagId);
        return StatusCode(500, new { detail = $"Error retrieving tag {tagId}" });
      }
    }


    /// <summary>
    /// Updates an existing tag.
    /// </summary>
    /// <param name="tagId">The unique identifier of the tag to update.</param>
    /// <param name="tagData">The fields to update for the tag.</param>
    /// <remarks>
    /// Sample request:
    ///
    ///     PUT /api/tag/3
    ///     { "tag_color": "#2E7D32" }
    ///
    /// - Only the fields sent are changed; an empty body changes nothing.
    /// - A new tag_name must be unused (keeping its own name is fine), up to 100 characters.
    /// - tag_color must be a #RRGGBB hex colour.
    /// </remarks>
    /// <response code="200">The updated tag.</response>
    /// <response code="400">tag_name is too long, or tag_color isn't a #RRGGBB colour.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Not an admin.</response>
    /// <response code="404">The tag doesn't exist.</response>
    /// <response code="409">Another tag already uses that name.</response>
    /// <response code="500">The tag couldn't be saved.</response>
    [Authorize(Policy = "adminOnly")]
    [HttpPut("{tagId:int}")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(TagBaseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateTag(int tagId, [FromBody] TagUpdateDto tagData)
    {
      try
      {
        var tag = await _context.Tags
          .FirstOrDefaultAsync(t => t.tag_id == tagId);

        if (tag is null)
        {
          return NotFound(new { detail = "Tag not found" });
        }

        // Another tag may not share the new name (renaming a tag to its own name is fine)
        if (tagData.TagName != null)
        {
          var nameTaken = await _context.Tags
            .AnyAsync(t => t.tag_name == tagData.TagName && t.tag_id != tagId);

          if (nameTaken)
          {
            return Conflict(new { detail = "Tag already exists" });
          }
        }

        // Update only provided fields
        if (tagData.TagName != null)
        {
          tag.tag_name = tagData.TagName;
        }
        if (tagData.TagColor != null)
        {
          tag.tag_color = tagData.TagColor;
        }

        await _context.SaveChangesAsync();

        return Ok(new TagBaseDto
        {
          TagId = tag.tag_id,
          TagName = tag.tag_name,
          TagColor = tag.tag_color
        });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error updating tag {TagId}", tagId);
        return StatusCode(500, new { detail = $"Error updating tag {tagId}" });
      }
    }

    /// <summary>
    /// Retrieves all menu items associated with a specific tag.
    /// </summary>
    /// <param name="tagId">The unique identifier of the tag.</param>
    /// <remarks>
    /// Sample request:
    ///
    ///     GET /api/tag/3/menu-items
    ///
    /// Each item includes all of its tags, not only this one.
    /// </remarks>
    /// <response code="200">The menu items (can be empty).</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Not an admin.</response>
    /// <response code="404">The tag doesn't exist.</response>
    /// <response code="500">The menu items couldn't be loaded.</response>
    [Authorize(Policy = "adminOnly")]
    [HttpGet("{tagId:int}/menu-items")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(IEnumerable<MenuItemResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetMenuItemsByTag(int tagId)
    {
      try
      {
        var tag = await _context.Tags
          .Include(t => t.MenuItemTags)
            .ThenInclude(mit => mit.MenuItem)
              .ThenInclude(mi => mi.MenuItemTags)
                .ThenInclude(itemTag => itemTag.Tag)
          .FirstOrDefaultAsync(t => t.tag_id == tagId);

        if (tag is null)
        {
          return NotFound(new { detail = "Tag not found" });
        }

        var menuItems = tag.MenuItemTags.Select(mit => MenuItemResponseDto.From(mit.MenuItem)).ToList();

        return Ok(menuItems);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error retrieving menu items by tag {TagId}", tagId);
        return StatusCode(500, new { detail = "Error retrieving menu items" });
      }
    }
    /// <summary>
    /// Deletes a tag by its identifier.
    /// <param name="tagId">The tag</param>
    /// <remarks>
    /// Sample request:
    ///
    ///     DELETE /api/tag/3
    ///
    /// The tag comes off every menu item that had it; the menu items themselves stay.
    /// </remarks>
    /// <response code="200">The tag was deleted.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Not an admin.</response>
    /// <response code="404">The tag doesn't exist.</response>
    /// <response code="500">The tag couldn't be deleted.</response>
    [Authorize(Policy = "adminOnly")]
    [HttpDelete("{tagId:int}")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteTag(int tagId)
    {
      try
      {
        var tag = await _context.Tags
          .Include(t => t.MenuItemTags)
          .FirstOrDefaultAsync(t => t.tag_id == tagId);

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