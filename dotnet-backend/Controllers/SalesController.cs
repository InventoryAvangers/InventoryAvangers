using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using InventoryAvengers.API.Data;
using InventoryAvengers.API.DTOs;
using InventoryAvengers.API.Models;
using InventoryAvengers.API.Services;

namespace InventoryAvengers.API.Controllers;

[ApiController]
[Route("api/sales")]
[Authorize]
public class SalesController : ControllerBase
{
    private readonly MongoDbContext _db;

    public SalesController(MongoDbContext db) => _db = db;

    private string? UserId => User.FindFirst("id")?.Value;
    private string? UserRole => User.FindFirst("role")?.Value;
    private string? UserStoreId => User.FindFirst("storeId")?.Value is { Length: > 0 } s ? s : null;

    // POST /api/sales
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSaleRequest req)
    {
        if (UserRole == "superuser") return StatusCode(403, new { success = false, message = "Forbidden: superuser cannot access store-level data" });

        if (req.Items == null || req.Items.Count == 0)
            return BadRequest(new { message = "No items in sale" });

        var storeId = UserStoreId;
        var subtotal = req.Items.Sum(i => i.Price * i.Qty);

        foreach (var item in req.Items)
        {
            var product = await _db.Products.Find(p => p.Id == item.ProductId).FirstOrDefaultAsync();
            if (product == null)
                return NotFound(new { message = $"Product {item.ProductId} not found" });

            if (!string.IsNullOrWhiteSpace(storeId))
            {
                var inv = await _db.Inventories
                    .Find(i => i.ProductId == item.ProductId && i.StoreId == storeId)
                    .FirstOrDefaultAsync();
                if (inv == null)
                    return BadRequest(new { message = $"Product \"{product.Name}\" is not available in this store's inventory" });
                if (inv.Quantity < item.Qty)
                    return BadRequest(new { message = $"Insufficient stock for {product.Name}" });

                await _db.Inventories.UpdateOneAsync(
                    i => i.Id == inv.Id,
                    Builders<Inventory>.Update
                        .Inc(i => i.Quantity, -item.Qty)
                        .Set(i => i.UpdatedAt, DateTime.UtcNow));
            }
            else
            {
                if (product.Quantity < item.Qty)
                    return BadRequest(new { message = $"Insufficient stock for {product.Name}" });
                await _db.Products.UpdateOneAsync(p => p.Id == product.Id,
                    Builders<Product>.Update.Inc(p => p.Quantity, -item.Qty));
            }
        }

        string receiptNumber;
        do { receiptNumber = HelperService.GenerateReceiptNumber(); }
        while (await _db.Sales.Find(s => s.ReceiptNumber == receiptNumber).AnyAsync());

        var sale = new Sale
        {
            Items = req.Items.Select(i => new SaleItem
            {
                ProductId = i.ProductId,
                Name = i.Name,
                Sku = i.Sku,
                Qty = i.Qty,
                Price = i.Price
            }).ToList(),
            TotalAmount = req.TotalAmount,
            Subtotal = subtotal,
            Tax = 0,
            PaymentMethod = req.PaymentMethod,
            CustomerName = req.CustomerName ?? "Walk-in",
            EmployeeId = UserId,
            StoreId = storeId,
            ReceiptNumber = receiptNumber
        };
        await _db.Sales.InsertOneAsync(sale);

        return StatusCode(201, sale);
    }

    // GET /api/sales/:id/receipt
    [HttpGet("{id}/receipt")]
    public async Task<IActionResult> GetReceipt(string id)
    {
        if (UserRole == "superuser") return StatusCode(403, new { success = false, message = "Forbidden: superuser cannot access store-level data" });

        var sale = await _db.Sales.Find(s => s.Id == id).FirstOrDefaultAsync();
        if (sale == null) return NotFound(new { message = "Sale not found" });

        if (!string.IsNullOrWhiteSpace(UserStoreId) && sale.StoreId != UserStoreId)
            return StatusCode(403, new { message = "Access denied" });

        return Ok(sale);
    }

    // GET /api/sales
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        if (UserRole == "superuser") return StatusCode(403, new { success = false, message = "Forbidden: superuser cannot access store-level data" });

        var filter = Builders<Sale>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(UserStoreId))
            filter = Builders<Sale>.Filter.Eq(s => s.StoreId, UserStoreId);

        var sales = await _db.Sales.Find(filter)
            .SortByDescending(s => s.CreatedAt)
            .ToListAsync();

        return Ok(sales);
    }
}
