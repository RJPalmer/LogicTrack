using LogiTrack.Data;
using LogiTrack.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using LogiTrack.Services;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Register LogiTrackContext with DI so other services/controllers can get it.
var connectionString = builder.Configuration.GetConnectionString("LogiTrack")
    ?? "Data Source=logitrack.db";
builder.Services.AddDbContext<LogiTrackContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddMemoryCache();
builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddIdentity<ApplicationUser, IdentityRole<int>>(options =>
    {
        // Password policy
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredLength = 8;

        // Lockout settings
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;

        // User settings
        options.User.RequireUniqueEmail = true;

        // Require confirmed email before login (optional)
        options.SignIn.RequireConfirmedEmail = true;
    })
    .AddEntityFrameworkStores<LogiTrackContext>()
    .AddDefaultTokenProviders()
    .AddRoles<IdentityRole<int>>();
// Configure JWT settings from configuration (add to appsettings.json or user secrets)
var jwtSection = builder.Configuration.GetSection("JwtSettings");
// Bind JwtSettings from configuration (appsettings, user-secrets, environment variables)
var jwtSettings = new JwtSettings();
jwtSection.Bind(jwtSettings);

// Allow overriding the secret with an environment variable or user secret named: JwtSettings__Secret
var envSecret = builder.Configuration["JwtSettings:Secret"];
if (!string.IsNullOrEmpty(envSecret)) jwtSettings.Secret = envSecret;

if (string.IsNullOrWhiteSpace(jwtSettings.Secret) || jwtSettings.Secret == "ReplaceWithAStrongSecretUsedFromUserSecretsOrEnv")
{
    // For security: if no real secret is provided, warn and use a temporary dev key (do not use in prod)
    Console.WriteLine("WARNING: JWT secret is not configured. Use user-secrets or env var JwtSettings__Secret for production.");
    jwtSettings.Secret = jwtSettings.Secret ?? "dev-temp-secret-please-change";
}

builder.Services.Configure<JwtSettings>(jwtSection);
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddScoped<JwtService>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateIssuerSigningKey = true,
            RoleClaimType = ClaimTypes.Role,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

//Preload the Manager role
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
    var rolesToEnsure = new[] { "Manager", "Staff", "Viewer" };

    foreach (var roleName in rolesToEnsure)
    {
        var roleExists = await roleManager.RoleExistsAsync(roleName);
        if (!roleExists)
        {
            var result = await roleManager.CreateAsync(new IdentityRole<int>(roleName));
            if (result.Succeeded)
            {
                Console.WriteLine($"Created role: {roleName}");
            }
            else
            {
                Console.WriteLine($"Error creating role {roleName}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();
// --- Quick EF Core sanity test at startup ---
try
{
    Console.WriteLine("Starting EF Core sanity check (using DI-scoped DbContext)...");
    using (var scope = app.Services.CreateScope())
    {
        var ctx = scope.ServiceProvider.GetRequiredService<LogiTrackContext>();

        // Recreate a clean database for the sanity test to avoid leftover bad data
        // (WARNING: this deletes the local database file 'logitrack.db').
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();

        // Seed test data if none exists.
        if (!ctx.InventoryItems.Any())
        {
            var seedItems = new List<InventoryItem>
            {
                new InventoryItem("Widget", 10, "A1"),
                new InventoryItem("Gadget", 5, "B2"),
                new InventoryItem("Bolt", 100, "C3"),
            };

            ctx.InventoryItems.AddRange(seedItems);
            ctx.SaveChanges();
            Console.WriteLine("Seeded InventoryItems with sample data.");
        }

        // Query and print items.
        var items = ctx.InventoryItems.AsNoTracking().ToList();
        Console.WriteLine($"InventoryItems ({items.Count}):");
        foreach (var it in items)
        {
            Console.WriteLine(it.ToString());
        }
    }
    Console.WriteLine("EF Core sanity check completed successfully.");
}
catch (Exception ex)
{
    Console.WriteLine($"EF Core sanity check failed: {ex}");
}

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
