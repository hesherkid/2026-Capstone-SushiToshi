using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using back_end.Dtos;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.Helpers;

namespace back_end.Controllers
{
    // Admin management of customer accounts. Customers sign themselves up, so there's no create.
    [ApiController]
    [Route("api/customers")]
    [Authorize(Roles = "Admin")]
    public class CustomerController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CustomerController> _logger;

        public CustomerController(ApplicationDbContext context, ILogger<CustomerController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: /api/customers?skip=0&limit=100
        /// <summary>Lists customer accounts, newest first.</summary>
        /// <param name="skip">How many to skip (0 or more).</param>
        /// <param name="limit">How many to return (1-100, default 100).</param>
        /// <remarks>
        /// Deleted accounts and "Continue as Guest" accounts are left out; Active, Inactive and
        /// Suspended accounts are included. To get everyone, keep calling with skip += 100 until a page has
        /// fewer than 100.
        /// </remarks>
        /// <response code="200">The page of customers.</response>
        /// <response code="400">skip or limit is out of range.</response>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserResponseDto>>> ListCustomers(
            [FromQuery] int skip = 0,
            [FromQuery] int limit = 100)
        {
            if (skip < 0) return BadRequest("skip must be >= 0");
            if (limit < 1 || limit > 100) return BadRequest("limit must be between 1 and 100");

            var users = await _context.Users.AsNoTracking()
                .Where(u => u.Role == UserRoles.Customer && u.Status != UserStatus.Deleted)
                // Throwaway "Continue as Guest" accounts aren't real customers
                .Where(u => !u.Email.EndsWith("@" + GuestAccounts.EmailDomain))
                .OrderByDescending(u => u.Created_at)
                .Skip(skip)
                .Take(limit)
                .Select(u => new UserResponseDto
                {
                    UserId = u.User_id,
                    Email = u.Email,
                    FirstName = u.First_name,
                    LastName = u.Last_name,
                    Role = u.Role,
                    Status = u.Status,
                    CreatedAt = u.Created_at,
                    LastInteractionAt = u.Last_Interaction_at,
                    IsEmailConfirmed = u.Is_email_confirmed
                })
                .ToListAsync();

            return Ok(users);
        }

        // PUT: /api/customers/5/status
        /// <summary>Deactivates or reactivates a customer account.</summary>
        /// <remarks>
        /// Status must be Active or Inactive. Inactive accounts can't log in (email or Google),
        /// but a login token they already have keeps working until it expires.
        /// </remarks>
        /// <response code="200">The updated account.</response>
        /// <response code="400">Status isn't Active or Inactive.</response>
        /// <response code="404">No customer with that ID (staff, admins and deleted accounts don't count).</response>
        [HttpPut("{userId:int}/status")]
        public async Task<ActionResult<UserResponseDto>> UpdateCustomerStatus(
            int userId,
            [FromBody] AccountStatusUpdateDto request)
        {
            if (request.Status != UserStatus.Active && request.Status != UserStatus.Inactive)
                return BadRequest("Status must be Active or Inactive");

            var user = await FindCustomer(userId);
            if (user == null) return NotFound("Customer not found");

            user.Status = request.Status;
            await _context.SaveChangesAsync();
            return Ok(UserResponseDto.From(user));
        }

        // DELETE: /api/customers/5
        /// <summary>Deletes a customer account.</summary>
        /// <remarks>
        /// Orders, visits and service requests still point to the account, so the row is kept
        /// but anonymous: the email becomes deleted-{id}@deleted.invalid, the name "Deleted Customer", the
        /// password, Google sign-in and location are removed, and the status is set to Deleted. Outstanding
        /// verification and reset links stop working, and the email can be used to register again.
        /// A login token the customer already has keeps working until it expires.
        /// </remarks>
        /// <response code="204">Deleted.</response>
        /// <response code="404">No customer with that ID, or it's already deleted.</response>
        [HttpDelete("{userId:int}")]
        public async Task<IActionResult> DeleteCustomer(int userId)
        {
            var user = await FindCustomer(userId);
            if (user == null) return NotFound("Customer not found");

            user.Email = $"deleted-{user.User_id}@deleted.invalid";
            user.First_name = "Deleted";
            user.Last_name = "Customer";
            user.Password_hash = null;
            user.GoogleSocial = null;
            user.Is_email_confirmed = false;
            user.Location_id = null;
            user.Status = UserStatus.Deleted;

            // Outstanding email verification / password reset links stop working
            var tokens = _context.AuthActionTokens.Where(t => t.UserId == userId);
            _context.AuthActionTokens.RemoveRange(tokens);

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // Customer accounts only, excluding ones already deleted
        private Task<User?> FindCustomer(int userId) =>
            _context.Users.FirstOrDefaultAsync(u =>
                u.User_id == userId &&
                u.Role == UserRoles.Customer &&
                u.Status != UserStatus.Deleted);
    }
}
