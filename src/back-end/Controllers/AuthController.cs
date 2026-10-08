
using System.Security.Cryptography.X509Certificates;
using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;
using back_end.DTO.Auth;
using Microsoft.AspNetCore.Mvc;
using BCryptNet = BCrypt.Net.BCrypt;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using back_end.domain.enums;
using Google.Apis.Auth;


namespace back_end.controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;

        public AuthController(ApplicationDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
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
        [HttpPost("login")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Login([FromBody] LoginDTO request)
        {
            string UserEmail = request.UserEmail ?? string.Empty;
            string UserPassword = request.UserPassword ?? string.Empty;
            var ReturnedUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == UserEmail);
            if (ReturnedUser == null)
            {
                return Unauthorized(new { message = "Invalid email or password" });
            }
            if (ReturnedUser.Status != UserStatus.Active)
            {
                return BadRequest("Issue Login into System");
            }
            bool isPasswordVerified = BCryptNet.Verify(UserPassword, ReturnedUser.Password_hash ?? string.Empty);
            if (!isPasswordVerified)
            {
                return Unauthorized(new { message = "Invalid email or password" });
            }
            ReturnedUser.Last_Interaction_at = DateTime.UtcNow;
            _context.Users.Update(ReturnedUser);
            await _context.SaveChangesAsync();

            //Generate the JWT token with Returned user assigned above
            var token = GenerateJwtToken(ReturnedUser);
            return Ok(new
            {
                access_token = token,
                token_type = "Bearer",
                expires_in = 7200, // 2 hours in seconds: 
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


            return Ok(new
            {
                access_token = GenerateJwtToken(SendNewTokenUser),
                message = "User created successfully"
            });
        }


        [HttpPost("google")]
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

                // Verify the Google token
                var settings = new GoogleJsonWebSignature.ValidationSettings
                {
                    //TODO: is there where we would add ngrok origins? Should this be somewhere else?
                    Audience = new[] { "913755751162-t0grfn5dn2np3lds6sca84l6all5rl4d.apps.googleusercontent.com" }
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

                // Check if user exists
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == userEmail);

                domain.Entities.User user;

                if (existingUser != null)
                {
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
                    message = existingUser != null ? "Login successful" : "Account created successfully"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Google login error: {ex.Message}");
                return StatusCode(500, new { message = "Authentication failed", error = ex.Message });
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
        private string GenerateJwtToken(domain.Entities.User user)
        {

            var keyStr = _config["Jwt:Key"];
            Console.WriteLine($"[JWT SIGN] Key len: {keyStr?.Length}, First8: {keyStr?[..Math.Min(8, keyStr!.Length)]}");
            // TODO: Not good practice to log the JWT key, even partially. Remove this in production. Should also rotate the JWT key in appsettings.json as it was previously commited, move it to a secure location.

            var securityKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            //Add all the claims based on the user's information and their role
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.User_id.ToString()),
                new Claim(ClaimTypes.Name, user.Email),
                new Claim(ClaimTypes.Email, user.Email), // TODO: do we want real names on orders, instead of email? Need to update Claims Helper if we do
                new Claim(ClaimTypes.Role, user.Role.ToString()),
                new Claim(JwtRegisteredClaimNames.Sub, user.Email), // FIXME: Could we end up with two NameIdentifiers? should we set sub to user.User_id.ToString()
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            };

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(
                    int.Parse(_config["Jwt:ExpiresInMinutes"] ?? "60")),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}