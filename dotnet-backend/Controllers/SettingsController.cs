using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using InventoryAvengers.API.Data;
using InventoryAvengers.API.DTOs;
using InventoryAvengers.API.Models;

namespace InventoryAvengers.API.Controllers;

[ApiController]
[Route("api/settings")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly MongoDbContext _db;

    public SettingsController(MongoDbContext db) => _db = db;

    private string? UserId => User.FindFirst("id")?.Value;
    private string? UserRole => User.FindFirst("role")?.Value;
    private string? UserStoreId => User.FindFirst("storeId")?.Value;

    // GET /api/settings/profile
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var user = await _db.Users.Find(u => u.Id == UserId).FirstOrDefaultAsync();
        if (user == null) return NotFound(new { success = false, message = "User not found" });

        return Ok(new
        {
            success = true,
            data = new
            {
                user.Name,
                user.Email,
                displayName = user.DisplayName,
                avatar = user.Avatar,
                currency = user.Currency,
                user.Role
            }
        });
    }

    // PUT /api/settings/profile
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest req)
    {
        var validCurrencies = new[] { "INR", "USD", "EUR", "GBP" };
        var updates = new List<UpdateDefinition<User>>();
        if (req.DisplayName != null) updates.Add(Builders<User>.Update.Set(u => u.DisplayName, req.DisplayName));
        if (req.Avatar != null) updates.Add(Builders<User>.Update.Set(u => u.Avatar, req.Avatar));
        if (req.Currency != null && validCurrencies.Contains(req.Currency))
            updates.Add(Builders<User>.Update.Set(u => u.Currency, req.Currency));

        if (updates.Count == 0) return BadRequest(new { success = false, message = "No fields to update" });

        var user = await _db.Users.FindOneAndUpdateAsync(
            u => u.Id == UserId,
            Builders<User>.Update.Combine(updates),
            new FindOneAndUpdateOptions<User> { ReturnDocument = ReturnDocument.After });

        if (user == null) return NotFound(new { success = false, message = "User not found" });

        return Ok(new
        {
            success = true,
            message = "Profile updated",
            data = new
            {
                user.Name,
                user.Email,
                displayName = user.DisplayName,
                avatar = user.Avatar,
                currency = user.Currency,
                user.Role
            }
        });
    }

    // GET /api/settings/features
    [HttpGet("features")]
    public async Task<IActionResult> GetFeatures()
    {
        if (UserRole == "superuser")
        {
            return Ok(new
            {
                success = true,
                data = FeatureFlag.GetDefaults("pro")
            });
        }

        if (string.IsNullOrWhiteSpace(UserStoreId))
            return StatusCode(403, new { success = false, message = "No store associated with this account" });

        var store = await _db.Stores.Find(s => s.Id == UserStoreId).FirstOrDefaultAsync();
        var subscription = await _db.Subscriptions.Find(s => s.StoreId == UserStoreId).FirstOrDefaultAsync();
        var plan = subscription?.Plan ?? store?.Plan ?? "free";

        var flags = await _db.FeatureFlags.Find(f => f.StoreId == UserStoreId).FirstOrDefaultAsync();
        if (flags == null)
        {
            flags = new FeatureFlag
            {
                StoreId = UserStoreId,
                Features = FeatureFlag.GetDefaults(plan)
            };

            try
            {
                await _db.FeatureFlags.InsertOneAsync(flags);
            }
            catch
            {
                flags = await _db.FeatureFlags.Find(f => f.StoreId == UserStoreId).FirstOrDefaultAsync() ?? flags;
            }
        }

        return Ok(new
        {
            success = true,
            data = flags.Features
        });
    }
}
