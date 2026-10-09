using Microsoft.Extensions.Logging;
using back_end.domain;
using back_end.domain.Entities;
using back_end.domain.DbContexts;
using back_end.domain.enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace back_end.domain.Seeders
{
    /// <summary>
    /// Seeder for the users table with initial data.
    /// Creates 3 staff accounts, 10 customer accounts, and a guest account.
    /// Note: Admin account (user_id: 1) should be created separately.
    /// </summary>
    public class UserSeeder : ISeeder
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UserSeeder> _logger;

        public UserSeeder(ApplicationDbContext context, ILogger<UserSeeder> logger)
        {
            _context = context;
            _logger = logger;
        }

        public void Seed()
        {
            // Admin accounts
            List<User> adminUsers = [
                  new()
                    {
                        Email = "admin.user@sushitoshi.ca",
                        Password_hash = HashPassword("AdminPass123!"),
                        First_name = "Admin",
                        Last_name = "User",
                        Role = UserRoles.Admin,
                        Status = UserStatus.Active,
                        Is_email_confirmed = true,
                        Created_at = DateTime.UtcNow,
                        Last_Interaction_at = DateTime.UtcNow
                    }
            ];

            // Staff accounts
            List<User> staffUsers = [
                  new()
                    {
                        Email = "john.smith@sushitoshi.ca",
                        Password_hash = HashPassword("StaffPass123!"),
                        First_name = "John",
                        Last_name = "Smith",
                        Role = UserRoles.Staff,
                        Status = UserStatus.Active,
                        Is_email_confirmed = true,
                        Created_at = DateTime.UtcNow,
                        Last_Interaction_at = DateTime.UtcNow,
                        Location_id = 2
                    },
                    new ()
                    {
                        Email = "sarah.jones@sushitoshi.ca",
                        Password_hash = HashPassword("StaffPass123!"),
                        First_name = "Sarah",
                        Last_name = "Jones",
                        Role = UserRoles.Staff,
                        Status = UserStatus.Active,
                        Is_email_confirmed = true,
                        Created_at = DateTime.UtcNow,
                        Last_Interaction_at = DateTime.UtcNow,
                        Location_id = 1
                    },
                    new()
                    {
                        Email = "michael.chen@sushitoshi.ca",
                        Password_hash = HashPassword("StaffPass123!"),
                        First_name = "Michael",
                        Last_name = "Chen",
                        Role = UserRoles.Staff,
                        Status = UserStatus.Active,
                        Is_email_confirmed = true,
                        Created_at = DateTime.UtcNow,
                        Last_Interaction_at = DateTime.UtcNow,
                        Location_id = 2
                    }
            ];

            // Customer accounts
            List<User> customerUsers = [
                new ()
                {
                    Email = "guestemail@email.com",
                    Password_hash = HashPassword("GuestUser!"),
                    First_name = "Guest",
                    Last_name = "User",
                    Role = UserRoles.Customer,
                    Status = UserStatus.Active,
                    Is_email_confirmed = true,
                    Created_at = DateTime.UtcNow,
                    Last_Interaction_at = DateTime.UtcNow
                },
                new()
                {
                    Email = "emma.wilson@email.com",
                    Password_hash = HashPassword("Customer123!"),
                    First_name = "Emma",
                    Last_name = "Wilson",
                    Role = UserRoles.Customer,
                    Status = UserStatus.Active,
                    Is_email_confirmed = true,
                    Created_at = DateTime.UtcNow,
                    Last_Interaction_at = DateTime.UtcNow
                },
                new()
                {
                    Email = "james.brown@email.com",
                    Password_hash = HashPassword("Customer123!"),
                    First_name = "James",
                    Last_name = "Brown",
                    Role = UserRoles.Customer,
                    Status = UserStatus.Active,
                    Is_email_confirmed = true,
                    Created_at = DateTime.UtcNow,
                    Last_Interaction_at = DateTime.UtcNow
                },
                new()
                {
                    Email = "sophia.lee@email.com",
                    Password_hash = HashPassword("Customer123!"),
                    First_name = "Sophia",
                    Last_name = "Lee",
                    Role = UserRoles.Customer,
                    Status = UserStatus.Active,
                    Is_email_confirmed = true,
                    Created_at = DateTime.UtcNow,
                    Last_Interaction_at = DateTime.UtcNow
                },
                new()
                {
                    Email = "oliver.taylor@email.com",
                    Password_hash = HashPassword("Customer123!"),
                    First_name = "Oliver",
                    Last_name = "Taylor",
                    Role = UserRoles.Customer,
                    Status = UserStatus.Active,
                    Is_email_confirmed = true,
                    Created_at = DateTime.UtcNow,
                    Last_Interaction_at = DateTime.UtcNow
                },
                new User
                {
                    Email = "ava.garcia@email.com",
                    Password_hash = HashPassword("Customer123!"),
                    First_name = "Ava",
                    Last_name = "Garcia",
                    Role = UserRoles.Customer,
                    Status = UserStatus.Active,
                    Is_email_confirmed = true,
                    Created_at = DateTime.UtcNow,
                    Last_Interaction_at = DateTime.UtcNow
                },
                new()
                {
                    Email = "william.miller@email.com",
                    Password_hash = HashPassword("Customer123!"),
                    First_name = "William",
                    Last_name = "Miller",
                    Role = UserRoles.Customer,
                    Status = UserStatus.Active,
                    Is_email_confirmed = true,
                    Created_at = DateTime.UtcNow,
                    Last_Interaction_at = DateTime.UtcNow
                },
                new()
                {
                    Email = "mia.davis@email.com",
                    Password_hash = HashPassword("Customer123!"),
                    First_name = "Mia",
                    Last_name = "Davis",
                    Role = UserRoles.Customer,
                    Status = UserStatus.Active,
                    Is_email_confirmed = true,
                    Created_at = DateTime.UtcNow,
                    Last_Interaction_at = DateTime.UtcNow
                },
                new()
                {
                    Email = "lucas.martinez@email.com",
                    Password_hash = HashPassword("Customer123!"),
                    First_name = "Lucas",
                    Last_name = "Martinez",
                    Role = UserRoles.Customer,
                    Status = UserStatus.Active,
                    Is_email_confirmed = true,
                    Created_at = DateTime.UtcNow,
                    Last_Interaction_at = DateTime.UtcNow
                },
                new()
                {
                    Email = "isabella.anderson@email.com",
                    Password_hash = HashPassword("Customer123!"),
                    First_name = "Isabella",
                    Last_name = "Anderson",
                    Role = UserRoles.Customer,
                    Status = UserStatus.Active,
                    Is_email_confirmed = true,
                    Created_at = DateTime.UtcNow,
                    Last_Interaction_at = DateTime.UtcNow
                },
                new()
                {
                    Email = "ethan.thomas@email.com",
                    Password_hash = HashPassword("Customer123!"),
                    First_name = "Ethan",
                    Last_name = "Thomas",
                    Role = UserRoles.Customer,
                    Status = UserStatus.Active,
                    Is_email_confirmed = true,
                    Created_at = DateTime.UtcNow,
                    Last_Interaction_at = DateTime.UtcNow
                }
            ];

            List<User> allUsers = [.. staffUsers.Concat(customerUsers).Concat(adminUsers)];

            // Only add users that don't already exist in DB (by email)
            HashSet<string> existingEmails = [.. _context.Users.Select(u => u.Email)];

            List<User> usersToAdd = [.. allUsers.Where(u => !existingEmails.Contains(u.Email))];

            if (usersToAdd.Count > 0)
            {
                _context.Users.AddRange(usersToAdd);
                // Note: SaveChanges is called in DatabaseSeeder after each seeder
                _logger.LogInformation($"Added {adminUsers.Count} admin users, {staffUsers.Count} staff users, {customerUsers.Count} customer users and any missing guest account.");
            }
            else
            {
                _logger.LogInformation("Users already seeded.");
            }
        }

        private string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }
    }
}