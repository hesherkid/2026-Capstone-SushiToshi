using System.ComponentModel.DataAnnotations;
using back_end.domain.Entities;
using back_end.domain.enums;

namespace back_end.Dtos
{
    // The signed-in user's own account, from GET /api/me/account
    public class AccountDto
    {
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public UserRoles Role { get; set; }
        // False for accounts that only sign in with Google, which have no password to change
        public bool HasPassword { get; set; }
        // False for staff, whose names are managed by an admin in Manage Staff
        public bool CanChangeName { get; set; }

        public static AccountDto From(User u) => new AccountDto
        {
            Email = u.Email,
            FirstName = u.First_name,
            LastName = u.Last_name,
            Role = u.Role,
            HasPassword = !string.IsNullOrEmpty(u.Password_hash),
            CanChangeName = CanChangeNameOf(u),
        };

        // Staff names are set by an admin in Manage Staff; customers and admins change their own
        public static bool CanChangeNameOf(User u) => u.Role != UserRoles.Staff;
    }

    // PUT /api/me/account. Same lengths as User.First_name and User.Last_name; names are trimmed
    public class UpdateAccountDto
    {
        [Required, MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string LastName { get; set; } = string.Empty;
    }

    // POST /api/me/account/change-password. The new password follows the usual rule: 8 to 200 characters,
    // not only whitespace, never trimmed
    public class ChangePasswordDto
    {
        [Required, MaxLength(200)]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required, MinLength(8), MaxLength(200)]
        public string NewPassword { get; set; } = string.Empty;
    }
}
