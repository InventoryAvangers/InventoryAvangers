using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using InventoryAvengers.API.Data;
using InventoryAvengers.API.Models;
using InventoryAvengers.API.DTOs;

namespace InventoryAvengers.API.Services;

public class AuthService
{
    private readonly MongoDbContext _db;
    private readonly IConfiguration _config;
    private static readonly Regex PasswordRegex = new(@"^(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,}$");

    public AuthService(MongoDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public string GenerateToken(User user)
    {
        var secret = Environment.GetEnvironmentVariable("JWT_SECRET")
            ?? _config["Jwt:Secret"]
            ?? throw new InvalidOperationException("JWT secret not configured");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiresInStr = Environment.GetEnvironmentVariable("JWT_EXPIRES_IN")
            ?? _config["Jwt:ExpiresIn"] ?? "7d";
        var expires = ParseExpiry(expiresInStr);

        var claims = new List<Claim>
        {
            new("id", user.Id ?? ""),
            new("name", user.Name),
            new("email", user.Email),
            new("role", user.Role),
            new("storeId", user.StoreId ?? ""),
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.Add(expires),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static TimeSpan ParseExpiry(string expiry)
    {
        if (expiry.EndsWith('d') && int.TryParse(expiry[..^1], out var days))
            return TimeSpan.FromDays(days);
        if (expiry.EndsWith('h') && int.TryParse(expiry[..^1], out var hours))
            return TimeSpan.FromHours(hours);
        return TimeSpan.FromDays(7);
    }

    public bool ValidatePassword(string password) => PasswordRegex.IsMatch(password);

    public string HashPassword(string password) => BCrypt.Net.BCrypt.HashPassword(password, 10);

    public bool VerifyPassword(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
}
