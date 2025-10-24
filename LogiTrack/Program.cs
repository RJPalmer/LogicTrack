using LogiTrack.Data;
using LogiTrack.Models;
using LogiTrack.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// -----------------------------
//  DATABASE CONFIGURATION (Persistent State)
// -----------------------------
var connectionString = builder.Configuration.GetConnectionString("LogiTrack") ?? "Data Source=logitrack.db";
builder.Services.AddDbContext<LogiTrackContext>(options => options.UseSqlite(connectionString));

// -----------------------------
//  IDENTITY CONFIGURATION
// -----------------------------
builder.Services.AddIdentity<ApplicationUser, IdentityRole<int>>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = true;
})
.AddEntityFrameworkStores<LogiTrackContext>()
.AddDefaultTokenProviders();

// -----------------------------
//  JWT AUTHENTICATION SETUP
// -----------------------------
var jwtSection = builder.Configuration.GetSection("JwtSettings");
var jwtSettings = new JwtSettings();
jwtSection.Bind(jwtSettings);

// Allow secret override via env var
var envSecret = builder.Configuration["JwtSettings:Secret"];
if (!string.IsNullOrEmpty(envSecret)) jwtSettings.Secret = envSecret;

if (string.IsNullOrWhiteSpace(jwtSettings.Secret))
{
    Console.WriteLine("⚠️ WARNING: JWT secret not configured. Using temporary dev key.");
    jwtSettings.Secret = "temporary-dev-secret-change-this";
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
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1)
    };
});

builder.Services.AddAuthorization();

// -----------------------------
//  REDIS CACHING
// -----------------------------
var redisConnection = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnection;
    options.InstanceName = "LogiTrack_";

});

// -----------------------------
//  BACKGROUND SERVICES
// -----------------------------
builder.Services.AddHostedService<InventorySyncService>();
builder.Services.AddScoped<InventoryCacheService>();

// -----------------------------
//  CONTROLLERS + OPENAPI
// -----------------------------
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// -----------------------------
//  DATABASE MIGRATIONS + SEEDING
// -----------------------------
using (var scope = app.Services.CreateScope())
{
    var ctx = scope.ServiceProvider.GetRequiredService<LogiTrackContext>();
    try
    {
        Console.WriteLine("Applying EF Core migrations...");
        ctx.Database.Migrate(); // Apply pending migrations

        if (!ctx.InventoryItems.Any())
        {
            ctx.InventoryItems.AddRange(
                new InventoryItem("Widget", 10, "A1", 2.99),
                new InventoryItem("Gadget", 5, "B2", 9.49),
                new InventoryItem("Bolt", 100, "C3", 0.10)
            );
            ctx.SaveChanges();
            Console.WriteLine("✅ Seeded InventoryItems with sample data.");
        }

        Console.WriteLine($"✅ Database initialized with {ctx.InventoryItems.Count()} inventory items.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Database initialization failed: {ex.Message}");
    }
}

// -----------------------------
//  MIDDLEWARE PIPELINE
// -----------------------------
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// -----------------------------
//  SAMPLE ROUTE (Optional)
// -----------------------------
var summaries = new[] { "Freezing", "Mild", "Warm", "Hot", "Scorching" };
app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new
        {
            Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = summaries[Random.Shared.Next(summaries.Length)]
        });
    return forecast;
}).WithName("GetWeatherForecast");

app.Run();