using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using InventoryAvengers.API.Data;
using InventoryAvengers.API.DTOs;
using InventoryAvengers.API.Models;

namespace InventoryAvengers.API.Controllers;

[ApiController]
[Route("api/stores")]
public class StoresController : ControllerBase
{
    private readonly MongoDbContext _db;

    public StoresController(MongoDbContext db) => _db = db;

    private string? UserId => User.FindFirst("id")?.Value;
    private string? UserRole => User.FindFirst("role")?.Value;
    private string? UserStoreId => User.FindFirst("storeId")?.Value is { Length: > 0 } s ? s : null;

    // GET /api/stores/public — unauthenticated
    [HttpGet("public")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublic()
    {
        var stores = await _db.Stores
            .Find(s => s.Status == "active" || s.Status == "trial")
            .SortBy(s => s.ShopName)
            .ToListAsync();
        var result = stores.Select(s => new { _id = s.Id, s.ShopName, s.Name });
        return Ok(result);
    }

    // GET /api/stores
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAll()
    {
        if (UserRole == "owner")
        {
            var stores = await _db.Stores.Find(_ => true).SortBy(s => s.Name).ToListAsync();
            return Ok(stores);
        }

        if (string.IsNullOrWhiteSpace(UserStoreId)) return Ok(new List<Store>());
        var store = await _db.Stores.Find(s => s.Id == UserStoreId).FirstOrDefaultAsync();
        return Ok(store != null ? new[] { store } : Array.Empty<Store>());
    }

    // POST /api/stores
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateStoreRequest req)
    {
        if (UserRole != "owner") return StatusCode(403, new { message = "Forbidden" });

        var store = new Store
        {
            Name = req.Name,
            Address = req.Address ?? string.Empty,
            Code = req.Code.ToUpper(),
            Phone = req.Phone ?? string.Empty,
            Email = req.Email ?? string.Empty
        };
        await _db.Stores.InsertOneAsync(store);

        await _db.AuditLogs.InsertOneAsync(new AuditLog
        {
            ActorId = UserId!,
            Action = "create_store",
            Metadata = new { storeId = store.Id, name = store.Name }
        });

        return StatusCode(201, store);
    }

    // PUT /api/stores/:id
    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateStoreRequest req)
    {
        if (UserRole != "owner") return StatusCode(403, new { message = "Forbidden" });

        var updates = new List<UpdateDefinition<Store>>();
        if (req.Name != null) updates.Add(Builders<Store>.Update.Set(s => s.Name, req.Name));
        if (req.Address != null) updates.Add(Builders<Store>.Update.Set(s => s.Address, req.Address));
        if (req.Phone != null) updates.Add(Builders<Store>.Update.Set(s => s.Phone, req.Phone));
        if (req.Email != null) updates.Add(Builders<Store>.Update.Set(s => s.Email, req.Email));
        if (req.Status != null) updates.Add(Builders<Store>.Update.Set(s => s.Status, req.Status));

        if (updates.Count == 0) return BadRequest(new { message = "No fields to update" });

        var store = await _db.Stores.FindOneAndUpdateAsync(
            s => s.Id == id,
            Builders<Store>.Update.Combine(updates),
            new FindOneAndUpdateOptions<Store> { ReturnDocument = ReturnDocument.After });

        if (store == null) return NotFound(new { message = "Store not found" });

        await _db.AuditLogs.InsertOneAsync(new AuditLog
        {
            ActorId = UserId!,
            Action = "update_store",
            Metadata = new { storeId = store.Id, name = store.Name }
        });

        return Ok(store);
    }

    // DELETE /api/stores/:id — soft-delete
    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> Delete(string id)
    {
        if (UserRole != "owner") return StatusCode(403, new { message = "Forbidden" });

        var store = await _db.Stores.FindOneAndUpdateAsync(
            s => s.Id == id,
            Builders<Store>.Update.Set(s => s.Status, "inactive").Set(s => s.IsActive, false),
            new FindOneAndUpdateOptions<Store> { ReturnDocument = ReturnDocument.After });

        if (store == null) return NotFound(new { message = "Store not found" });

        await _db.AuditLogs.InsertOneAsync(new AuditLog
        {
            ActorId = UserId!,
            Action = "delete_store",
            Metadata = new { storeId = store.Id, name = store.Name }
        });

        return Ok(new { message = "Store deactivated" });
    }

    // PUT /api/stores/:id/manager
    [HttpPut("{id}/manager")]
    [Authorize]
    public async Task<IActionResult> AssignManager(string id, [FromBody] AssignManagerRequest req)
    {
        if (UserRole != "owner") return StatusCode(403, new { success = false, message = "Forbidden" });

        var store = await _db.Stores.Find(s => s.Id == id).FirstOrDefaultAsync();
        if (store == null) return NotFound(new { success = false, message = "Store not found" });

        if (!string.IsNullOrWhiteSpace(req.ManagerId))
        {
            var manager = await _db.Users.Find(u => u.Id == req.ManagerId).FirstOrDefaultAsync();
            if (manager == null) return NotFound(new { success = false, message = "User not found" });
            if (manager.Role != "manager" && manager.Role != "owner")
                return BadRequest(new { success = false, message = "User must have manager or owner role" });
        }

        await _db.Stores.UpdateOneAsync(s => s.Id == id,
            Builders<Store>.Update.Set(s => s.ManagerId, req.ManagerId ?? null));

        await _db.AuditLogs.InsertOneAsync(new AuditLog
        {
            ActorId = UserId!,
            Action = "assign_store_manager",
            Metadata = new { storeId = id, managerId = req.ManagerId }
        });

        var updated = await _db.Stores.Find(s => s.Id == id).FirstOrDefaultAsync();
        return Ok(new { success = true, data = updated });
    }

    // GET /api/stores/:id/stats
    [HttpGet("{id}/stats")]
    [Authorize]
    public async Task<IActionResult> GetStats(string id)
    {
        if (UserRole != "owner" && UserRole != "manager") return StatusCode(403, new { success = false, message = "Forbidden" });
        if (UserRole == "manager" && UserStoreId != id)
            return StatusCode(403, new { success = false, message = "Managers can only view stats for their own store" });

        var now = DateTime.UtcNow;
        var todayStart = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var storeFilter = Builders<Sale>.Filter.Eq(s => s.StoreId, id);
        var dailySales = await _db.Sales.Find(storeFilter & Builders<Sale>.Filter.Gte(s => s.CreatedAt, todayStart)).ToListAsync();
        var monthlySales = await _db.Sales.Find(storeFilter & Builders<Sale>.Filter.Gte(s => s.CreatedAt, monthStart)).ToListAsync();
        var totalSalesCount = await _db.Sales.CountDocumentsAsync(storeFilter);
        var totalStaff = await _db.Users.CountDocumentsAsync(
            Builders<User>.Filter.Eq(u => u.StoreId, id) & Builders<User>.Filter.Eq(u => u.Status, "approved"));

        var inventoryRecords = await _db.Inventories.Find(i => i.StoreId == id).ToListAsync();
        var invProductIds = inventoryRecords.Select(i => i.ProductId).ToList();
        var products = await _db.Products.Find(p => invProductIds.Contains(p.Id!)).ToListAsync();
        var productMap = products.ToDictionary(p => p.Id!, p => p.SellingPrice);

        decimal inventoryValue = 0;
        int lowStockCount = 0;
        foreach (var rec in inventoryRecords)
        {
            var price = productMap.TryGetValue(rec.ProductId, out var p) ? p : 0;
            inventoryValue += rec.Quantity * price;
            if (rec.Quantity <= rec.Threshold) lowStockCount++;
        }

        var dailySalesTotal = dailySales.Sum(s => s.TotalAmount);
        var monthlySalesTotal = monthlySales.Sum(s => s.TotalAmount);

        return Ok(new
        {
            success = true,
            data = new
            {
                dailySales = dailySalesTotal,
                dailyProfit = dailySalesTotal * 0.2m,
                monthlySales = monthlySalesTotal,
                monthlyProfit = monthlySalesTotal * 0.2m,
                monthlySalesCount = monthlySales.Count,
                totalSalesCount,
                totalStaff,
                inventoryValue,
                lowStockCount
            }
        });
    }

    // PUT /api/stores/:id/branding
    [HttpPut("{id}/branding")]
    [Authorize]
    public async Task<IActionResult> UpdateBranding(string id, [FromBody] UpdateBrandingRequest req)
    {
        if (UserRole != "owner") return StatusCode(403, new { success = false, message = "Forbidden" });

        var updates = new List<UpdateDefinition<Store>>();
        if (req.ShopName != null) updates.Add(Builders<Store>.Update.Set(s => s.ShopName, req.ShopName));
        if (req.LogoUrl != null) updates.Add(Builders<Store>.Update.Set(s => s.LogoUrl, req.LogoUrl));
        if (req.Address != null) updates.Add(Builders<Store>.Update.Set(s => s.Address, req.Address));
        if (req.Phone != null) updates.Add(Builders<Store>.Update.Set(s => s.Phone, req.Phone));
        if (req.Email != null) updates.Add(Builders<Store>.Update.Set(s => s.Email, req.Email));
        if (req.ReceiptFooter != null) updates.Add(Builders<Store>.Update.Set(s => s.ReceiptFooter, req.ReceiptFooter));

        if (updates.Count == 0) return BadRequest(new { success = false, message = "No fields to update" });

        var store = await _db.Stores.FindOneAndUpdateAsync(
            s => s.Id == id,
            Builders<Store>.Update.Combine(updates),
            new FindOneAndUpdateOptions<Store> { ReturnDocument = ReturnDocument.After });

        if (store == null) return NotFound(new { success = false, message = "Store not found" });

        await _db.AuditLogs.InsertOneAsync(new AuditLog
        {
            ActorId = UserId!,
            Action = "update_store_branding",
            Metadata = new { storeId = store.Id, name = store.Name }
        });

        return Ok(new { success = true, data = store });
    }
}
