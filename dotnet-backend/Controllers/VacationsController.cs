using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using InventoryAvengers.API.Data;
using InventoryAvengers.API.Models;

namespace InventoryAvengers.API.Controllers;

[ApiController]
[Route("api/vacations")]
[Authorize]
public class VacationsController : ControllerBase
{
    private readonly MongoDbContext _db;

    public VacationsController(MongoDbContext db) => _db = db;

    private string? UserId    => User.FindFirst("id")?.Value;
    private string? UserRole  => User.FindFirst("role")?.Value;
    private string? UserStoreId => User.FindFirst("storeId")?.Value is { Length: > 0 } s ? s : null;

    // ── EMPLOYEE: submit a vacation request ──────────────────────────────────

    // POST /api/vacations
    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] VacationRequestDto req)
    {
        if (UserRole != "staff" && UserRole != "manager")
            return StatusCode(403, new { success = false, message = "Only staff and managers can submit vacation requests" });

        if (req.StartDate >= req.EndDate)
            return BadRequest(new { success = false, message = "End date must be after start date" });

        if (string.IsNullOrWhiteSpace(UserStoreId))
            return BadRequest(new { success = false, message = "No store associated with your account" });

        var vr = new VacationRequest
        {
            EmployeeId = UserId!,
            StoreId    = UserStoreId,
            StartDate  = req.StartDate.ToUniversalTime(),
            EndDate    = req.EndDate.ToUniversalTime(),
            Reason     = req.Reason?.Trim() ?? string.Empty,
            Status     = "pending"
        };

        await _db.VacationRequests.InsertOneAsync(vr);

        // Notify owner/manager of the store
        var storeOwner = await _db.Users.Find(u => u.StoreId == UserStoreId && u.Role == "owner").FirstOrDefaultAsync();
        if (storeOwner?.Id != null)
        {
            await _db.Notifications.InsertOneAsync(new Notification
            {
                UserId  = storeOwner.Id,
                Type    = "vacation_request",
                Title   = "New Vacation Request",
                Message = $"A team member has requested time off from {vr.StartDate:MMM d} to {vr.EndDate:MMM d}."
            });
        }

        return StatusCode(201, new { success = true, message = "Vacation request submitted.", data = vr });
    }

    // GET /api/vacations/my — employee sees their own requests
    [HttpGet("my")]
    public async Task<IActionResult> GetMine()
    {
        if (UserRole != "staff" && UserRole != "manager")
            return StatusCode(403, new { success = false, message = "Forbidden" });

        var requests = await _db.VacationRequests
            .Find(v => v.EmployeeId == UserId)
            .SortByDescending(v => v.CreatedAt)
            .ToListAsync();

        return Ok(new { success = true, data = requests });
    }

    // ── OWNER/MANAGER: see all requests for their store ──────────────────────

    // GET /api/vacations — owner/manager sees all pending requests for their store
    [HttpGet]
    public async Task<IActionResult> GetForStore([FromQuery] string? status)
    {
        if (UserRole != "owner" && UserRole != "manager")
            return StatusCode(403, new { success = false, message = "Forbidden" });

        if (string.IsNullOrWhiteSpace(UserStoreId))
            return Ok(new { success = true, data = new List<object>() });

        var filter = Builders<VacationRequest>.Filter.Eq(v => v.StoreId, UserStoreId);
        if (!string.IsNullOrWhiteSpace(status))
            filter &= Builders<VacationRequest>.Filter.Eq(v => v.Status, status);

        var requests = await _db.VacationRequests.Find(filter)
            .SortByDescending(v => v.CreatedAt)
            .ToListAsync();

        // Populate employee names
        var empIds  = requests.Select(r => r.EmployeeId).Distinct().ToList();
        var empList = await _db.Users.Find(Builders<User>.Filter.In(u => u.Id, empIds)).ToListAsync();
        var empMap  = empList.ToDictionary(u => u.Id!, u => new { _id = u.Id, name = u.Name, email = u.Email, role = u.Role });

        var result = requests.Select(r => new
        {
            r.Id,
            r.StartDate,
            r.EndDate,
            r.Reason,
            r.Status,
            r.ReviewNote,
            r.ReviewedAt,
            r.CreatedAt,
            employee = empMap.TryGetValue(r.EmployeeId, out var emp) ? emp : null
        });

        return Ok(new { success = true, data = result });
    }

    // PUT /api/vacations/:id/approve
    [HttpPut("{id}/approve")]
    public async Task<IActionResult> Approve(string id, [FromBody] ReviewDto? req)
    {
        if (UserRole != "owner" && UserRole != "manager")
            return StatusCode(403, new { success = false, message = "Forbidden" });

        var vr = await _db.VacationRequests.Find(v => v.Id == id && v.StoreId == UserStoreId).FirstOrDefaultAsync();
        if (vr == null) return NotFound(new { success = false, message = "Request not found" });

        await _db.VacationRequests.UpdateOneAsync(
            v => v.Id == id,
            Builders<VacationRequest>.Update
                .Set(v => v.Status, "approved")
                .Set(v => v.ReviewNote, req?.Note)
                .Set(v => v.ReviewedBy, UserId)
                .Set(v => v.ReviewedAt, DateTime.UtcNow));

        await _db.Notifications.InsertOneAsync(new Notification
        {
            UserId  = vr.EmployeeId,
            Type    = "vacation_approved",
            Title   = "Vacation Request Approved",
            Message = $"Your request from {vr.StartDate:MMM d} to {vr.EndDate:MMM d} has been approved."
        });

        return Ok(new { success = true, message = "Request approved." });
    }

    // PUT /api/vacations/:id/decline
    [HttpPut("{id}/decline")]
    public async Task<IActionResult> Decline(string id, [FromBody] ReviewDto? req)
    {
        if (UserRole != "owner" && UserRole != "manager")
            return StatusCode(403, new { success = false, message = "Forbidden" });

        var vr = await _db.VacationRequests.Find(v => v.Id == id && v.StoreId == UserStoreId).FirstOrDefaultAsync();
        if (vr == null) return NotFound(new { success = false, message = "Request not found" });

        await _db.VacationRequests.UpdateOneAsync(
            v => v.Id == id,
            Builders<VacationRequest>.Update
                .Set(v => v.Status, "declined")
                .Set(v => v.ReviewNote, req?.Note)
                .Set(v => v.ReviewedBy, UserId)
                .Set(v => v.ReviewedAt, DateTime.UtcNow));

        await _db.Notifications.InsertOneAsync(new Notification
        {
            UserId  = vr.EmployeeId,
            Type    = "vacation_declined",
            Title   = "Vacation Request Declined",
            Message = $"Your request from {vr.StartDate:MMM d} to {vr.EndDate:MMM d} has been declined.{(string.IsNullOrWhiteSpace(req?.Note) ? "" : $" Note: {req!.Note}")}"
        });

        return Ok(new { success = true, message = "Request declined." });
    }
}

// ── DTOs ─────────────────────────────────────────────────────────────────────

public class VacationRequestDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate   { get; set; }
    public string?  Reason    { get; set; }
}

public class ReviewDto
{
    public string? Note { get; set; }
}
