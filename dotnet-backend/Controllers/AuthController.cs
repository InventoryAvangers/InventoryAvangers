using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using InventoryAvengers.API.Data;
using InventoryAvengers.API.DTOs;
using InventoryAvengers.API.Models;
using InventoryAvengers.API.Services;

namespace InventoryAvengers.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly MongoDbContext _db;
    private readonly AuthService _authService;

    public AuthController(MongoDbContext db, AuthService authService)
    {
        _db = db;
        _authService = authService;
    }

    // POST /api/auth/login
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { success = false, message = "Email and password required" });

        // Match email case-insensitively to handle old accounts stored non-lowercase
        var emailLower = req.Email.ToLower();
        var user = await _db.Users
            .Find(u => u.Email == emailLower || u.Email == req.Email)
            .FirstOrDefaultAsync();
        if (user == null)
            return Unauthorized(new { success = false, message = "Invalid credentials" });

        if (!_authService.VerifyPassword(req.Password, user.PasswordHash))
            return Unauthorized(new { success = false, message = "Invalid credentials" });

        if (user.Status == "pending")
            return StatusCode(403, new { success = false, message = "Your account is pending approval" });
        if (user.Status == "rejected")
            return StatusCode(403, new { success = false, message = "Your account registration was rejected" });
        if (user.Status == "suspended")
            return StatusCode(403, new { success = false, message = "Your account has been suspended" });
        if (user.Status == "deactivated")
            return StatusCode(403, new { success = false, message = "Your account has been deactivated" });
        // Allow login for "approved" or any legacy accounts with null/empty status
        // (old Node.js accounts may not have a status field set)

        await _db.Users.UpdateOneAsync(
            u => u.Id == user.Id,
            Builders<User>.Update.Set(u => u.LastLogin, DateTime.UtcNow));

        var token = _authService.GenerateToken(user);

        return Ok(new
        {
            success = true,
            token,
            user = new
            {
                id = user.Id,
                name = user.Name,
                email = user.Email,
                role = user.Role,
                storeId = user.StoreId,
                mustChangePassword = user.MustChangePassword,
                displayName = user.DisplayName,
                avatar = user.Avatar,
                currency = user.Currency
            }
        });
    }

    // POST /api/auth/register
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { success = false, message = "Name, email, and password required" });

        if (!_authService.ValidatePassword(req.Password))
            return BadRequest(new { success = false, message = "Password must be at least 8 characters with one uppercase letter, one number, and one special character" });

        if (req.Role != null && new[] { "owner", "superuser" }.Contains(req.Role))
            return StatusCode(403, new { success = false, message = "Owner and Superuser accounts cannot be self-registered" });

        var allowedRoles = new[] { "manager", "staff" };
        var assignedRole = req.Role != null && allowedRoles.Contains(req.Role) ? req.Role : "staff";

        string? assignedStoreId = null;
        if (!string.IsNullOrWhiteSpace(req.StoreId))
        {
            var store = await _db.Stores.Find(s => s.Id == req.StoreId).FirstOrDefaultAsync();
            if (store == null)
                return BadRequest(new { success = false, message = "Selected store not found" });
            if (!new[] { "active", "trial" }.Contains(store.Status))
                return BadRequest(new { success = false, message = "Selected store is not currently active" });
            assignedStoreId = store.Id;
        }

        var existing = await _db.Users.Find(u => u.Email == req.Email.ToLower()).FirstOrDefaultAsync();
        if (existing != null)
            return BadRequest(new { success = false, message = "Email already in use" });

        var user = new User
        {
            Name = req.Name,
            Email = req.Email.ToLower(),
            PasswordHash = _authService.HashPassword(req.Password),
            Role = assignedRole,
            StoreId = assignedStoreId,
            Status = "pending"
        };
        await _db.Users.InsertOneAsync(user);

        // Notify managers
        try
        {
            var managerFilter = Builders<User>.Filter.In(u => u.Role, new[] { "owner", "manager" })
                & Builders<User>.Filter.Eq(u => u.Status, "approved");
            if (assignedStoreId != null)
                managerFilter &= Builders<User>.Filter.Eq(u => u.StoreId, assignedStoreId);

            var managers = await _db.Users.Find(managerFilter).ToListAsync();
            var notifications = managers.Select(m => new Notification
            {
                UserId = m.Id!,
                Type = "new_registration",
                Title = "New Registration Request",
                Message = $"{user.Name} ({user.Email}) has registered and is awaiting approval.",
                Metadata = new { userId = user.Id }
            }).ToList();

            if (notifications.Count > 0)
                await _db.Notifications.InsertManyAsync(notifications);
        }
        catch { /* non-blocking */ }

        return StatusCode(201, new
        {
            success = true,
            message = "Registration successful. Your registration is pending approval by your store administrator.",
            user = new { id = user.Id, name = user.Name, email = user.Email, status = user.Status }
        });
    }

    // POST /api/auth/logout
    [Authorize]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        return Ok(new { success = true, message = "Logged out successfully" });
    }

    // PUT /api/auth/change-password
    [Authorize]
    [HttpPut("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.CurrentPassword) || string.IsNullOrWhiteSpace(req.NewPassword))
            return BadRequest(new { success = false, message = "Current password and new password required" });

        if (!_authService.ValidatePassword(req.NewPassword))
            return BadRequest(new { success = false, message = "New password must be at least 8 characters with one uppercase letter, one number, and one special character" });

        var userId = User.FindFirst("id")?.Value;
        var user = await _db.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
        if (user == null)
            return NotFound(new { success = false, message = "User not found" });

        if (!_authService.VerifyPassword(req.CurrentPassword, user.PasswordHash))
            return BadRequest(new { success = false, message = "Current password is incorrect" });

        var newHash = _authService.HashPassword(req.NewPassword);
        await _db.Users.UpdateOneAsync(u => u.Id == userId,
            Builders<User>.Update
                .Set(u => u.PasswordHash, newHash)
                .Set(u => u.MustChangePassword, false));

        await _db.AuditLogs.InsertOneAsync(new AuditLog
        {
            ActorId = userId!,
            TargetId = userId,
            Action = "change_password",
            StoreId = user.StoreId
        });

        return Ok(new { success = true, message = "Password changed successfully" });
    }

    // POST /api/auth/forgot
    [HttpPost("forgot")]
    public IActionResult ForgotPassword()
    {
        return Ok(new { success = true, message = "Password reset email sent (not implemented)" });
    }

    // POST /api/auth/access-request
    [HttpPost("access-request")]
    public async Task<IActionResult> AccessRequest([FromBody] AccessRequestDto req)
    {
        if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Email))
            return BadRequest(new { success = false, message = "Name and email are required" });

        if (string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { success = false, message = "Password is required" });

        if (req.Password.Length < 8 || !req.Password.Any(char.IsDigit))
            return BadRequest(new { success = false, message = "Password must be at least 8 characters and contain at least one number" });

        if (req.Password != req.ConfirmPassword)
            return BadRequest(new { success = false, message = "Passwords do not match" });

        var existing = await _db.AccessRequests
            .Find(r => r.Email == req.Email.ToLower() && r.Status == "pending")
            .FirstOrDefaultAsync();
        if (existing != null)
            return BadRequest(new { success = false, message = "A pending request with this email already exists" });

        var passwordHash = _authService.HashPassword(req.Password);

        var request = new AccessRequest
        {
            Name = req.Name,
            Email = req.Email.ToLower(),
            BusinessName = req.BusinessName ?? string.Empty,
            Message = req.Message ?? string.Empty,
            PasswordHash = passwordHash,
            StoreId = string.IsNullOrWhiteSpace(req.StoreId) ? null : req.StoreId
        };
        await _db.AccessRequests.InsertOneAsync(request);

        return StatusCode(201, new
        {
            success = true,
            message = "Access request submitted. You will be contacted when approved.",
            id = request.Id
        });
    }
}
