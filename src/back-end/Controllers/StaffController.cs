using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using back_end.Dtos;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using back_end.Helpers;
using back_end.Services;

namespace back_end.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StaffController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<StaffController> _logger;
        private readonly ILocationAccessService _locationAccess;

        public StaffController(ApplicationDbContext context, ILogger<StaffController> logger, ILocationAccessService locationAccess)
        {
            _context = context;
            _logger = logger;
            _locationAccess = locationAccess;
        }

        /// <summary>
        /// Creates a staff or admin account.
        /// </summary>
        /// <param name="userData">The new account's details</param>
        /// <remarks>
        /// Sample request:
        ///
        ///     POST /api/staff
        ///     {
        ///       "email": "sam@example.com", "first_name": "Sam", "last_name": "Tanaka",
        ///       "password": "StaffPass123!", "role": "Staff", "status": "Active",
        ///       "location_id": 1, "location_ids": [1, 2]
        ///     }
        ///
        /// - role must be Staff or Admin. status defaults to Active.
        /// - password is 8–200 characters. The email is stored in lower case and counts as confirmed.
        /// - Staff: location_ids are the locations they may work at; location_id, if given, is added to them
        ///   and is where they start. Admins can work anywhere, so location_ids is ignored for them.
        /// </remarks>
        /// <response code="201">The new account; the Location header points to GET /api/staff/{userId}.</response>
        /// <response code="400">role isn't Staff or Admin; a name, email or password is missing or invalid;
        /// or a location doesn't exist.</response>
        /// <response code="401">Not signed in.</response>
        /// <response code="403">Not an admin.</response>
        /// <response code="409">The email is already registered.</response>
        [HttpPost]
        [Authorize(Policy = "adminOnly")]
        [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<UserResponseDto>> CreateStaff(
            [FromBody] CreateStaffDto userData
        )
        {
            Locations location = new();
            UserRoles? role = userData.Role;

            if (role != UserRoles.Staff && role != UserRoles.Admin)
                return BadRequest("Role must be either staff or admin");

            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            string? email = userData.Email?.Trim().ToLowerInvariant();
            bool emailExists = email != null && await _context.Users.AnyAsync(u => u.Email.ToLower() == email);

            if (string.IsNullOrWhiteSpace(userData.FirstName) || string.IsNullOrWhiteSpace(userData.LastName))
                return BadRequest("First name and last name are required");

            if (string.IsNullOrWhiteSpace(email))
                return BadRequest("Email is required");
            else if (emailExists)
                return Conflict("Email already registered");

            if (string.IsNullOrWhiteSpace(userData.Password))
                return BadRequest("Password is required");

            if (userData.LocationId is int locationId
                && !await _context.Locations.AnyAsync(l => l.Location_Id == locationId))
                return BadRequest("Location not found");

            DateTime now = DateTime.UtcNow;

            User entity = new()
            {
                Email = email,
                Password_hash = BCrypt.Net.BCrypt.HashPassword(userData.Password, workFactor: 12),
                First_name = userData.FirstName.Trim(),
                Last_name = userData.LastName.Trim(),
                Role = (UserRoles)role,
                Status = userData.Status,
                Is_email_confirmed = true,
                Created_at = now,
                Last_Interaction_at = now,
                Location_id = userData.LocationId
            };

            // Staff can switch only between assigned locations, so assign the one they start at too
            if (entity.Role == UserRoles.Staff)
            {
                var locationIds = userData.LocationIds ?? [];
                if (userData.LocationId is int startingLocationId) locationIds = [.. locationIds, startingLocationId];

                var error = await AssignLocationsAsync(entity, locationIds, userData.LocationId);
                if (error != null) return BadRequest(error);
            }

            _context.Users.Add(entity);
            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetStaff),
                new { userId = entity.User_id },
                ToResponse(entity)
            );
        }

        /// <summary>
        /// Staff and admin accounts, newest first, a page at a time.
        /// </summary>
        /// <param name="role">Optional: Staff or Admin only</param>
        /// <param name="skip">How many to skip (0 or more)</param>
        /// <param name="limit">How many to return (1–100, default 100)</param>
        /// <remarks>
        /// Sample request:
        ///
        ///     GET /api/staff?role=Staff&amp;skip=0&amp;limit=100
        ///
        /// Each account includes its current location and the locations it's assigned to.
        /// </remarks>
        /// <response code="200">The accounts (can be empty).</response>
        /// <response code="400">skip is negative, limit is outside 1–100, or role isn't Staff or Admin.</response>
        /// <response code="401">Not signed in.</response>
        /// <response code="403">Not staff.</response>
        [HttpGet]
        [Authorize(Policy = "staffOnly")]
        [ProducesResponseType(typeof(List<StaffResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<IEnumerable<UserResponseDto>>> ListStaff(
            [FromQuery] UserRoles? role = null,
            [FromQuery] int skip = 0,
            [FromQuery] int limit = 100)
        {
            if (skip < 0) return BadRequest("skip must be >= 0");
            if (limit < 1 || limit > 100) return BadRequest("limit must be between 1 and 100");

            var q = _context.Users.AsNoTracking()
                .Where(u => u.Role == UserRoles.Staff || u.Role == UserRoles.Admin);

            if (role.HasValue)
            {
                if (role != UserRoles.Staff && role != UserRoles.Admin)
                    return BadRequest("Role must be either staff or admin");
                q = q.Where(u => u.Role == role);
            }

            var users = await q
                .OrderByDescending(u => u.Created_at)
                .Skip(skip)
                .Take(limit)
                .Select(u => new StaffResponseDto
                {
                    CreatedAt = u.Created_at,
                    Email = u.Email,
                    IsEmailConfirmed = u.Is_email_confirmed,
                    LastTransactionAt = u.Last_Interaction_at ?? u.Created_at,
                    LastName = u.Last_name,
                    FirstName = u.First_name,
                    Role = u.Role,
                    Status = u.Status,
                    UserId = u.User_id,
                    Location = u.PrimaryLocation != null ? u.PrimaryLocation.Name : "Not Assigned",
                    LocationId = u.Location_id,
                    LocationIds = u.AssignedLocations.OrderBy(a => a.Location_Id).Select(a => a.Location_Id).ToList(),
                    Locations = u.AssignedLocations.OrderBy(a => a.Location_Id).Select(a => a.Location.Name).ToList()
                })
                .ToListAsync();


            return Ok(users);
        }

        /// <summary>
        /// One staff or admin account.
        /// </summary>
        /// <param name="userId">The account</param>
        /// <response code="200">The account (without its locations; the list includes them).</response>
        /// <response code="401">Not signed in.</response>
        /// <response code="403">Not staff.</response>
        /// <response code="404">No staff or admin account has that ID.</response>
        [HttpGet("{userId:int}")]
        [Authorize(Policy = "staffOnly")]
        [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<UserResponseDto>> GetStaff(int userId)
        {
            var u = await _context.Users.AsNoTracking()
                .Where(x => x.User_id == userId &&
                            (x.Role == UserRoles.Staff || x.Role == UserRoles.Admin))
                .FirstOrDefaultAsync();

            if (u == null) return NotFound("Staff user not found");
            return Ok(ToResponse(u));
        }

        /// <summary>
        /// Updates a staff or admin account.
        /// </summary>
        /// <param name="userId">The account</param>
        /// <param name="userData">The new details</param>
        /// <remarks>
        /// Sample request:
        ///
        ///     PUT /api/staff/5
        ///     {
        ///       "first_name": "Sue", "last_name": "Park", "email": "sue@example.com",
        ///       "role": "Staff", "status": "Active", "location_ids": [1, 2]
        ///     }
        ///
        /// - role (Staff or Admin) and status are required.
        /// - email, first_name and last_name are optional; blank leaves them as they are.
        /// - location_ids (staff only) replaces their assigned locations; leave it out to keep them. If their
        ///   current location is no longer assigned, they move to the first one left, or to none.
        /// - Admins can't change their own role or status.
        /// - Customer accounts aren't found here, so a customer can't be made staff.
        /// </remarks>
        /// <response code="200">The updated account.</response>
        /// <response code="400">role or status is missing; role isn't Staff or Admin; an admin changed their own
        /// role or status; or a location doesn't exist.</response>
        /// <response code="401">Not signed in.</response>
        /// <response code="403">Not an admin.</response>
        /// <response code="404">No staff or admin account has that ID.</response>
        /// <response code="409">The email is already registered.</response>
        [HttpPut("{userId:int}")]
        [Authorize(Policy = "adminOnly")]
        [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<UserResponseDto>> UpdateStaff(int userId, [FromBody] StaffMemberUpdateDto userData)
        {
            // Staff and admin accounts only, so this can't turn a customer into staff
            var user = await FindStaff(userId);

            if (user == null) return NotFound("Staff user not found");

            // Stops an admin locking themselves out by demoting or deactivating their own account
            bool isSelf = ClaimsHelpers.GetUserId(User) == userId.ToString();
            if (isSelf && (userData.Role != user.Role || userData.Status != user.Status))
            {
                return BadRequest("You can't change the role or status of your own account");
            }

            if (!string.IsNullOrWhiteSpace(userData.Email))
            {
                var newEmail = userData.Email.Trim().ToLowerInvariant();
                if (!newEmail.Equals(user.Email, StringComparison.OrdinalIgnoreCase))
                {
                    var exists = await _context.Users.AnyAsync(u => u.Email.ToLower() == newEmail && u.User_id != userId);
                    if (exists) return Conflict("Email already registered");
                    user.Email = newEmail;
                }
            }

            if (!string.IsNullOrWhiteSpace(userData.FirstName)) user.First_name = userData.FirstName.Trim();
            if (!string.IsNullOrWhiteSpace(userData.LastName)) user.Last_name = userData.LastName.Trim();


            if (userData.Role != UserRoles.Staff && userData.Role != UserRoles.Admin)
            {
                return BadRequest("Role must be either staff or admin");
            }

            user.Role = userData.Role;
            user.Status = userData.Status;

            // Admins can work at every location, so only staff have assigned locations
            if (user.Role == UserRoles.Staff && userData.LocationIds != null)
            {
                var error = await AssignLocationsAsync(user, userData.LocationIds);
                if (error != null) return BadRequest(error);
            }

            await _context.SaveChangesAsync();
            return Ok(ToResponse(user));
        }

        /// <summary>
        /// Deactivates or reactivates a staff or admin account. Inactive accounts can't sign in.
        /// </summary>
        /// <param name="userId">The account</param>
        /// <param name="request">Active or Inactive</param>
        /// <remarks>
        /// Sample request:
        ///
        ///     PUT /api/staff/5/status
        ///     { "status": "Inactive" }
        ///
        /// Admins can't change their own status.
        /// </remarks>
        /// <response code="200">The updated account.</response>
        /// <response code="400">status isn't Active or Inactive, or it's the caller's own account.</response>
        /// <response code="401">Not signed in.</response>
        /// <response code="403">Not an admin.</response>
        /// <response code="404">No staff or admin account has that ID.</response>
        [HttpPut("{userId:int}/status")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<UserResponseDto>> UpdateStaffStatus(int userId, [FromBody] AccountStatusUpdateDto request)
        {
            if (request.Status != UserStatus.Active && request.Status != UserStatus.Inactive)
                return BadRequest("Status must be Active or Inactive");

            // Stops an admin locking themselves out
            if (ClaimsHelpers.GetUserId(User) == userId.ToString())
                return BadRequest("You can't change the status of your own account");

            var user = await FindStaff(userId);
            if (user == null) return NotFound("Staff user not found");

            user.Status = request.Status;
            await _context.SaveChangesAsync();
            return Ok(ToResponse(user));
        }

        private Task<User?> FindStaff(int userId) =>
            _context.Users
                .Include(u => u.AssignedLocations)
                .FirstOrDefaultAsync(u =>
                    u.User_id == userId &&
                    (u.Role == UserRoles.Staff || u.Role == UserRoles.Admin));

        // Replaces a staff member's assigned locations (AssignedLocations must be loaded).
        // They start at startAt if it's one of them; otherwise, if their current location is no
        // longer assigned, they move to another assigned location, or to none if none are left.
        // Returns an error message when a location doesn't exist
        private async Task<string?> AssignLocationsAsync(User user, IEnumerable<int> locationIds, int? startAt = null)
        {
            var ids = locationIds.Distinct().ToList();
            if (await _context.Locations.CountAsync(l => ids.Contains(l.Location_Id)) != ids.Count)
                return "Location not found";

            foreach (var removed in user.AssignedLocations.Where(a => !ids.Contains(a.Location_Id)).ToList())
            {
                user.AssignedLocations.Remove(removed);
                _context.StaffLocations.Remove(removed);
            }
            foreach (var added in ids.Where(id => user.AssignedLocations.All(a => a.Location_Id != id)))
            {
                user.AssignedLocations.Add(new StaffLocation { Location_Id = added });
            }

            if (startAt is int start && ids.Contains(start))
                user.Location_id = start;
            else if (user.Location_id is not int current || !ids.Contains(current))
                user.Location_id = ids.Count > 0 ? ids[0] : null;

            return null;
        }

        /// <summary>
        /// Changes a staff or admin account's password, given its current password.
        /// </summary>
        /// <param name="userId">The account</param>
        /// <param name="passwordData">The current password, and the new one</param>
        /// <remarks>
        /// Sample request:
        ///
        ///     POST /api/staff/5/change-password
        ///     { "current_password": "OldPass123!", "password": "NewPass123!" }
        ///
        /// Both passwords are 8–200 characters. To set a password without the current one, use reset-password.
        /// </remarks>
        /// <response code="200">The account.</response>
        /// <response code="400">The current password is wrong, or a password isn't 8–200 characters.</response>
        /// <response code="401">Not signed in.</response>
        /// <response code="403">Not an admin.</response>
        /// <response code="404">No staff or admin account has that ID.</response>
        [HttpPost("{userId:int}/change-password")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<UserResponseDto>> ChangeStaffPassword(int userId, [FromBody] StaffMemberChangePasswordDto passwordData)
        {
            var user = await _context.Users
                .Where(x => x.User_id == userId &&
                            (x.Role == UserRoles.Staff || x.Role == UserRoles.Admin))
                .FirstOrDefaultAsync();

            if (user == null) return NotFound("Staff user not found");

            var ok = BCrypt.Net.BCrypt.Verify(passwordData.CurrentPassword, user.Password_hash);
            if (!ok) return BadRequest("Incorrect current password");

            user.Password_hash = BCrypt.Net.BCrypt.HashPassword(passwordData.NewPassword, workFactor: 12);
            user.Last_Interaction_at = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(ToResponse(user));
        }

        /// <summary>
        /// Sets a new password for a staff or admin account, without its current one.
        /// </summary>
        /// <param name="userId">The account</param>
        /// <param name="request">The new password (8–200 characters)</param>
        /// <remarks>
        /// Sample request:
        ///
        ///     POST /api/staff/5/reset-password
        ///     { "new_password": "NewPass123!" }
        /// </remarks>
        /// <response code="200">The account.</response>
        /// <response code="400">The new password isn't 8–200 characters.</response>
        /// <response code="401">Not signed in.</response>
        /// <response code="403">Not an admin.</response>
        /// <response code="404">No staff or admin account has that ID.</response>
        [HttpPost("{userId:int}/reset-password")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<UserResponseDto>> ResetStaffPassword(int userId, [FromBody] ResetPasswordRequestDto request)
        {
            var user = await _context.Users
                .Where(x => x.User_id == userId &&
                            (x.Role == UserRoles.Staff || x.Role == UserRoles.Admin))
                .FirstOrDefaultAsync();

            if (user == null) return NotFound("Staff user not found");

            user.Password_hash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: 12);
            user.Last_Interaction_at = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(ToResponse(user));
        }

        /// <summary>
        /// Switches the signed-in staff member or admin to another location.
        /// </summary>
        /// <param name="locationId">The location to work at</param>
        /// <remarks>
        /// Sample request:
        ///
        ///     PUT /api/staff/update-user-location/2
        ///
        /// Staff can switch only between the locations an admin assigned them; admins can pick any.
        /// </remarks>
        /// <response code="200">The location was changed.</response>
        /// <response code="400">The caller's account wasn't found.</response>
        /// <response code="401">Not signed in.</response>
        /// <response code="403">Not staff, or not assigned to that location.</response>
        /// <response code="404">The location doesn't exist.</response>
        /// <response code="500">The change couldn't be saved.</response>
        [HttpPut("update-user-location/{locationId}")]
        [Authorize(Roles = "Admin,Staff")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateStaffLocation(
            int locationId
        )
        {
            try
            {
                string? nameIdentifier = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                int userId = 0;
                if (string.IsNullOrEmpty(nameIdentifier) || !int.TryParse(nameIdentifier, out userId))
                {
                    return BadRequest("Issue in processing a User location request.");
                }
                var user = await _context.Users.FirstOrDefaultAsync(u => u.User_id == userId);
                if (user is null)
                {
                    return BadRequest("Issue in Processing a user request for location change");
                }
                if (!await _context.Locations.AnyAsync(l => l.Location_Id == locationId))
                {
                    return NotFound("Location not found");
                }
                // Staff can switch only between the locations an admin assigned them; admins anywhere
                if (!await _locationAccess.CanSwitchToLocationAsync(User, locationId))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, ILocationAccessService.NotAssignedMessage);
                }
                user.Location_id = locationId;
                _context.Update(user);
                await _context.SaveChangesAsync();
                return Ok("User Location was updated. ");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in updating user locations. ");
                return BadRequest("Unhandled Exception occurred in processing request.");
            }


        }

        /// <summary>
        /// The signed-in staff member's or admin's current location. Prefer GET /api/staff/my-locations.
        /// </summary>
        /// <response code="200">The location, or null if they have none.</response>
        /// <response code="400">The caller's account wasn't found.</response>
        /// <response code="401">Not signed in.</response>
        /// <response code="403">Not staff.</response>
        /// <response code="500">The location couldn't be loaded.</response>
        [HttpGet("get-initial-staff-location")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> GetStaffStartingLocation(
        )
        {
            try
            {
                string? nameIdentifier = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                int userId = 0;
                if (string.IsNullOrEmpty(nameIdentifier) || !int.TryParse(nameIdentifier, out userId))
                {
                    return BadRequest("Issue in processing a User location request.");
                }
                var user = await _context.Users.Include(u => u.PrimaryLocation).FirstOrDefaultAsync(u => u.User_id == userId);
                if (user is null)
                {
                    return BadRequest("Issue in Processing a user request for location change");
                }

                return Ok(user.PrimaryLocation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in updating user locations. ");
                return BadRequest("Unhandled Exception occurred in processing request.");
            }


        }

        /// <summary>
        /// The signed-in user's current location, and the locations they can switch between.
        /// </summary>
        /// <remarks>
        /// Sample request:
        ///
        ///     GET /api/staff/my-locations
        ///
        /// Staff get the locations an admin assigned them; admins get every location (all_locations is true).
        /// current_location is null if they have none.
        /// </remarks>
        /// <response code="200">Their current location and the locations they can pick.</response>
        /// <response code="400">The user ID in the sign-in is invalid.</response>
        /// <response code="401">Not signed in.</response>
        /// <response code="403">Not staff.</response>
        /// <response code="404">The caller's account wasn't found.</response>
        [HttpGet("my-locations")]
        [Authorize(Roles = "Admin,Staff")]
        [ProducesResponseType(typeof(MyLocationsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<MyLocationsDto>> GetMyLocations()
        {
            if (!int.TryParse(ClaimsHelpers.GetUserId(User), out var userId))
                return BadRequest("Issue in processing a User location request.");

            var user = await _context.Users.AsNoTracking()
                .Include(u => u.PrimaryLocation)
                .FirstOrDefaultAsync(u => u.User_id == userId);
            if (user is null) return NotFound("User not found");

            bool allLocations = !_locationAccess.IsLimitedToOneLocation(User);
            var locations = allLocations
                ? await _context.Locations
                    .OrderBy(l => l.Location_Id)
                    .Select(l => new LocationOptionDto { LocationId = l.Location_Id, Name = l.Name })
                    .ToListAsync()
                : await _context.StaffLocations
                    .Where(sl => sl.User_Id == userId)
                    .OrderBy(sl => sl.Location_Id)
                    .Select(sl => new LocationOptionDto { LocationId = sl.Location_Id, Name = sl.Location.Name })
                    .ToListAsync();

            return Ok(new MyLocationsDto
            {
                CurrentLocation = user.PrimaryLocation is null
                    ? null
                    : new LocationOptionDto { LocationId = user.PrimaryLocation.Location_Id, Name = user.PrimaryLocation.Name },
                Locations = locations,
                AllLocations = allLocations,
            });
        }

        private static UserResponseDto ToResponse(User u) => UserResponseDto.From(u);
    }
}