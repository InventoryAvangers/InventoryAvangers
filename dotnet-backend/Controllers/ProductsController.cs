using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using InventoryAvengers.API.Data;
using InventoryAvengers.API.DTOs;
using InventoryAvengers.API.Models;
using InventoryAvengers.API.Services;

namespace InventoryAvengers.API.Controllers;

[ApiController]
[Route("api/products")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly MongoDbContext _db;

    public ProductsController(MongoDbContext db) => _db = db;

    private string? UserId => User.FindFirst("id")?.Value;
    private string? UserRole => User.FindFirst("role")?.Value;
    private string? UserStoreId => User.FindFirst("storeId")?.Value is { Length: > 0 } s ? s : null;

    private bool IsSuperuser => UserRole == "superuser";

    // GET /api/products/lookup?barcode=VALUE
    [HttpGet("lookup")]
    public async Task<IActionResult> Lookup([FromQuery] string? barcode)
    {
        if (IsSuperuser) return StatusCode(403, new { success = false, message = "Forbidden: superuser cannot access store-level data" });
        if (string.IsNullOrWhiteSpace(barcode))
            return BadRequest(new { message = "barcode query param required" });

        var filter = Builders<Product>.Filter.Eq(p => p.Barcode, barcode);
        if (!string.IsNullOrWhiteSpace(UserStoreId))
            filter &= Builders<Product>.Filter.Eq(p => p.StoreId, UserStoreId);

        var product = await _db.Products.Find(filter).FirstOrDefaultAsync();
        if (product == null) return NotFound(new { message = "Product not found" });
        return Ok(product);
    }

    // GET /api/products
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        if (IsSuperuser) return StatusCode(403, new { success = false, message = "Forbidden: superuser cannot access store-level data" });

        var filter = Builders<Product>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(UserStoreId))
            filter = Builders<Product>.Filter.Eq(p => p.StoreId, UserStoreId);

        var products = await _db.Products.Find(filter).ToListAsync();

        if (!string.IsNullOrWhiteSpace(UserStoreId))
        {
            var productIds = products.Select(p => p.Id).ToList();
            var inventoryFilter = Builders<Inventory>.Filter.Eq(i => i.StoreId, UserStoreId)
                & Builders<Inventory>.Filter.In(i => i.ProductId, productIds!);
            var invRecords = await _db.Inventories.Find(inventoryFilter).ToListAsync();
            var invMap = invRecords.ToDictionary(i => i.ProductId, i => i.Quantity);

            var enriched = products.Select(p =>
            {
                var obj = new
                {
                    _id = p.Id,
                    id = p.Id,
                    p.Name,
                    p.Category,
                    p.CostPrice,
                    p.SellingPrice,
                    quantity = invMap.TryGetValue(p.Id!, out var q) ? q : p.Quantity,
                    p.Threshold,
                    p.Sku,
                    p.Barcode,
                    p.BarcodeType,
                    p.StoreId,
                    p.CreatedBy,
                    p.CreatedAt,
                    profit = p.Profit
                };
                return (object)obj;
            }).ToList();
            return Ok(enriched);
        }

        return Ok(products);
    }

    // POST /api/products
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest req)
    {
        if (IsSuperuser) return StatusCode(403, new { success = false, message = "Forbidden: superuser cannot access store-level data" });
        if (UserRole != "owner" && UserRole != "manager")
            return StatusCode(403, new { message = "Forbidden: insufficient permissions" });

        var storeId = UserStoreId;
        var sku = req.Sku;

        if (string.IsNullOrWhiteSpace(sku))
        {
            do
            {
                sku = HelperService.GenerateSku();
            } while (await _db.Products.Find(p => p.Sku == sku).AnyAsync());
        }

        var product = new Product
        {
            Name = req.Name,
            Category = req.Category,
            CostPrice = req.CostPrice,
            SellingPrice = req.SellingPrice,
            Quantity = req.Quantity,
            Threshold = req.Threshold,
            Sku = sku,
            Barcode = req.Barcode,
            BarcodeType = req.BarcodeType,
            StoreId = storeId,
            CreatedBy = UserId
        };
        await _db.Products.InsertOneAsync(product);

        if (!string.IsNullOrWhiteSpace(storeId))
        {
            var existing = await _db.Inventories
                .Find(i => i.ProductId == product.Id && i.StoreId == storeId)
                .FirstOrDefaultAsync();
            if (existing == null)
            {
                await _db.Inventories.InsertOneAsync(new Inventory
                {
                    ProductId = product.Id!,
                    StoreId = storeId,
                    Quantity = req.Quantity,
                    Threshold = req.Threshold
                });
            }
        }

        return StatusCode(201, product);
    }

    // PUT /api/products/:id
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateProductRequest req)
    {
        if (IsSuperuser) return StatusCode(403, new { success = false, message = "Forbidden: superuser cannot access store-level data" });
        if (UserRole != "owner" && UserRole != "manager")
            return StatusCode(403, new { message = "Forbidden: insufficient permissions" });

        var update = Builders<Product>.Update.Combine();
        var updates = new List<UpdateDefinition<Product>>();
        if (req.Name != null) updates.Add(Builders<Product>.Update.Set(p => p.Name, req.Name));
        if (req.Category != null) updates.Add(Builders<Product>.Update.Set(p => p.Category, req.Category));
        if (req.CostPrice.HasValue) updates.Add(Builders<Product>.Update.Set(p => p.CostPrice, req.CostPrice.Value));
        if (req.SellingPrice.HasValue) updates.Add(Builders<Product>.Update.Set(p => p.SellingPrice, req.SellingPrice.Value));
        if (req.Quantity.HasValue) updates.Add(Builders<Product>.Update.Set(p => p.Quantity, req.Quantity.Value));
        if (req.Threshold.HasValue) updates.Add(Builders<Product>.Update.Set(p => p.Threshold, req.Threshold.Value));
        if (req.Sku != null) updates.Add(Builders<Product>.Update.Set(p => p.Sku, req.Sku));
        if (req.Barcode != null) updates.Add(Builders<Product>.Update.Set(p => p.Barcode, req.Barcode));
        if (req.BarcodeType != null) updates.Add(Builders<Product>.Update.Set(p => p.BarcodeType, req.BarcodeType));

        if (updates.Count == 0)
            return BadRequest(new { message = "No fields to update" });

        var result = await _db.Products.FindOneAndUpdateAsync(
            p => p.Id == id,
            Builders<Product>.Update.Combine(updates),
            new FindOneAndUpdateOptions<Product> { ReturnDocument = ReturnDocument.After });

        if (result == null) return NotFound(new { message = "Product not found" });

        // Synchronize quantity and threshold updates with the store's Inventory collection
        if (!string.IsNullOrWhiteSpace(UserStoreId) && (req.Quantity.HasValue || req.Threshold.HasValue))
        {
            var invUpdates = new List<UpdateDefinition<Inventory>>
            {
                Builders<Inventory>.Update.Set(i => i.UpdatedAt, DateTime.UtcNow)
            };
            if (req.Quantity.HasValue) invUpdates.Add(Builders<Inventory>.Update.Set(i => i.Quantity, req.Quantity.Value));
            if (req.Threshold.HasValue) invUpdates.Add(Builders<Inventory>.Update.Set(i => i.Threshold, req.Threshold.Value));

            await _db.Inventories.UpdateOneAsync(
                i => i.ProductId == id && i.StoreId == UserStoreId,
                Builders<Inventory>.Update.Combine(invUpdates)
            );
        }

        return Ok(result);
    }

    // DELETE /api/products/:id
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        if (IsSuperuser) return StatusCode(403, new { success = false, message = "Forbidden: superuser cannot access store-level data" });

        var product = await _db.Products.Find(p => p.Id == id).FirstOrDefaultAsync();
        if (product == null) return NotFound(new { message = "Product not found" });

        if (UserRole == "owner")
        {
            await _db.Products.DeleteOneAsync(p => p.Id == id);
<<<<<<< HEAD
=======
            await _db.Inventories.DeleteManyAsync(i => i.ProductId == id);
>>>>>>> 177ae6d (feat(inventory): add product and inventory schemas with CRUD controller)
            return Ok(new { message = "Product deleted" });
        }

        if (UserRole == "manager")
        {
            var approval = new Approval
            {
                Action = "delete_product",
                Description = $"Manager {User.FindFirst("name")?.Value} requested deletion of product \"{product.Name}\"",
                RequestedBy = UserId,
<<<<<<< HEAD
                Metadata = new { productId = product.Id, productName = product.Name }
            };
            await _db.Approvals.InsertOneAsync(approval);
=======
                StoreId = UserStoreId,
                Metadata = new { productId = product.Id, productName = product.Name }
            };
            await _db.Approvals.InsertOneAsync(approval);

            var owners = await _db.Users.Find(u => u.Role == "owner").ToListAsync();
            var notifications = owners.Select(owner => new Notification
            {
                UserId = owner.Id,
                Title = "Deletion Request",
                Message = $"Manager {User.FindFirst("name")?.Value} requested to delete product \"{product.Name}\".",
                Type = "approval",
                CreatedAt = DateTime.UtcNow
            }).ToList();
            if (notifications.Any())
            {
                await _db.Notifications.InsertManyAsync(notifications);
            }
>>>>>>> 177ae6d (feat(inventory): add product and inventory schemas with CRUD controller)
            return StatusCode(202, new { message = "Deletion request submitted for owner approval", approval });
        }

        return StatusCode(403, new { message = "Forbidden: insufficient permissions" });
    }
}
