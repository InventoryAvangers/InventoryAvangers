using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using InventoryAvengers.API.Data;
using InventoryAvengers.API.DTOs;
using InventoryAvengers.API.Models;

namespace InventoryAvengers.API.Controllers;

[ApiController]
[Route("api/inventory")]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly MongoDbContext _db;

    public InventoryController(MongoDbContext db) => _db = db;

    private string? UserRole => User.FindFirst("role")?.Value;
    private string? UserStoreId => User.FindFirst("storeId")?.Value is { Length: > 0 } s ? s : null;

    // GET /api/inventory
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        if (UserRole == "superuser") return StatusCode(403, new { success = false, message = "Forbidden: superuser cannot access store-level data" });

        var storeId = UserStoreId;
        if (string.IsNullOrWhiteSpace(storeId))
            return StatusCode(403, new { message = "No store assigned to your account" });

        var records = await _db.Inventories.Find(i => i.StoreId == storeId).ToListAsync();
        var productIds = records.Select(r => r.ProductId).ToList();
        var products = await _db.Products.Find(p => productIds.Contains(p.Id)).ToListAsync();
        var productMap = products.ToDictionary(p => p.Id!);

        var enrichedRecords = records.Select(r => {
            productMap.TryGetValue(r.ProductId, out var prod);
            return new
            {
                r.Id,
                r.StoreId,
                r.Quantity,
                r.Threshold,
                r.UpdatedAt,
                costPrice = prod?.CostPrice ?? 0m,
                sellingPrice = prod?.SellingPrice ?? 0m,
                productId = prod // Frontend expects the full product object here
            };
        }).Cast<object>().ToList();

        var inventoriedIds = records.Select(r => r.ProductId).ToList();

        var missingProducts = await _db.Products.Find(
            Builders<Product>.Filter.Eq(p => p.StoreId, storeId)
            & Builders<Product>.Filter.Nin(p => p.Id, inventoriedIds)).ToListAsync();

        var virtualRecords = missingProducts.Select(p => new
        {
            _id = (string?)null,
            productId = p,
            storeId = new { _id = storeId },
            quantity = 0,
            threshold = p.Threshold,
            costPrice = p.CostPrice,
            sellingPrice = p.SellingPrice,
            updatedAt = p.CreatedAt
        }).Cast<object>().ToList();

        var result = enrichedRecords.Concat(virtualRecords).ToList();
        return Ok(result);
    }

    // GET /api/inventory/average
    [HttpGet("average")]
    public async Task<IActionResult> GetAverage()
    {
        if (UserRole == "superuser") return StatusCode(403, new { success = false, message = "Forbidden: superuser cannot access store-level data" });

        var storeId = UserStoreId;
        if (string.IsNullOrWhiteSpace(storeId))
            return StatusCode(403, new { message = "No store assigned to your account" });

        var records = await _db.Inventories.Find(i => i.StoreId == storeId).ToListAsync();
        if (records.Count == 0)
            return Ok(new { averageCostPrice = 0m, averageSellingPrice = 0m, averageQuantity = 0m, totalProducts = 0 });

        var productIds = records.Select(r => r.ProductId).Distinct().ToList();
        var products = await _db.Products
            .Find(Builders<Product>.Filter.In(p => p.Id, productIds))
            .ToListAsync();

        if (products.Count == 0)
            return Ok(new { averageCostPrice = 0m, averageSellingPrice = 0m, averageQuantity = 0m, totalProducts = 0 });

        var avgCostPrice = Math.Round(products.Average(p => p.CostPrice), 2);
        var avgSellingPrice = Math.Round(products.Average(p => p.SellingPrice), 2);
        var avgQuantity = Math.Round(records.Average(r => (decimal)r.Quantity), 2);

        return Ok(new
        {
            averageCostPrice = avgCostPrice,
            averageSellingPrice = avgSellingPrice,
            averageQuantity = avgQuantity,
            totalProducts = products.Count
        });
    }

    // POST /api/inventory/adjust
    [HttpPost("adjust")]
    public async Task<IActionResult> Adjust([FromBody] AdjustInventoryRequest req)
    {
        if (UserRole == "superuser") return StatusCode(403, new { success = false, message = "Forbidden: superuser cannot access store-level data" });
        if (UserRole != "owner" && UserRole != "manager")
            return StatusCode(403, new { message = "Forbidden: insufficient permissions" });

        var storeId = UserStoreId;
        if (string.IsNullOrWhiteSpace(req.ProductId) || string.IsNullOrWhiteSpace(storeId))
            return BadRequest(new { message = "productId is required and store must be assigned to your account" });

        var updates = new List<UpdateDefinition<Inventory>>
        {
            Builders<Inventory>.Update.Set(i => i.UpdatedAt, DateTime.UtcNow)
        };
        if (req.Quantity.HasValue) updates.Add(Builders<Inventory>.Update.Set(i => i.Quantity, req.Quantity.Value));
        if (req.Threshold.HasValue) updates.Add(Builders<Inventory>.Update.Set(i => i.Threshold, req.Threshold.Value));

        var existing = await _db.Inventories.Find(i => i.ProductId == req.ProductId && i.StoreId == storeId).FirstOrDefaultAsync();
        if (existing == null)
        {
            var newRecord = new Inventory
            {
                ProductId = req.ProductId,
                StoreId = storeId,
                Quantity = req.Quantity ?? 0,
                Threshold = req.Threshold ?? 10
            };
            await _db.Inventories.InsertOneAsync(newRecord);
            return Ok(newRecord);
        }

        var record = await _db.Inventories.FindOneAndUpdateAsync(
            i => i.ProductId == req.ProductId && i.StoreId == storeId,
            Builders<Inventory>.Update.Combine(updates),
            new FindOneAndUpdateOptions<Inventory> { ReturnDocument = ReturnDocument.After });

        return Ok(record);
    }
}
