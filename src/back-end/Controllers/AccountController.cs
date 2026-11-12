using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.Dtos;
using back_end.Helpers;
using back_end.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using BCryptNet = BCrypt.Net.BCrypt;

namespace back_end.Controllers
{
    // The signed-in user's own account: their name and password. Works for customers, staff and
    // admins, except that staff can't change their name (an admin does that in Manage Staff).
    // Guests get 403, since their throwaway account is lost when the visit ends.
    // Changing the email isn't offered, since the new address would need verifying again.
    [ApiController]
    [Route("api/me/account")]
    [Authorize]
    public class AccountController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthTokenService _tokenService;

        public AccountController(ApplicationDbContext context, IAuthTokenService tokenService)
        {
            _context = context;
            _tokenService = tokenService;
        }

        /// <summary>
        /// The signed-in user's account.
        /// </summary>
        /// <remarks>
        /// Sample response:
        ///
        ///     GET /api/me/account
        ///     {
        ///       "email": "sam@example.com",
        ///       "first_name": "Sam",
        ///       "last_name": "Tanaka",
        ///       "role": "Customer",
        ///       "has_password": true,
        ///       "can_change_name": true
        ///     }
        ///
        /// has_password is false for accounts that only sign in with Google.
        /// can_change_name is false for staff.
        /// </remarks>
        [HttpGet]
        [ProducesResponseType(typeof(AccountDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetAccount()
        {
            var (user, error) = await FindSignedInUser();
            if (error != null) return error;

            return Ok(AccountDto.From(user!));
        }

        /// <summary>
        /// Changes the signed-in user's first and last name.
        /// </summary>
        /// <remarks>
        /// Sample request:
        ///
        ///     PUT /api/me/account
        ///     { "first_name": "Sam", "last_name": "Tanaka" }
        ///
        /// Both names are required, trimmed, and at most 100 characters. Returns the updated account.
        /// Staff get 403: an admin changes their name in Manage Staff.
        /// </remarks>
        [HttpPut]
        [ProducesResponseType(typeof(AccountDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateAccount([FromBody] UpdateAccountDto request)
        {
            var (user, error) = await FindSignedInUser();
            if (error != null) return error;

            if (!AccountDto.CanChangeNameOf(user!))
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { message = "Ask an admin to change your name." });
            }

            var firstName = request.FirstName.Trim();
            var lastName = request.LastName.Trim();
            if (firstName.Length == 0 || lastName.Length == 0)
            {
                return BadRequest(new { message = "First and last name are required" });
            }

            user!.First_name = firstName;
            user.Last_name = lastName;
            user.Last_Interaction_at = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(AccountDto.From(user));
        }

        /// <summary>
        /// Changes the signed-in user's password, after checking their current one.
        /// </summary>
        /// <remarks>
        /// Sample request:
        ///
        ///     POST /api/me/account/change-password
        ///     { "current_password": "OldPassword1!", "new_password": "NewPassword1!" }
        ///
        /// A wrong current password is a 400, not a 401, so the front end doesn't sign the user out.
        /// Any password reset link already emailed stops working. Rate limited like login, since
        /// the current password can be guessed here.
        /// </remarks>
        [HttpPost("change-password")]
        [EnableRateLimiting(AuthRateLimits.SignInPolicy)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto request)
        {
            var (user, error) = await FindSignedInUser();
            if (error != null) return error;

            if (string.IsNullOrEmpty(user!.Password_hash))
            {
                return BadRequest(new { message = "This account signs in with Google, so it has no password to change" });
            }
            if (!BCryptNet.Verify(request.CurrentPassword, user.Password_hash))
            {
                return BadRequest(new { message = "Current password is incorrect" });
            }

            user.Password_hash = BCryptNet.HashPassword(request.NewPassword);
            user.Last_Interaction_at = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // A reset link sent before the change would let someone set the password again
            await _tokenService.RevokeAllAsync(user.User_id);

            return NoContent();
        }

        // The signed-in user, or the response to send instead
        private async Task<(User? User, IActionResult? Error)> FindSignedInUser()
        {
            if (!int.TryParse(ClaimsHelpers.GetUserId(User), out var userId))
            {
                return (null, Unauthorized(new { message = "User ID not found in claims." }));
            }
            if (GuestAccounts.IsGuest(User))
            {
                return (null, StatusCode(StatusCodes.Status403Forbidden,
                    new { message = "Sign in to an account to change your details." }));
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.User_id == userId);
            return user == null
                ? (null, NotFound(new { message = "Account not found." }))
                : (user, null);
        }
    }
}
