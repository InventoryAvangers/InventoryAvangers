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

    [HttpGet("pending-users")]
    public async Task<IActionResult> GetPendingUsers()
    {
        if (UserRole != "owner" && UserRole != "manager")
            return StatusCode(403, new { success = false, message = "Forbidden" });

        var filter = Builders<User>.Filter.Eq(u => u.Status, "pending");
        if (!string.IsNullOrWhiteSpace(UserStoreId))
            filter &= Builders<User>.Filter.Eq(u => u.StoreId, UserStoreId);

        var users = await _db.Users.Find(filter)
            .SortByDescending(u => u.CreatedAt)
            .ToListAsync();

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

    [HttpPut("users/{id}/approve")]
    public async Task<IActionResult> ApproveUser(string id, [FromBody] ApproveUserRequest req)
    {
        if (UserRole != "owner" && UserRole != "manager")
            return StatusCode(403, new { success = false, message = "Forbidden" });

        var user = await _db.Users.Find(u => u.Id == id).FirstOrDefaultAsync();
        if (user == null)
            return NotFound(new { success = false, message = "User not found" });

        if (!string.IsNullOrWhiteSpace(UserStoreId) && user.StoreId != UserStoreId)
            return StatusCode(403, new { success = false, message = "Forbidden" });

        var updates = new List<UpdateDefinition<User>>
        {
            Builders<User>.Update.Set(u => u.Status, "approved")
        };

        if (!string.IsNullOrWhiteSpace(req.Role))
            updates.Add(Builders<User>.Update.Set(u => u.Role, req.Role));

        if (!string.IsNullOrWhiteSpace(req.StoreId))
            updates.Add(Builders<User>.Update.Set(u => u.StoreId, req.StoreId));

        await _db.Users.UpdateOneAsync(u => u.Id == id, Builders<User>.Update.Combine(updates));

        await _db.AuditLogs.InsertOneAsync(new AuditLog
        {
            ActorId = UserId!,
            TargetId = id,
            Action = "approve_user"
        });

        await _db.Notifications.InsertOneAsync(new Notification
        {
            UserId = id,
            Type = "account_approved",
            Title = "Account Approved",
            Message = "Your account has been approved."
        });

        return Ok(new { success = true });
    }

    [HttpPut("users/{id}/reject")]
    public async Task<IActionResult> RejectUser(string id)
    {
        var user = await _db.Users.Find(u => u.Id == id).FirstOrDefaultAsync();
        if (user == null)
            return NotFound(new { success = false });

        await _db.Users.UpdateOneAsync(u => u.Id == id,
            Builders<User>.Update.Set(u => u.Status, "rejected"));

        return Ok(new { success = true });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var filter = Builders<Approval>.Filter.Empty;

        if (UserRole != "owner" && UserRole != "manager")
            filter = Builders<Approval>.Filter.Eq(a => a.RequestedBy, UserId);

        var approvals = await _db.Approvals.Find(filter)
            .SortByDescending(a => a.CreatedAt)
            .ToListAsync();

        return Ok(approvals);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateApprovalRequest req)
    {
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

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateApproval(string id, [FromBody] UpdateApprovalRequest req)
    {
        var approval = await _db.Approvals.Find(a => a.Id == id).FirstOrDefaultAsync();
        if (approval == null)
            return NotFound();

        await _db.Approvals.UpdateOneAsync(a => a.Id == id,
            Builders<Approval>.Update
                .Set(a => a.Status, req.Status)
                .Set(a => a.ApprovedBy, UserId)
                .Set(a => a.UpdatedAt, DateTime.UtcNow));

        if (approval.Action == "delete_product" && req.Status == "approved")
        {
            var raw = _db.Approvals.Database.GetCollection<MongoDB.Bson.BsonDocument>("approvals");
            var doc = await raw.Find(Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", MongoDB.Bson.ObjectId.Parse(id))).FirstOrDefaultAsync();

            var productId = doc?["metadata"]?["productId"]?.ToString();

            if (!string.IsNullOrWhiteSpace(productId))
            {
                await _db.Products.DeleteOneAsync(p => p.Id == productId);
                await _db.Inventories.DeleteManyAsync(i => i.ProductId == productId);
            }
        }

        return Ok();
    }
}