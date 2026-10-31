
using back_end.Configurations;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.DTO.Auth;
using back_end.Services.Auth;
using back_end.Services.Email;
using back_end.Helpers;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using BCryptNet = BCrypt.Net.BCrypt;


namespace back_end.controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;
        private readonly IEmailService _emailService;
        private readonly IAuthTokenService _tokenService;
        private readonly AuthEmailSettings _emailSettings;

        public AuthController(
            ApplicationDbContext context,
            IConfiguration config,
            IEmailService emailService,
            IAuthTokenService tokenService,
            IOptions<AuthEmailSettings> emailSettings)
        {
            _context = context;
            _config = config;
            _emailService = emailService;
            _tokenService = tokenService;
            _emailSettings = emailSettings.Value;
        }

        /// <summary>
        /// Authenticates a user and returns a JWT access token.
        /// </summary>
        /// <param name="request">The login credentials containing email and password</param>
        /// <returns>
        /// An <see cref="IActionResult"/> containing the JWT token and authentication details.
        /// Returns HTTP 200 (OK) with the access token on successful authentication.
        /// Returns HTTP 401 (Unauthorized) if the credentials are invalid.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during authentication.
        /// </returns>
        /// <response code="200">Returns the JWT access token and token metadata</response>
        /// <response code="401">If the email or password is invalid</response>
        /// <response code="500">If an internal error occurs during authentication</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     POST /api/auth/login
        ///     {
        ///         "userEmail": "user@example.com",
        ///         "userPassword": "SecurePassword123!"
        ///     }
        ///
        /// Returns a JWT bearer token that expires in 2 hours.
        /// The token should be included in subsequent requests in the Authorization header:
        /// Authorization: Bearer {access_token}
        /// 
        /// Updates the user's last interaction timestamp upon successful login.
        /// </remarks>
        /// <summary>
        /// Signs in a new anonymous guest for "Continue as Guest".
        /// </summary>
        /// <remarks>
        /// Creates a throwaway customer account for this guest, so every guest at a table is a separate
        /// session participant. It has no password and a reserved email domain, so nobody can sign in to it
        /// later, and it's left out of the admin customer list. The token lasts a whole visit
        /// (Jwt:GuestExpiresInMinutes, default 6 hours).
        /// </remarks>
        /// <response code="200">Returns the guest's access token and display name.</response>
        /// <response code="429">Too many guest sign-ins from this network in the last minute.</response>
        [HttpPost("guest")]
        [EnableRateLimiting(GuestAccounts.RateLimitPolicy)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> GuestLogin()
        {
            var now = DateTime.UtcNow;
            var guest = new User
            {
                Email = GuestAccounts.NewEmail(),
                Password_hash = null,
                First_name = "Guest",
                Last_name = GuestAccounts.NewCode(),
                Role = UserRoles.Customer,
                Status = UserStatus.Active,
                Is_email_confirmed = true,
                Created_at = now,
                Last_Interaction_at = now,
            };

            _context.Users.Add(guest);
            await _context.SaveChangesAsync();

            int minutes = int.Parse(_config["Jwt:GuestExpiresInMinutes"] ?? GuestAccounts.DefaultTokenMinutes.ToString());

            return Ok(new
            {
                access_token = GenerateJwtToken(guest, minutes),
                token_type = "Bearer",
                expires_in = minutes * 60,
                user = new
                {
                    first_name = guest.First_name,
                    last_name = guest.Last_name,
                },
            });
        }

        [HttpPost("login")]
        [EnableRateLimiting(AuthRateLimits.SignInPolicy)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Login([FromBody] LoginDTO request)
        {
            var userEmail = (request.UserEmail ?? "")
            .Trim()
            .ToLowerInvariant();

            var userPassword = request.UserPassword ?? "";

            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.Email == userEmail);

            if (user == null ||
                string.IsNullOrEmpty(user.Password_hash) ||
                !BCryptNet.Verify(userPassword, user.Password_hash))
            {
                return Unauthorized(new
                {
                    message = "Invalid email or password"
                });
            }

            if (user.Status != UserStatus.Active)
            {
                return StatusCode(403, new
                {
                    message = "Account is not active"
                });
            }

            if (!user.Is_email_confirmed)
            {
                return StatusCode(403, new
                {
                    message = "Please verify your email before logging in",
                    requires_email_verification = true
                });
            }

            user.Last_Interaction_at = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                access_token = GenerateJwtToken(user),
                token_type = "Bearer",
                expires_in = int.Parse(
                    _config["Jwt:ExpiresInMinutes"] ?? "60") * 60,
                // The token carries no name, so the home page greets the customer from this
                user = new
                {
                    first_name = user.First_name,
                    last_name = user.Last_name,
                },
            });

        }

        /// <summary>
        /// Registers a new user account in the system.
        /// </summary>
        /// <param name="request">The registration data containing email, password, first name, and last name</param>
        /// <returns>
        /// An <see cref="IActionResult"/> indicating the result of the registration.
        /// Returns HTTP 200 (OK) with a success message when the user is created.
        /// Returns HTTP 400 (Bad Request) if validation fails or required fields are missing.
        /// Returns HTTP 409 (Conflict) if the email is already in use.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during registration.
        /// </returns>
        /// <response code="200">Returns a success message when the user is created</response>
        /// <response code="400">If validation fails or required fields are missing</response>
        /// <response code="409">If the email is already registered</response>
        /// <response code="500">If an internal error occurs during registration</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     POST /api/auth/register
        ///     {
        ///         "userEmail": "newuser@example.com",
        ///         "userPassword": "SecurePassword123!",
        ///         "firstName": "John",
        ///         "lastName": "Doe"
        ///     }
        ///
        /// Requirements:
        /// - All fields are required
        /// - Email must be unique
        /// - Password must be at least 6 characters long
        /// 
        /// Password is automatically hashed using BCrypt before storage.
        /// Email addresses are converted to lowercase and trimmed.
        /// New accounts are created with email confirmation pending.
        /// </remarks>
        [HttpPost("register")]
        [EnableRateLimiting(AuthRateLimits.EmailPolicy)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateUser([FromBody] RegisterDTO request)
        {
            var SendNewTokenUser = new domain.Entities.User();
            int passwordMinLength = 6;
            if (request == null || request.UserEmail == null || request.UserPassword == null || request.FirstName == null || request.LastName == null)
            {
                return BadRequest(new { message = "Invalid request" });
            }
            string UserEmail = request.UserEmail.ToLower().Trim() ?? string.Empty;
            string UserPassword = request.UserPassword.Trim() ?? string.Empty;
            string FirstName = request.FirstName.Trim() ?? string.Empty;
            string LastName = request.LastName.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(UserEmail) || string.IsNullOrEmpty(UserPassword) || string.IsNullOrEmpty(FirstName) || string.IsNullOrEmpty(LastName))
            {
                return BadRequest(new { message = "All fields are required" });
            }
            if (await _context.Users.AnyAsync(u => u.Email == UserEmail))
            {
                return Conflict(new { message = "Email already in use" });
            }
            if (UserPassword.Length < passwordMinLength)
            {
                return BadRequest(new { message = $"Password must be at least {passwordMinLength} characters long" });
            }
            UserPassword = BCryptNet.HashPassword(UserPassword);
            try
            {
                var NewUser = new domain.Entities.User
                {
                    Email = UserEmail,
                    Password_hash = UserPassword,
                    First_name = FirstName,
                    Last_name = LastName,
                    Is_email_confirmed = false
                };
                _context.Users.Add(NewUser);
                await _context.SaveChangesAsync();
                SendNewTokenUser = NewUser;
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Error creating user: {ex.Message}" });
            }

            var token = await _tokenService.CreateAsync(SendNewTokenUser.User_id, AuthTokenPurpose.EmailVerification);
            var verificationUrl =
                $"{_emailSettings.FrontendBaseUrl.TrimEnd('/')}" +
                "/auth/verify-email?token=" +
                Uri.EscapeDataString(token);

            await _emailService.SendEmailVerificationAsync(
                SendNewTokenUser.Email,
                verificationUrl);

            return Ok(new
            {
                message = "Registration successful. " +
                          "Please check your email to verify your account.",
                requires_email_verification = true
            });
        }


        [HttpPost("verify-email")]
        [EnableRateLimiting(AuthRateLimits.SignInPolicy)]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailDTO request)
        {
            var token = await _tokenService.FindValidAsync(
                request.Token,
                AuthTokenPurpose.EmailVerification);

            if (token == null)
            {
                return BadRequest(new
                {
                    message = "Invalid or expired verification token"
                });
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.User_id == token.UserId);

            if (user == null)
            {
                return BadRequest(new
                {
                    message = "Invalid verification token"
                });
            }

            // Perform consumption and confirmation atomically
            // in production.
            await _tokenService.ConsumeAsync(token);

            user.Is_email_confirmed = true;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Email verified successfully. You can now log in."
            });
        }

        [HttpPost("forgot-password")]
        [EnableRateLimiting(AuthRateLimits.EmailPolicy)]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDTO request)
        {
            const string successMessage =
                "If an account exists for this email, " +
                "a password reset link will be sent.";

            var email = request.Email.Trim().ToLowerInvariant();

            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.Email == email);

            if (user != null &&
                !string.IsNullOrEmpty(user.Password_hash))
            {
                var token = await _tokenService.CreateAsync(
                    user.User_id,
                    AuthTokenPurpose.PasswordReset);

                var resetUrl =
                    $"{_emailSettings.FrontendBaseUrl.TrimEnd('/')}" +
                    "/auth/reset-password?token=" +
                    Uri.EscapeDataString(token);

                await _emailService.SendPasswordResetAsync(
                    user.Email,
                    resetUrl);
            }

            return Ok(new
            {
                message = successMessage
            });
        }

        [HttpPost("reset-password")]
        [EnableRateLimiting(AuthRateLimits.SignInPolicy)]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDTO request)
        {
            var token = await _tokenService.FindValidAsync(
                request.Token,
                AuthTokenPurpose.PasswordReset);

            if (token == null)
            {
                return BadRequest(new
                {
                    message = "Invalid or expired reset token"
                });
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(x =>
                    x.User_id == token.UserId);

            if (user == null ||
                string.IsNullOrEmpty(user.Password_hash))
            {
                return BadRequest(new
                {
                    message = "Invalid reset token"
                });
            }

            // These operations must be atomic in production.
            await _tokenService.ConsumeAsync(token);

            user.Password_hash =
                BCryptNet.HashPassword(request.NewPassword);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Password reset successfully. Please log in."
            });
        }

        [HttpPost("resend-verification")]
        [EnableRateLimiting(AuthRateLimits.EmailPolicy)]
        public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationDTO request)
        {
            const string message =
                "If this account needs verification, " +
                "a new email will be sent.";

            var email = request.Email.Trim().ToLowerInvariant();

            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.Email == email);

            if (user != null && !user.Is_email_confirmed)
            {
                var token = await _tokenService.CreateAsync(
                    user.User_id,
                    AuthTokenPurpose.EmailVerification);

                var url =
                    $"{_emailSettings.FrontendBaseUrl.TrimEnd('/')}" +
                    "/auth/verify-email?token=" +
                    Uri.EscapeDataString(token);

                await _emailService.SendEmailVerificationAsync(
                    user.Email,
                    url);
            }

            return Ok(new { message });
        }

        [HttpPost("google")]
        [EnableRateLimiting(AuthRateLimits.SignInPolicy)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleTokenDTO request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.IdToken))
                {
                    return BadRequest(new { message = "Token is required" });
                }

                var clientId = _config["Google:ClientId"];
                if (string.IsNullOrWhiteSpace(clientId))
                {
                    Console.WriteLine("Google login error: Google:ClientId is not configured.");
                    return StatusCode(500, new { message = "Google sign-in is not available" });
                }

                var settings = new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { clientId }
                };

                GoogleJsonWebSignature.Payload payload;

                try
                {
                    payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);
                }
                catch (InvalidJwtException)
                {
                    return Unauthorized(new { message = "Invalid Google token" });
                }

                // Extract user info from verified token
                string userEmail = payload.Email.ToLower().Trim();
                string userName = payload.Name;
                string userPicture = payload.Picture;
                string googleId = payload.Subject;
                bool emailVerified = payload.EmailVerified;

                if (!payload.EmailVerified)
                {
                    return StatusCode(403, new
                    {
                        message = "Google email is not verified"
                    });
                }

                // Check if user exists
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == userEmail);

                domain.Entities.User user;

                if (existingUser != null)
                {
                    // Same rule as password login: deactivated accounts can't sign in
                    if (existingUser.Status != UserStatus.Active)
                    {
                        return StatusCode(403, new
                        {
                            message = "Account is not active"
                        });
                    }

                    // Existing user - update info if needed
                    user = existingUser;

                    if (string.IsNullOrEmpty(user.First_name))
                    {
                        user.First_name = userName;
                    }

                    if (string.IsNullOrEmpty(user.GoogleSocial))
                    {
                        user.GoogleSocial = googleId;
                    }

                    await _context.SaveChangesAsync();
                }
                else
                {
                    // New user - create account
                    user = new domain.Entities.User
                    {
                        Email = userEmail,
                        First_name = userName,
                        GoogleSocial = googleId,
                        Is_email_confirmed = emailVerified,
                        Created_at = DateTime.UtcNow
                    };

                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();
                }

                // Generate JWT tokens
                var accessToken = GenerateJwtToken(user);

                return Ok(new
                {
                    access_token = accessToken,
                    message = existingUser != null ? "Login successful" : "Account created successfully",
                    user = new
                    {
                        first_name = user.First_name,
                        last_name = user.Last_name,
                    },
                });
            }
            catch (Exception ex)
            {
                // The details stay in the server log; they can reveal internals to the caller
                Console.WriteLine($"Google login error: {ex.Message}");
                return StatusCode(500, new { message = "Authentication failed" });
            }
        }

        /// <summary>
        /// Generates a JWT access token for an authenticated user.
        /// </summary>
        /// <param name="user">The authenticated user for whom to generate the token</param>
        /// <returns>A JWT token string containing user claims and authentication information</returns>
        /// <remarks>
        /// The generated token includes the following claims:
        /// - NameIdentifier: User's unique ID
        /// - Name: User's email
        /// - Email: User's email address
        /// - Role: User's role (Customer, Staff, or Admin)
        /// - Sub: Subject identifier (email)
        /// - Jti: Unique token identifier
        /// 
        /// Token expiration is configurable via Jwt:ExpiresInMinutes setting (default: 60 minutes).
        /// The token is signed using HMAC-SHA256 algorithm.
        /// </remarks>
        private string GenerateJwtToken(domain.Entities.User user, int? expiresInMinutes = null)
        {

            var keyStr = _config["Jwt:Key"]
                ?? throw new InvalidOperationException("Jwt:Key is not configured.");

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyStr));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            //Add all the claims based on the user's information and their role
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.User_id.ToString()),
                new Claim(ClaimTypes.Name, user.Email),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role.ToString()),
                // Read back as a second NameIdentifier, so it must hold the ID too
                new Claim(JwtRegisteredClaimNames.Sub, user.User_id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            };

            // Guests' emails are random, so give orders and requests a readable name ("Guest K7P2").
            // ClaimsHelpers.GetUserDisplayName prefers these over the email.
            if (GuestAccounts.IsGuest(user.Email))
            {
                claims.Add(new Claim("given_name", user.First_name));
                claims.Add(new Claim("family_name", user.Last_name));
                claims.Add(new Claim(GuestAccounts.TokenClaim, "true"));
            }

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(
                    expiresInMinutes ?? int.Parse(_config["Jwt:ExpiresInMinutes"] ?? "60")),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}