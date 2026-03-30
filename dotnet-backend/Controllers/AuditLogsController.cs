using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using InventoryAvengers.API.Data;
using InventoryAvengers.API.Models;

namespace InventoryAvengers.API.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize]
public class AuditLogsController : ControllerBase
{
    private readonly MongoDbContext _db;

    public AuditLogsController(MongoDbContext db) => _db = db;

    private string? UserRole => User.FindFirst("role")?.Value;
    private string? UserStoreId => User.FindFirst("storeId")?.Value is { Length: > 0 } s ? s : null;

    // GET /api/audit-logs
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int limit = 50)
    {
        if (UserRole != "owner" && UserRole != "manager")
            return StatusCode(403, new { success = false, message = "Forbidden" });

        if (string.IsNullOrWhiteSpace(UserStoreId))
            return Ok(new { success = true, data = new List<AuditLog>(), total = 0, page = 1, pages = 1 });

        var filter = Builders<AuditLog>.Filter.Eq(a => a.StoreId, UserStoreId);
        var skip = (page - 1) * limit;

        var logs = await _db.AuditLogs.Find(filter)
            .SortByDescending(a => a.CreatedAt)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync();

        var total = await _db.AuditLogs.CountDocumentsAsync(filter);
        var pages = (int)Math.Ceiling(total / (double)limit);

        // Fetch users to populate ActorId and TargetId
        var userIds = logs.Select(l => l.ActorId)
            .Concat(logs.Select(l => l.TargetId).Where(id => !string.IsNullOrEmpty(id)))
            .Distinct()
            .ToList();

        var users = await _db.Users.Find(Builders<User>.Filter.In(u => u.Id, userIds)).ToListAsync();
        var userMap = users.ToDictionary(u => u.Id!, u => new { _id = u.Id, name = u.Name, email = u.Email });

        var annotatedLogs = logs.Select(l => new
        {
            _id = l.Id,
            action = l.Action,
            metadata = l.Metadata,
            createdAt = l.CreatedAt,
            actorId = userMap.TryGetValue(l.ActorId, out var actor) ? actor : null,
            targetId = l.TargetId != null && userMap.TryGetValue(l.TargetId, out var target) ? target : null
        }).ToList();

        return Ok(new { success = true, data = annotatedLogs, total, page, pages });
    }
}
