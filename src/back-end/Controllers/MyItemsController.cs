using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace back_end.Controllers
{
    // A signed-in customer's favourites and the items they've ordered before ("Order again").
    [ApiController]
    [Route("api/me")]
    [Authorize(Roles = "Customer")]
    public class MyItemsController : ControllerBase
    {
        // "Order again" lists the most recent items, so a long-time regular's list stays short
        public const int OrderedBeforeLimit = 50;

        private const string GuestMessage = "Sign in to an account to save favourites and order again.";

        private readonly ApplicationDbContext _context;
        private readonly ILogger<MyItemsController> _logger;

        public MyItemsController(ApplicationDbContext context, ILogger<MyItemsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // The signed-in customer's ID, or the response to send back instead
        private bool TryGetCustomerId(out int userId, out IActionResult? denied)
        {
            denied = null;
            if (!int.TryParse(ClaimsHelpers.GetUserId(User), out userId))
            {
                denied = Unauthorized(new { message = "User ID not found in claims." });
            }
            else if (GuestAccounts.IsGuest(User))
            {
                denied = StatusCode(StatusCodes.Status403Forbidden, new { message = GuestMessage });
            }
            return denied == null;
        }

        /// <summary>
        /// The menu item IDs the customer has hearted, newest first.
        /// </summary>
        /// <remarks>
        /// Sample response:
        ///
        ///     GET /api/me/favourites
        ///     [12, 7, 31]
        /// </remarks>
        [HttpGet("favourites")]
        [ProducesResponseType(typeof(List<int>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetFavourites()
        {
            if (!TryGetCustomerId(out var userId, out var denied)) return denied!;

            var itemIds = await _context.FavouriteItems
                .AsNoTracking()
                .Where(f => f.User_Id == userId)
                .OrderByDescending(f => f.Created_At)
                .Select(f => f.Item_Id)
                .ToListAsync();

            return Ok(itemIds);
        }

        /// <summary>
        /// Hearts a menu item. Hearting one that's already a favourite does nothing.
        /// </summary>
        [HttpPut("favourites/{itemId:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AddFavourite(int itemId)
        {
            if (!TryGetCustomerId(out var userId, out var denied)) return denied!;

            try
            {
                if (!await _context.MenuItems.AnyAsync(i => i.item_id == itemId))
                {
                    return NotFound(new { message = $"Menu item {itemId} was not found." });
                }

                if (!await _context.FavouriteItems.AnyAsync(f => f.User_Id == userId && f.Item_Id == itemId))
                {
                    _context.FavouriteItems.Add(new FavouriteItem
                    {
                        User_Id = userId,
                        Item_Id = itemId,
                        Created_At = DateTime.UtcNow
                    });
                    await _context.SaveChangesAsync();
                }

                return NoContent();
            }
            catch (DbUpdateException)
            {
                // A double tap can save the same favourite twice at once; the first one counts
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding favourite item {ItemId} for user {UserId}", itemId, userId);
                return StatusCode(500, new { message = "An error occurred while saving the favourite." });
            }
        }

        /// <summary>
        /// Un-hearts a menu item. Removing one that isn't a favourite does nothing.
        /// </summary>
        [HttpDelete("favourites/{itemId:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> RemoveFavourite(int itemId)
        {
            if (!TryGetCustomerId(out var userId, out var denied)) return denied!;

            try
            {
                var favourite = await _context.FavouriteItems
                    .FirstOrDefaultAsync(f => f.User_Id == userId && f.Item_Id == itemId);

                if (favourite != null)
                {
                    _context.FavouriteItems.Remove(favourite);
                    await _context.SaveChangesAsync();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing favourite item {ItemId} for user {UserId}", itemId, userId);
                return StatusCode(500, new { message = "An error occurred while removing the favourite." });
            }
        }

        /// <summary>
        /// The menu item IDs the customer has ordered on any visit, most recently ordered first.
        /// </summary>
        /// <remarks>
        /// Cancelled orders and cancelled items don't count. At most 50 items are returned.
        ///
        ///     GET /api/me/ordered-before
        ///     [7, 12]
        /// </remarks>
        [HttpGet("ordered-before")]
        [ProducesResponseType(typeof(List<int>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetOrderedBefore()
        {
            if (!TryGetCustomerId(out var userId, out var denied)) return denied!;

            var itemIds = await _context.OrderItems
                .AsNoTracking()
                .Where(oi => oi.SessionOrder.User_Id == userId
                        && oi.SessionOrder.Status != OrderStatus.Cancelled
                        && oi.Order_Item_Status != OrderStatus.Cancelled)
                .GroupBy(oi => oi.Item_Id)
                .Select(g => new { ItemId = g.Key, LastOrdered = g.Max(oi => oi.SessionOrder.Created_At) })
                .OrderByDescending(x => x.LastOrdered)
                .Take(OrderedBeforeLimit)
                .Select(x => x.ItemId)
                .ToListAsync();

            return Ok(itemIds);
        }
    }
}
