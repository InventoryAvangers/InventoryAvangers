using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using InventoryAvengers.API.Data;
using InventoryAvengers.API.Models;

namespace InventoryAvengers.API.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly MongoDbContext _db;

    public NotificationsController(MongoDbContext db) => _db = db;

    private string? UserId => User.FindFirst("id")?.Value;

    // GET /api/notifications
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var notifications = await _db.Notifications
            .Find(n => n.UserId == UserId)
            .SortByDescending(n => n.CreatedAt)
            .Limit(50)
            .ToListAsync();

        var unreadCount = await _db.Notifications
            .CountDocumentsAsync(n => n.UserId == UserId && !n.Read);

        return Ok(new { success = true, data = notifications, unreadCount });
    }

    // PUT /api/notifications/read-all
    [HttpPut("read-all")]
    public async Task<IActionResult> ReadAll()
    {
        await _db.Notifications.UpdateManyAsync(
            n => n.UserId == UserId && !n.Read,
            Builders<Notification>.Update.Set(n => n.Read, true));
        return Ok(new { success = true, message = "All notifications marked as read" });
    }

    // PUT /api/notifications/:id/read
    [HttpPut("{id}/read")]
    public async Task<IActionResult> Read(string id)
    {
        var notification = await _db.Notifications.FindOneAndUpdateAsync(
            n => n.Id == id && n.UserId == UserId,
            Builders<Notification>.Update.Set(n => n.Read, true),
            new FindOneAndUpdateOptions<Notification> { ReturnDocument = ReturnDocument.After });

        if (notification == null)
            return NotFound(new { success = false, message = "Notification not found" });

        return Ok(new { success = true, data = notification });
    }
}
