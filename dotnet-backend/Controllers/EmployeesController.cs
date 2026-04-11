using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using InventoryAvengers.API.Data;
using InventoryAvengers.API.DTOs;
using InventoryAvengers.API.Middleware;
using InventoryAvengers.API.Models;

namespace InventoryAvengers.API.Controllers;

[ApiController]
[Route("api/employees")]
[Authorize]
[FeatureCheck("employees")]
public class EmployeesController : ControllerBase
{
    private readonly MongoDbContext _db;

    public EmployeesController(MongoDbContext db) => _db = db;

    private string? UserId => User.FindFirst("id")?.Value;
    private string? UserRole => User.FindFirst("role")?.Value;
    private string? UserName => User.FindFirst("name")?.Value;
    private string? UserStoreId => User.FindFirst("storeId")?.Value is { Length: > 0 } s ? s : null;

    // GET /api/employees
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        FilterDefinition<User> filter;
        if (UserRole == "manager")
        {
            if (string.IsNullOrWhiteSpace(UserStoreId)) return Ok(new { success = true, data = new List<User>() });
            filter = Builders<User>.Filter.In(u => u.Role, new[] { "manager", "staff" })
                & Builders<User>.Filter.Eq(u => u.StoreId, UserStoreId);
        }
        else if (UserRole == "owner")
        {
            filter = Builders<User>.Filter.In(u => u.Role, new[] { "manager", "staff" });
            if (!string.IsNullOrWhiteSpace(UserStoreId))
                filter &= Builders<User>.Filter.Eq(u => u.StoreId, UserStoreId);
        }
        else
        {
            return StatusCode(403, new { success = false, message = "Forbidden" });
        }

        var employees = await _db.Users.Find(filter).SortBy(u => u.Name).ToListAsync();
        var result = employees.Select(e => new
        {
            _id = e.Id,
            id = e.Id,
            e.Name,
            e.Email,
            e.Role,
            e.Status,
            e.StoreId,
            e.MustChangePassword,
            e.LastLogin,
            e.CreatedAt,
            e.DisplayName,
            e.Avatar,
            e.Currency
        });
        return Ok(new { success = true, data = result });
    }

    // GET /api/employees/:id
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        if (UserRole != "owner" && UserRole != "manager")
            return StatusCode(403, new { success = false, message = "Forbidden" });

        var employee = await _db.Users.Find(u => u.Id == id).FirstOrDefaultAsync();
        if (employee == null) return NotFound(new { success = false, message = "Employee not found" });

        if (UserRole == "manager" && employee.StoreId != UserStoreId)
            return StatusCode(403, new { success = false, message = "Forbidden: cross-store access denied" });

        // Populate storeId as an object
        object? storeObj = null;
        if (!string.IsNullOrWhiteSpace(employee.StoreId))
        {
            var store = await _db.Stores.Find(s => s.Id == employee.StoreId).FirstOrDefaultAsync();
            if (store != null)
                storeObj = new { _id = store.Id, name = store.Name, code = store.Code };
        }

        return Ok(new { success = true, data = new
        {
            _id = employee.Id,
            id = employee.Id,
            employee.Name,
            employee.Email,
            employee.Role,
            employee.Status,
            storeId = storeObj,
            createdAt = employee.CreatedAt,
            lastLogin = employee.LastLogin
        }});
    }

    // PUT /api/employees/:id/promote
    [HttpPut("{id}/promote")]
    public async Task<IActionResult> Promote(string id)
    {
        if (UserRole != "owner" && UserRole != "manager")
            return StatusCode(403, new { success = false, message = "Forbidden" });

        if (UserRole == "manager")
            return StatusCode(403, new { success = false, message = "Managers cannot promote employees to Manager role" });

        var employee = await _db.Users.Find(u => u.Id == id).FirstOrDefaultAsync();
        if (employee == null) return NotFound(new { success = false, message = "Employee not found" });
        if (employee.Role != "staff")
            return BadRequest(new { success = false, message = "Only staff members can be promoted to manager" });

        await _db.Users.UpdateOneAsync(u => u.Id == id, Builders<User>.Update.Set(u => u.Role, "manager"));

        await _db.AuditLogs.InsertOneAsync(new AuditLog
        {
            ActorId = UserId!,
            TargetId = id,
            Action = "promote_employee",
            Metadata = new { from = "staff", to = "manager" },
            StoreId = employee.StoreId
        });

        await _db.Notifications.InsertOneAsync(new Notification
        {
            UserId = id,
            Type = "role_change",
            Title = "Role Updated",
            Message = "You have been promoted to Manager."
        });

        return Ok(new { success = true, data = new { id = employee.Id, role = "manager" }, message = "Employee promoted to manager" });
    }

    // PUT /api/employees/:id/demote
    [HttpPut("{id}/demote")]
    public async Task<IActionResult> Demote(string id)
    {
        if (UserRole != "owner") return StatusCode(403, new { success = false, message = "Forbidden" });

        var employee = await _db.Users.Find(u => u.Id == id).FirstOrDefaultAsync();
        if (employee == null) return NotFound(new { success = false, message = "Employee not found" });
        if (employee.Role != "manager")
            return BadRequest(new { success = false, message = "Only managers can be demoted to staff" });

        await _db.Users.UpdateOneAsync(u => u.Id == id, Builders<User>.Update.Set(u => u.Role, "staff"));

        await _db.AuditLogs.InsertOneAsync(new AuditLog
        {
            ActorId = UserId!,
            TargetId = id,
            Action = "demote_employee",
            Metadata = new { from = "manager", to = "staff" },
            StoreId = employee.StoreId
        });

        await _db.Notifications.InsertOneAsync(new Notification
        {
            UserId = id,
            Type = "role_change",
            Title = "Role Updated",
            Message = "Your role has been changed to Staff."
        });

        return Ok(new { success = true, data = new { id = employee.Id, role = "staff" }, message = "Employee demoted to staff" });
    }

    // PUT /api/employees/:id/transfer
    [HttpPut("{id}/transfer")]
    public async Task<IActionResult> Transfer(string id, [FromBody] TransferEmployeeRequest req)
    {
        if (UserRole != "owner") return StatusCode(403, new { success = false, message = "Forbidden" });
        if (string.IsNullOrWhiteSpace(req.StoreId))
            return BadRequest(new { success = false, message = "storeId is required" });

        var employee = await _db.Users.Find(u => u.Id == id).FirstOrDefaultAsync();
        if (employee == null) return NotFound(new { success = false, message = "Employee not found" });

        var prevStoreId = employee.StoreId;
        await _db.Users.UpdateOneAsync(u => u.Id == id, Builders<User>.Update.Set(u => u.StoreId, req.StoreId));

        await _db.AuditLogs.InsertOneAsync(new AuditLog
        {
            ActorId = UserId!,
            TargetId = id,
            Action = "transfer_employee",
            Metadata = new { from = prevStoreId, to = req.StoreId },
            StoreId = req.StoreId
        });

        await _db.Notifications.InsertOneAsync(new Notification
        {
            UserId = id,
            Type = "store_transfer",
            Title = "Store Transfer",
            Message = "You have been transferred to a new store."
        });

        return Ok(new { success = true, data = new { id = employee.Id, storeId = req.StoreId }, message = "Employee transferred" });
    }

    // PUT /api/employees/:id/suspend
    [HttpPut("{id}/suspend")]
    public async Task<IActionResult> Suspend(string id)
    {
        if (UserRole != "owner") return StatusCode(403, new { success = false, message = "Forbidden" });

        var employee = await _db.Users.Find(u => u.Id == id).FirstOrDefaultAsync();
        if (employee == null) return NotFound(new { success = false, message = "Employee not found" });
        if (employee.Role == "owner")
            return BadRequest(new { success = false, message = "Cannot suspend owner account" });

        await _db.Users.UpdateOneAsync(u => u.Id == id, Builders<User>.Update.Set(u => u.Status, "suspended"));

        await _db.AuditLogs.InsertOneAsync(new AuditLog
        {
            ActorId = UserId!,
            TargetId = id,
            Action = "suspend_employee",
            Metadata = new { status = "suspended", role = employee.Role },
            StoreId = employee.StoreId
        });

        return Ok(new { success = true, message = "Employee suspended" });
    }

    // PUT /api/employees/:id/rehire
    [HttpPut("{id}/rehire")]
    public async Task<IActionResult> Rehire(string id)
    {
        if (UserRole != "owner") return StatusCode(403, new { success = false, message = "Forbidden" });

        var employee = await _db.Users.Find(u => u.Id == id).FirstOrDefaultAsync();
        if (employee == null) return NotFound(new { success = false, message = "Employee not found" });
        if (employee.Status != "suspended")
            return BadRequest(new { success = false, message = "Employee is not suspended" });

        await _db.Users.UpdateOneAsync(u => u.Id == id, Builders<User>.Update.Set(u => u.Status, "approved"));

        await _db.AuditLogs.InsertOneAsync(new AuditLog
        {
            ActorId = UserId!,
            TargetId = id,
            Action = "rehire_employee",
            Metadata = new { status = "approved", role = employee.Role },
            StoreId = employee.StoreId
        });

        await _db.Notifications.InsertOneAsync(new Notification
        {
            UserId = id,
            Type = "status_change",
            Title = "Account Reinstated",
            Message = "Your account has been reinstated. You can now log in."
        });

        return Ok(new { success = true, message = "Employee reinstated successfully" });
    }

    // DELETE /api/employees/:id
    [HttpDelete("{id}")]
    public async Task<IActionResult> Remove(string id)
    {
        if (UserRole != "owner") return StatusCode(403, new { success = false, message = "Forbidden" });

        var employee = await _db.Users.Find(u => u.Id == id).FirstOrDefaultAsync();
        if (employee == null) return NotFound(new { success = false, message = "Employee not found" });
        if (employee.Role == "owner")
            return BadRequest(new { success = false, message = "Cannot remove owner account" });

        await _db.Users.DeleteOneAsync(u => u.Id == id);
        await _db.Stores.UpdateManyAsync(s => s.ManagerId == id,
            Builders<Store>.Update.Set(s => s.ManagerId, null));

        await _db.AuditLogs.InsertOneAsync(new AuditLog
        {
            ActorId = UserId!,
            TargetId = id,
            Action = "remove_employee",
            StoreId = employee.StoreId
        });

        return Ok(new { success = true, message = "Employee removed" });
    }
}
