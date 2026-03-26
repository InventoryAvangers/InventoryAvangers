using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using InventoryAvengers.API.Data;
using InventoryAvengers.API.DTOs;
using InventoryAvengers.API.Middleware;
using InventoryAvengers.API.Models;

namespace InventoryAvengers.API.Controllers;

[ApiController]
[Route("api/returns")]
[Authorize]
[FeatureCheck("returns")]
public class ReturnsController : ControllerBase
{
    private readonly MongoDbContext _db;

    public ReturnsController(MongoDbContext db) => _db = db;

    private string? UserId => User.FindFirst("id")?.Value;
    private string? UserRole => User.FindFirst("role")?.Value;
    private string? UserStoreId => User.FindFirst("storeId")?.Value is { Length: > 0 } s ? s : null;

    private static readonly string[] ValidReasons = { "defective", "wrong_item", "others" };
    private static readonly string[] RestockReasons = { "wrong_item", "others" };

    // POST /api/returns
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReturnRequest req)
    {
        if (UserRole == "superuser") return StatusCode(403, new { success = false, message = "Forbidden: superuser cannot access store-level data" });

        if (string.IsNullOrWhiteSpace(req.SaleId) || string.IsNullOrWhiteSpace(req.ProductId)
            || req.Quantity <= 0 || string.IsNullOrWhiteSpace(req.Reason))
            return BadRequest(new { message = "Missing required fields" });

        if (!ValidReasons.Contains(req.Reason))
            return BadRequest(new { message = "Invalid return reason. Must be defective, wrong_item, or others." });

        // Fetch original sale
        var sale = await _db.Sales.Find(s => s.Id == req.SaleId).FirstOrDefaultAsync();
        if (sale == null)
            return NotFound(new { message = "Original sale not found." });

        // Find item in sale
        var boughtItem = sale.Items.FirstOrDefault(i => i.ProductId == req.ProductId);
        if (boughtItem == null)
            return BadRequest(new { message = "Item not found in the original sale." });

        // Check already returned quantity for this item in this sale
        var allReturnsForThisSaleItem = await _db.Returns
            .Find(r => r.SaleId == req.SaleId && r.ProductId == req.ProductId)
            .ToListAsync();
        
        int totalReturnedAlready = allReturnsForThisSaleItem.Sum(r => r.Quantity);

        if (totalReturnedAlready + req.Quantity > boughtItem.Qty)
        {
            return BadRequest(new { 
                message = $"Cannot return {req.Quantity} units. Only {boughtItem.Qty - totalReturnedAlready} units remaining from the original purchase of {boughtItem.Qty} units.",
                boughtQuantity = boughtItem.Qty,
                alreadyReturned = totalReturnedAlready,
                requested = req.Quantity
            });
        }

        var storeId = UserStoreId;

        if (RestockReasons.Contains(req.Reason) && !string.IsNullOrWhiteSpace(storeId))
        {
            var inv = await _db.Inventories
                .Find(i => i.ProductId == req.ProductId && i.StoreId == storeId)
                .FirstOrDefaultAsync();
            if (inv != null)
            {
                await _db.Inventories.UpdateOneAsync(
                    i => i.Id == inv.Id,
                    Builders<Inventory>.Update
                        .Inc(i => i.Quantity, req.Quantity)
                        .Set(i => i.UpdatedAt, DateTime.UtcNow));
            }
        }

        var ret = new Return
        {
            SaleId = req.SaleId,
            ProductId = req.ProductId,
            StoreId = storeId,
            Quantity = req.Quantity,
            Reason = req.Reason,
            RefundAmount = req.RefundAmount,
            ProcessedBy = UserId
        };
        await _db.Returns.InsertOneAsync(ret);

        return StatusCode(201, ret);
    }

    // GET /api/returns
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        if (UserRole == "superuser") return StatusCode(403, new { success = false, message = "Forbidden: superuser cannot access store-level data" });

        var filter = Builders<Return>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(UserStoreId))
            filter = Builders<Return>.Filter.Eq(r => r.StoreId, UserStoreId);

        var returns = await _db.Returns.Find(filter)
            .SortByDescending(r => r.CreatedAt)
            .ToListAsync();

        return Ok(returns);
    }
}
