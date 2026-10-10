using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;
using back_end.domain.Seeders;
using back_end.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using System.Text.Json.Serialization;
using Microsoft.OpenApi.Models;
using back_end.Configurations;
using back_end.Services.Email;
using back_end.Services.Auth;
using back_end.Helpers;
using SendGrid;

var builder = WebApplication.CreateBuilder(args);

// Settings for Email Service (SendGrid), Email Verification and Password Reset
builder.Services
    .AddOptions<SendGridSettings>()
    .Bind(builder.Configuration.GetSection(
        SendGridSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<AuthEmailSettings>()
    .Bind(builder.Configuration.GetSection(
        AuthEmailSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<ISendGridClient>(sp =>
{
    var settings = sp.GetRequiredService<
        Microsoft.Extensions.Options.IOptions<SendGridSettings>>()
        .Value;

    return new SendGridClient(settings.ApiKey);
});

builder.Services.AddScoped<IEmailService, SendGridEmailService>();
builder.Services.AddScoped<IAuthTokenService, AuthTokenService>();

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

Console.WriteLine(Path.GetPathRoot(Directory.GetCurrentDirectory()));

// Add Swagger/OpenAPI services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Sushi Toshi API",
        Version = "v1",
        Description = "API for Sushi Toshi Restaurant Management System"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme. Enter your token in the text input below."
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    c.EnableAnnotations();
});

// Add Entity Framework and MySQL/MariaDB connection
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("MySqlConnection");
    // Use static server version to avoid connection attempt during startup (important for testing)
    // Using MariaDB 10.5 as a reasonable baseline version
    options.UseMySql(connectionString, new MariaDbServerVersion(new Version(10, 5, 0)));
});

// Seeders registration
builder.Services.AddDatabaseSeeders();

// Simple JWT Authentication
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])),
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role
        };
    });

// Authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("adminOnly", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole("Admin");
    });

    options.AddPolicy("staffOnly", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole("Staff", "Admin");
    });
});

// Add rate limiting for API protection
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("api", config =>
    {
        config.PermitLimit = 100;
        config.Window = TimeSpan.FromMinutes(1);
        config.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        config.QueueLimit = 5;
    });

    options.AddFixedWindowLimiter("auth", config =>
    {
        config.PermitLimit = 10;
        config.Window = TimeSpan.FromMinutes(1);
        config.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        config.QueueLimit = 2;
    });

    // Menu item view tracking (POST api/menu-item-views).
    // Partitioned by the bearer token, not IP or user id: every guest at the restaurant shares the
    // Wi-Fi IP and the guest account, but each sign-in gets its own token.
    // Reads the raw header so it works even though UseRateLimiter runs before UseAuthentication.
    options.AddPolicy(ViewTrackingRules.RateLimitPolicy, httpContext =>
    {
        var authorization = httpContext.Request.Headers.Authorization.ToString();
        var partitionKey = string.IsNullOrEmpty(authorization)
            ? $"ip:{httpContext.Connection.RemoteIpAddress}"
            : $"token:{authorization}";

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = ViewTrackingRules.RateLimitPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});


// Add CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.WithOrigins("http://localhost:3000", "http://localhost:5173", "https://calorie-daylight-define.ngrok-free.dev")
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
        else
        {
            var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? Array.Empty<string>();

            policy.WithOrigins(allowedOrigins)
                  .WithMethods("GET", "POST", "PUT", "DELETE", "PATCH")
                  .WithHeaders("Content-Type", "Authorization")
                  .AllowCredentials();
        }
    });
});
builder.Services.AddHttpContextAccessor();
// Services
builder.Services.AddScoped<IPricingService, PricingService>();
builder.Services.AddScoped<QrGeneratorService>();

// Temporary //**
Console.WriteLine(">>> USING CONNECTION STRING:");
Console.WriteLine(builder.Configuration.GetConnectionString("MySqlConnection"));

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Sushi Toshi API v1");
        c.RoutePrefix = "swagger";
    });
}
else
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            var exceptionHandlerPathFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
            var exception = exceptionHandlerPathFeature?.Error;

            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(exception, "Unhandled exception occurred");

            await context.Response.WriteAsJsonAsync(new
            {
                error = "An unexpected error occurred. Please try again later.",
                requestId = context.TraceIdentifier
            });
        });
    });

    app.UseHttpsRedirection();
}

app.UseRateLimiter();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/", () => Results.Ok(new
{
    status = "ok",
    service = "back-end",
    message = "Backend is running",
    timestamp = DateTime.UtcNow
}));
app.MapGet("/health", () => "API is running!");
app.MapGet("/healthz", () => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTime.UtcNow
}));
app.MapGet("/db-test", async (ApplicationDbContext context) =>
{
    try
    {
        await context.Database.CanConnectAsync();
        return Results.Ok("Database connection successful!");
    }
    catch (Exception ex)
    {
        return Results.Problem($"Database connection failed: {ex.Message}");
    }
});

// Database initialization
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var canConnect = await context.Database.CanConnectAsync();
        if (canConnect)
        {
            logger.LogInformation("Database connection successful");
            await context.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied successfully");

            if (!context.MenuItems.Any())
            {
                await seeder.SeedDatabase();
                logger.LogInformation("Database seed completed.");
            }
            else
            {
                logger.LogInformation("Database has already been seeded.");
                UserSeeder userSeeder = scope.ServiceProvider.GetRequiredService<UserSeeder>();
                userSeeder.Seed();
                await context.SaveChangesAsync();
            }
        }
        else
        {
            logger.LogWarning("Cannot connect to database");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred during database initialization: {Message}", ex.Message);
    }
}

app.Run();

// Make Program class accessible to integration tests
public partial class Program { }
