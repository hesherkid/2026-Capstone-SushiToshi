using System;
using System.ComponentModel.DataAnnotations;
using back_end.domain;
using back_end.domain.enums;
using back_end.domain.Entities;

namespace back_end.DTO.UserDTOs
{
    public class UserCreateDto
    {
        [Required, MaxLength(255), EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, MinLength(8), MaxLength(200)]
        public string Password { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string First_name { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string Last_name { get; set; } = string.Empty;

        public UserRoles Role { get; set; } = UserRoles.Customer;
        public UserStatus Status { get; set; } = UserStatus.Active;
    }

    public class UserUpdateDto
    {
        [MaxLength(255), EmailAddress]
        public string? Email { get; set; }

        [MaxLength(100)]
        public string? First_name { get; set; }

        [MaxLength(100)]
        public string? Last_name { get; set; }

        public UserRoles? Role { get; set; }
        public UserStatus? Status { get; set; }

        [MinLength(8), MaxLength(200)]
        public string? New_password { get; set; }
    }

    public class UserResponseDto
    {
        public int User_id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string First_name { get; set; } = string.Empty;
        public string Last_name { get; set; } = string.Empty;
        public UserRoles Role { get; set; }
        public UserStatus Status { get; set; }
        public DateTime Created_at { get; set; }
        public DateTime? Last_Interaction_at { get; set; }
        public bool Is_email_confirmed { get; set; }

        public static UserResponseDto From(User u) => new UserResponseDto
        {
            User_id = u.User_id,
            Email = u.Email,
            First_name = u.First_name,
            Last_name = u.Last_name,
            Role = u.Role,
            Status = u.Status,
            Created_at = u.Created_at,
            Last_Interaction_at = u.Last_Interaction_at,
            Is_email_confirmed = u.Is_email_confirmed
        };
    }

    public class PagedUserResponseDto
    {
        public int page { get; set; }
        public int page_size { get; set; }
        public int total { get; set; }
        public List<UserResponseDto> items { get; set; } = new();
    }


    public class PasswordChangeDto
    {
        [Required, MinLength(8), MaxLength(200)]
        public string Current_password { get; set; } = string.Empty;

        [Required, MinLength(8), MaxLength(200)]
        public string New_password { get; set; } = string.Empty;
    }

    public class ResetPasswordRequestDto
    {
        [Required, MinLength(8), MaxLength(200)]
        public string New_password { get; set; } = string.Empty;
    }

    // Admin deactivate/reactivate for staff and customer accounts (Active or Inactive)
    public class AccountStatusUpdateDto
    {
        [Required]
        public UserStatus Status { get; set; }
    }

}