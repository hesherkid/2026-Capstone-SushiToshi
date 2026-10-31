using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;

namespace back_end.Services
{
    // Creates the first admin from configuration, so a production database (which seeds no
    // accounts) can be signed into. Does nothing once any admin exists, so it never changes
    // an existing account and leaving the settings in place can't be used to take one over.
    public static class AdminBootstrap
    {
        public const string EmailKey = "Bootstrap:AdminEmail";
        public const string PasswordKey = "Bootstrap:AdminPassword";
        public const string FirstNameKey = "Bootstrap:AdminFirstName";
        public const string LastNameKey = "Bootstrap:AdminLastName";

        // Stricter than customer sign-up: this account can do everything
        public const int PasswordMinLength = 12;
        public const int PasswordMaxLength = 200;

        // Called at startup after migrations. Returns true when an admin was created.
        // Throws InvalidOperationException when the settings are incomplete or invalid.
        public static async Task<bool> EnsureAdminAsync(
            ApplicationDbContext context,
            IConfiguration configuration,
            ILogger logger,
            CancellationToken ct = default)
        {
            if (await context.Users.AnyAsync(u => u.Role == UserRoles.Admin, ct))
            {
                return false;
            }

            var email = configuration[EmailKey]?.Trim().ToLowerInvariant();
            var password = configuration[PasswordKey];

            if (string.IsNullOrEmpty(email) && string.IsNullOrEmpty(password))
            {
                logger.LogWarning(
                    "No admin account exists. Set {EmailKey} and {PasswordKey} to create one on startup.",
                    EmailKey, PasswordKey);
                return false;
            }

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                throw new InvalidOperationException(
                    $"Both {EmailKey} and {PasswordKey} must be set to create the first admin.");
            }

            if (!new EmailAddressAttribute().IsValid(email))
            {
                throw new InvalidOperationException($"{EmailKey} is not a valid email address.");
            }

            if (password.Length < PasswordMinLength || password.Length > PasswordMaxLength)
            {
                throw new InvalidOperationException(
                    $"{PasswordKey} must be {PasswordMinLength} to {PasswordMaxLength} characters long.");
            }

            // Never promote an existing account: that would hand admin access to whoever owns it
            if (await context.Users.AnyAsync(u => u.Email == email, ct))
            {
                throw new InvalidOperationException(
                    $"{EmailKey} belongs to an existing account. Use an email that isn't registered yet.");
            }

            context.Users.Add(new User
            {
                Email = email,
                Password_hash = BCrypt.Net.BCrypt.HashPassword(password),
                First_name = configuration[FirstNameKey]?.Trim() is { Length: > 0 } first ? first : "Admin",
                Last_name = configuration[LastNameKey]?.Trim() is { Length: > 0 } last ? last : "User",
                Role = UserRoles.Admin,
                Status = UserStatus.Active,
                Is_email_confirmed = true,
                Created_at = DateTime.UtcNow,
                Last_Interaction_at = DateTime.UtcNow
            });
            await context.SaveChangesAsync(ct);

            logger.LogWarning(
                "Created the first admin account ({Email}) from configuration. Remove {PasswordKey} now that it exists.",
                email, PasswordKey);
            return true;
        }
    }
}
