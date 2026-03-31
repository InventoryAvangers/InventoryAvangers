using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using InventoryAvengers.API.Data;
using InventoryAvengers.API.DTOs;
using InventoryAvengers.API.Models;

namespace InventoryAvengers.API.Controllers;

[ApiController]
[Route("api/messages")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly MongoDbContext _db;

    public MessagesController(MongoDbContext db) => _db = db;

    private string? UserId   => User.FindFirst("id")?.Value;
    private string? UserRole => User.FindFirst("role")?.Value;
    private string? UserStoreId => User.FindFirst("storeId")?.Value is { Length: > 0 } s ? s : null;

    // POST /api/messages — send a message
    [HttpPost]
    public async Task<IActionResult> Send([FromBody] CreateMessageRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Subject) || string.IsNullOrWhiteSpace(req.Body))
            return BadRequest(new { success = false, message = "Subject and body are required" });

        string? recipientId = req.ToId;
        string recipientRole = "superuser";

        if (UserRole == "superuser")
        {
            if (string.IsNullOrWhiteSpace(req.ToId))
                return BadRequest(new { success = false, message = "Recipient (toId) is required for superuser replies" });

            var recipient = await _db.Users.Find(u => u.Id == req.ToId).FirstOrDefaultAsync();
            if (recipient == null)
                return NotFound(new { success = false, message = "Recipient not found" });
            recipientRole = recipient.Role;
        }
        else
        {
            var su = await _db.Users
                .Find(u => u.Role == "superuser" && u.Status == "approved")
                .FirstOrDefaultAsync();
            if (su == null)
                return StatusCode(503, new { success = false, message = "No superuser available to receive your message" });
            recipientId = su.Id;
        }

        var message = new Message
        {
            FromId          = UserId,
            FromRole        = UserRole,
            ToId            = recipientId,
            ToRole          = recipientRole,
            StoreId         = UserStoreId,
            Subject         = req.Subject,
            Body            = req.Body,
            ParentMessageId = req.ParentMessageId
        };
        await _db.Messages.InsertOneAsync(message);

        // Create bell notification for the recipient
        if (UserRole != "superuser" && !string.IsNullOrEmpty(recipientId))
        {
            await _db.Notifications.InsertOneAsync(new Notification
            {
                UserId  = recipientId,
                Type    = "support_message",
                Title   = "New Support Message",
                Message = req.Subject
            });
        }

        return StatusCode(201, new { success = true, data = message });
    }

    // GET /api/messages/inbox — messages received by the current user
    [HttpGet("inbox")]
    public async Task<IActionResult> GetInbox()
    {
        var filter = UserRole == "superuser"
            ? Builders<Message>.Filter.And(
                Builders<Message>.Filter.Eq(m => m.ToId, UserId),
                Builders<Message>.Filter.Eq(m => m.IsBroadcast, false))
            : Builders<Message>.Filter.Eq(m => m.ToId, UserId);

        var messages = await _db.Messages.Find(filter)
            .SortByDescending(m => m.SentAt)
            .Limit(100)
            .ToListAsync();

        return Ok(new { success = true, data = messages });
    }

    // GET /api/messages — alias for inbox (backward compat)
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var messages = await _db.Messages
            .Find(m => m.ToId == UserId)
            .SortByDescending(m => m.SentAt)
            .Limit(100)
            .ToListAsync();

        return Ok(new { success = true, data = messages });
    }

    // GET /api/messages/sent — messages sent by the current user
    [HttpGet("sent")]
    public async Task<IActionResult> GetSent()
    {
        var messages = await _db.Messages
            .Find(m => m.FromId == UserId)
            .SortByDescending(m => m.SentAt)
            .Limit(100)
            .ToListAsync();

        return Ok(new { success = true, data = messages });
    }

    // GET /api/messages/all — superuser: view all support messages
    [HttpGet("all")]
    public async Task<IActionResult> GetAllMessages()
    {
        if (UserRole != "superuser")
            return StatusCode(403, new { success = false, message = "Forbidden" });

        var messages = await _db.Messages
            .Find(m => m.ToRole == "superuser")
            .SortByDescending(m => m.SentAt)
            .Limit(200)
            .ToListAsync();

        return Ok(new { success = true, data = messages });
    }

    // PATCH /api/messages/:id/read — mark a message as read
    [HttpPatch("{id}/read")]
    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkRead(string id)
    {
        var message = await _db.Messages.FindOneAndUpdateAsync(
            m => m.Id == id && m.ToId == UserId,
            Builders<Message>.Update
                .Set(m => m.Read, true)
                .Set(m => m.ReadAt, DateTime.UtcNow),
            new FindOneAndUpdateOptions<Message> { ReturnDocument = ReturnDocument.After });

        if (message == null) return NotFound(new { success = false, message = "Message not found" });
        return Ok(new { success = true, data = message });
    }
}
