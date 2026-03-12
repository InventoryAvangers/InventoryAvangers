using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using InventoryAvengers.API.Data;
using InventoryAvengers.API.DTOs;
using InventoryAvengers.API.Models;

namespace InventoryAvengers.API.Controllers;

[ApiController]
[Route("api/approvals")]
[Authorize]
public class ApprovalsController : ControllerBase
{
    private readonly MongoDbContext _db;

    public ApprovalsController(MongoDbContext db) => _db = db;

    private string? UserId => User.FindFirst("id")?.Value;
    private string? UserRole => User.FindFirst("role")?.Value;
    private string? UserStoreId => User.FindFirst("storeId")?.Value is { Length: > 0 } s ? s : null;

    // GET /api/approvals/pending-users
    [HttpGet("pending-users")]
    public async Task<IActionResult> GetPendingUsers()
    {
        if (UserRole != "owner" && UserRole != "manager")
            return StatusCode(403, new { success = false, message = "Forbidden" });

        var filter = Builders<User>.Filter.Eq(u => u.Status, "pending");
        if (!string.IsNullOrWhiteSpace(UserStoreId))
            filter &= Builders<User>.Filter.Eq(u => u.StoreId, UserStoreId);

        var users = await _db.Users.Find(filter).SortByDescending(u => u.CreatedAt).ToListAsync();
        var result = users.Select(u => new
        {
            _id = u.Id,
            u.Name,
            u.Email,
            u.Role,
            u.Status,
            u.StoreId,
            u.CreatedAt
        });
        return Ok(new { success = true, data = result });
    }

    // PUT /api/approvals/users/:id/approve
    [HttpPut("users/{id}/approve")]
    public async Task<IActionResult> ApproveUser(string id, [FromBody] ApproveUserRequest req)
    {
        if (UserRole != "owner" && UserRole != "manager")
            return StatusCode(403, new { success = false, message = "Forbidden" });

        var user = await _db.Users.Find(u => u.Id == id).FirstOrDefaultAsync();
        if (user == null) return NotFound(new { success = false, message = "User not found" });

        if (!string.IsNullOrWhiteSpace(UserStoreId) && !string.IsNullOrWhiteSpace(user.StoreId)
            && user.StoreId != UserStoreId)
            return StatusCode(403, new { success = false, message = "You can only approve users for your own store" });

        if (UserRole == "manager")
        {
            if (!string.IsNullOrWhiteSpace(req.Role) && req.Role != "staff")
                return StatusCode(403, new { success = false, message = "Managers can only approve staff accounts" });
            if (!string.IsNullOrWhiteSpace(req.StoreId) && req.StoreId != UserStoreId)
                return StatusCode(403, new { success = false, message = "Managers can only approve users for their own store" });
        }

        var updates = new List<UpdateDefinition<User>>
        {
            Builders<User>.Update.Set(u => u.Status, "approved")
        };
        if (!string.IsNullOrWhiteSpace(req.Role)) updates.Add(Builders<User>.Update.Set(u => u.Role, req.Role));
        if (!string.IsNullOrWhiteSpace(req.StoreId)) updates.Add(Builders<User>.Update.Set(u => u.StoreId, req.StoreId));

        await _db.Users.UpdateOneAsync(u => u.Id == id, Builders<User>.Update.Combine(updates));

        await _db.AuditLogs.InsertOneAsync(new AuditLog
        {
            ActorId = UserId!,
            TargetId = id,
            Action = "approve_user",
            Metadata = new { role = req.Role ?? user.Role, storeId = req.StoreId ?? user.StoreId },
            StoreId = req.StoreId ?? user.StoreId
        });

        await _db.Notifications.InsertOneAsync(new Notification
        {
            UserId = id,
            Type = "account_approved",
            Title = "Account Approved",
            Message = "Your account has been approved. You can now log in."
        });

        return Ok(new { success = true, message = "User approved", data = new { id, status = "approved" } });
    }

    // PUT /api/approvals/users/:id/reject
    [HttpPut("users/{id}/reject")]
    public async Task<IActionResult> RejectUser(string id)
    {
        if (UserRole != "owner" && UserRole != "manager")
            return StatusCode(403, new { success = false, message = "Forbidden" });

        var user = await _db.Users.Find(u => u.Id == id).FirstOrDefaultAsync();
        if (user == null) return NotFound(new { success = false, message = "User not found" });

        await _db.Users.UpdateOneAsync(u => u.Id == id, Builders<User>.Update.Set(u => u.Status, "rejected"));

        await _db.AuditLogs.InsertOneAsync(new AuditLog
        {
            ActorId = UserId!,
            TargetId = id,
            Action = "reject_user"
        });

        await _db.Notifications.InsertOneAsync(new Notification
        {
            UserId = id,
            Type = "account_rejected",
            Title = "Account Rejected",
            Message = "Your account registration has been rejected. Please contact an administrator."
        });

        return Ok(new { success = true, message = "User rejected" });
    }

    // GET /api/approvals
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        FilterDefinition<Approval> filter;
        if (UserRole == "owner" || UserRole == "manager")
        {
            filter = Builders<Approval>.Filter.Empty;
            if (!string.IsNullOrWhiteSpace(UserStoreId))
                filter = Builders<Approval>.Filter.Eq(a => a.StoreId, UserStoreId);
        }
        else
        {
            filter = Builders<Approval>.Filter.Eq(a => a.RequestedBy, UserId);
        }

        var approvals = await _db.Approvals.Find(filter)
            .SortByDescending(a => a.CreatedAt)
            .ToListAsync();

        return Ok(approvals);
    }

    // POST /api/approvals
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateApprovalRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Action) || string.IsNullOrWhiteSpace(req.Description))
            return BadRequest(new { message = "Action and description required" });

        var approval = new Approval
        {
            Action = req.Action,
            Description = req.Description,
            RequestedBy = UserId,
            StoreId = UserStoreId,
            Metadata = req.Metadata
        };
        await _db.Approvals.InsertOneAsync(approval);
        return StatusCode(201, approval);
    }

    // PUT /api/approvals/:id
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateApproval(string id, [FromBody] UpdateApprovalRequest req)
    {
        if (UserRole != "owner" && UserRole != "manager")
            return StatusCode(403, new { success = false, message = "Forbidden" });

        if (!new[] { "approved", "rejected" }.Contains(req.Status))
            return BadRequest(new { message = "Status must be approved or rejected" });

        var approval = await _db.Approvals.Find(a => a.Id == id).FirstOrDefaultAsync();
        if (approval == null) return NotFound(new { message = "Approval not found" });

        if (!string.IsNullOrWhiteSpace(UserStoreId) && !string.IsNullOrWhiteSpace(approval.StoreId)
            && approval.StoreId != UserStoreId)
            return StatusCode(403, new { message = "Forbidden: approval belongs to a different store" });

        await _db.Approvals.UpdateOneAsync(a => a.Id == id,
            Builders<Approval>.Update
                .Set(a => a.Status, req.Status)
                .Set(a => a.ApprovedBy, UserId)
                .Set(a => a.UpdatedAt, DateTime.UtcNow));

        if (approval.Action == "delete_product" && req.Status == "approved")
        {
            var metadata = approval.Metadata as System.Text.Json.JsonElement?;
            var productId = metadata?.GetProperty("productId").GetString();
            if (!string.IsNullOrWhiteSpace(productId))
                await _db.Products.DeleteOneAsync(p => p.Id == productId);
        }

        var updated = await _db.Approvals.Find(a => a.Id == id).FirstOrDefaultAsync();
        return Ok(updated);
    }
}
