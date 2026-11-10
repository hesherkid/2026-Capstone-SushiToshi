using System;
using System.ComponentModel.DataAnnotations;
using back_end.domain;
using back_end.domain.enums;
using back_end.domain.Entities;

namespace back_end.Dtos
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
        public int UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public UserRoles Role { get; set; }
        public UserStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastInteractionAt { get; set; }
        public bool IsEmailConfirmed { get; set; }

        public static UserResponseDto From(User u) => new UserResponseDto
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
        };
    }

    public class PagedUserResponseDto
    {
        public int Page { get; set; }
        public int page_size { get; set; }
        public int Total { get; set; }
        public List<UserResponseDto> Items { get; set; } = new();
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