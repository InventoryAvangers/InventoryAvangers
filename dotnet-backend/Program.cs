using System.Text;
using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using InventoryAvengers.API.Data;
using InventoryAvengers.API.Middleware;
using InventoryAvengers.API.Services;

var builder = WebApplication.CreateBuilder(args);

// Disable default claim mapping to keep JWT claim names (required for "role" and "id" to work correctly)
System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

var envPath = Path.Combine(Directory.GetCurrentDirectory(), "..", ".env");
if (File.Exists(envPath))
{
    foreach (var line in File.ReadAllLines(envPath))
    {
        var parts = line.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2)
        {
            Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
        }
    }
}

// ─── Services ───────────────────────────────────────────────────────────────

builder.Services.AddSingleton<MongoDbContext>();
builder.Services.AddScoped<AuthService>();

// Controllers + JSON serialisation
builder.Services.AddControllers(opts =>
    {
        // Register FeatureCheck as a global convention-based filter (can also be used per-controller)
    })
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        opts.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// OpenAPI / Swagger
builder.Services.AddOpenApi();

// CORS — allow all origins (tighten in production)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

// JWT Authentication
var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET")
    ?? builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("JWT_SECRET not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnChallenge = context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                return context.Response.WriteAsync("{\"success\":false,\"message\":\"Not authorized, no token\"}");
            },
            OnForbidden = context =>
            {
                context.Response.StatusCode = 403;
                context.Response.ContentType = "application/json";
                return context.Response.WriteAsync("{\"success\":false,\"message\":\"Forbidden: insufficient permissions\"}");
            }
        };
    });

builder.Services.AddAuthorization();

// Rate limiting
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

// ─── App ────────────────────────────────────────────────────────────────────

var app = builder.Build();

// ─── Seed demo accounts on every startup (mirrors Node seedOwner.js) ────────
try
{
    var dbForSeed = app.Services.GetRequiredService<MongoDbContext>();
    await SeedService.SeedAsync(dbForSeed);
}
catch (Exception ex)
{
    Console.WriteLine($"[Seed] Warning: {ex.Message}");
}

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseIpRateLimiting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Serve static frontend (dist/) if present
var distPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "frontend", "dist");
if (Directory.Exists(distPath))
{
    app.UseStaticFiles(new StaticFileOptions { FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(distPath) });
    app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(distPath) });
}

app.Run();
