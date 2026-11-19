using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using Microsoft.AspNetCore.Authorization;

namespace back_end.Controllers
{
    [ApiController]
    [Route("api/[controller]")]

    public class MenuLocationController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MenuLocationController> _logger;
        private readonly IWebHostEnvironment _env;

        public MenuLocationController
        (ApplicationDbContext context,
            ILogger<MenuLocationController> logger,
            IWebHostEnvironment env)
        {
            _context = context;
            _logger = logger;
            _env = env;
        }

        /// <summary>
        /// Offers a menu at a location.
        /// </summary>
        /// <remarks>
        /// Sample request:
        ///
        ///     PUT /api/menulocation/menu/3/location/1
        ///
        /// Safe to repeat: if the menu is already offered there, nothing changes and it still returns 200.
        /// </remarks>
        /// <response code="200">The menu is offered at the location.</response>
        /// <response code="400">menuId or locationId isn't a positive number.</response>
        /// <response code="401">Not signed in.</response>
        /// <response code="403">Not an admin.</response>
        /// <response code="404">The menu or location doesn't exist.</response>
        /// <response code="500">An unexpected error occurred.</response>
        [Authorize(Policy = "adminOnly")]
        [HttpPut("menu/{menuId}/location/{locationId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AddLocationToMenu(
            [FromRoute] int menuId = -1,
            [FromRoute] int locationId = -1
        )
        {
            try
            {
                if (menuId <= 0 || locationId <= 0)
                {
                    return BadRequest("menu_id and location_id must be positive integers.");
                }
                if (!_context.Menus.Any(m => menuId == m.Menu_id))
                {
                    return NotFound("The menu location was not found");
                }
                if (!_context.Locations.Any(l => l.Location_Id == locationId))
                {
                    return NotFound("The location was not found.");
                }
                var menuLocation = await _context.MenuLocations.FirstOrDefaultAsync(ml =>
                    ml.Menu_Id == menuId && ml.Location_Id == locationId
                );
                if (menuLocation is null)
                {
                    menuLocation = new MenuLocations
                    {
                        Menu_Id = menuId,
                        Location_Id = locationId
                    };
                    _context.MenuLocations.Add(menuLocation);
                    await _context.SaveChangesAsync();
                }
                return Ok();
            }
            catch (Exception error)
            {
                _logger.LogError(error, "Can not Assign a menu location");
                return StatusCode(500, "Internal Server Error");
            }
        }

        /// <summary>
        /// Stops offering a menu at a location.
        /// </summary>
        /// <remarks>
        /// Sample request:
        ///
        ///     DELETE /api/menulocation/menu/3/location/1
        ///
        /// Refused while that location has Pending or Processing order items on this menu.
        /// Open orders at other locations don't block it. The menu itself isn't deleted.
        /// </remarks>
        /// <response code="200">The menu is no longer offered at the location.</response>
        /// <response code="400">An ID isn't a positive number, or the location has open orders on this menu.</response>
        /// <response code="401">Not signed in.</response>
        /// <response code="403">Not an admin.</response>
        /// <response code="404">The menu isn't offered at that location.</response>
        /// <response code="500">An unexpected error occurred.</response>
        [Authorize(Policy = "adminOnly")]
        [HttpDelete("menu/{menuId}/location/{locationId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteLocationToMenu(
        [FromRoute] int menuId,
        [FromRoute] int locationId
        )
        {
            try
            {
                if (menuId < 1)
                {
                    return BadRequest("No menu location");
                }
                if (locationId < 1)
                {
                    return BadRequest("Menu location is Missing");
                }

                var menuLocation = await _context.MenuLocations.FirstOrDefaultAsync(ml =>
                    ml.Menu_Id == menuId && ml.Location_Id == locationId
                );
                if (menuLocation is null)
                {
                    return NotFound("Menu and Menu Location were not found");
                }
                ;
                // Only orders on this menu at this location; another location's open orders don't block it
                var hasOpenOrders = await _context.OrderItems.AnyAsync(oi =>
                    oi.Menu_Id == menuId
                    && oi.SessionOrder.DiningSession.Location_Id == locationId
                    && (
                        oi.Order_Item_Status == OrderStatus.Pending
                        || oi.Order_Item_Status == OrderStatus.Processing
                    )
                );
                if (hasOpenOrders)
                {
                    return BadRequest(
                        "Could not process request, there are open orders on this menu location."
                    );
                }
                _context.MenuLocations.Remove(menuLocation);
                await _context.SaveChangesAsync();
                return Ok("Menu location was Removed");
            }
            catch (Exception error)
            {
                _logger.LogError(error, "Can not remove a menu location");
                return StatusCode(500, "Internal Server Error");
            }
        }
    }
}
