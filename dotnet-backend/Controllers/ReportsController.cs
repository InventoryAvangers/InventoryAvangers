using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using InventoryAvengers.API.Data;
using InventoryAvengers.API.Middleware;
using InventoryAvengers.API.Models;

namespace InventoryAvengers.API.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly MongoDbContext _db;

    public ReportsController(MongoDbContext db) => _db = db;

    private string? UserRole => User.FindFirst("role")?.Value;
    private string? UserStoreId => User.FindFirst("storeId")?.Value is { Length: > 0 } s ? s : null;

    // GET /api/reports/dashboard
    [HttpGet("dashboard")]
    [FeatureCheck("reports")]
    public async Task<IActionResult> Dashboard()
    {
        if (UserRole == "superuser") return StatusCode(403, new { success = false, message = "Forbidden: superuser cannot access store-level data" });
        if (UserRole != "owner" && UserRole != "manager") return StatusCode(403, new { success = false, message = "Forbidden" });

        var storeId = UserStoreId;

        var now = DateTime.UtcNow;
        var startOfDay = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var saleFilter = Builders<Sale>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(storeId)) saleFilter = Builders<Sale>.Filter.Eq(s => s.StoreId, storeId);

        var returnFilter = Builders<Return>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(storeId)) returnFilter = Builders<Return>.Filter.Eq(r => r.StoreId, storeId);

        var invFilter = Builders<Inventory>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(storeId)) invFilter = Builders<Inventory>.Filter.Eq(i => i.StoreId, storeId);

        var userFilter = Builders<User>.Filter.In(u => u.Role, new[] { "manager", "staff" })
            & Builders<User>.Filter.Eq(u => u.Status, "approved");
        if (!string.IsNullOrWhiteSpace(storeId)) userFilter &= Builders<User>.Filter.Eq(u => u.StoreId, storeId);

        var allSales = await _db.Sales.Find(saleFilter).ToListAsync();
        var allReturns = await _db.Returns.Find(returnFilter).ToListAsync();
        var inventory = await _db.Inventories.Find(invFilter).ToListAsync();
        var staffCount = await _db.Users.CountDocumentsAsync(userFilter);

        var dailySales = allSales.Where(s => s.CreatedAt >= startOfDay).ToList();
        var monthlySales = allSales.Where(s => s.CreatedAt >= startOfMonth).ToList();
        var dailyReturns = allReturns.Where(r => r.CreatedAt >= startOfDay).ToList();
        var monthlyReturns = allReturns.Where(r => r.CreatedAt >= startOfMonth).ToList();

        var grossDailyRevenue = dailySales.Sum(s => s.TotalAmount);
        var dailyReturnAmount = dailyReturns.Sum(r => r.RefundAmount);
        var dailyRevenue = Math.Max(0, grossDailyRevenue - dailyReturnAmount);

        var grossMonthlyRevenue = monthlySales.Sum(s => s.TotalAmount);
        var monthlyReturnAmount = monthlyReturns.Sum(r => r.RefundAmount);
        var monthlyRevenue = Math.Max(0, grossMonthlyRevenue - monthlyReturnAmount);

        var salesCount = allSales.Count;

        // Fetch selling prices for inventory products
        var productIds = inventory.Select(i => i.ProductId).Distinct().ToList();
        var products = await _db.Products
            .Find(Builders<Product>.Filter.In(p => p.Id, productIds))
            .ToListAsync();
        var priceMap = products.ToDictionary(p => p.Id!, p => p.SellingPrice);

        var inventoryValue = inventory.Sum(inv =>
            inv.Quantity * (priceMap.TryGetValue(inv.ProductId, out var price) ? price : 0));
        var lowStockCount = inventory.Count(inv => inv.Quantity <= inv.Threshold);

        return Ok(new
        {
            dailyRevenue,
            monthlyRevenue,
            salesCount,
            inventoryValue,
            lowStockCount,
            staffCount
        });
    }

    // GET /api/reports/sales
    [HttpGet("sales")]
    [FeatureCheck("reports")]
    public async Task<IActionResult> SalesReport([FromQuery] string? startDate, [FromQuery] string? endDate, [FromQuery] string? paymentMethod)
    {
        if (UserRole == "superuser") return StatusCode(403, new { success = false, message = "Forbidden: superuser cannot access store-level data" });
        if (UserRole != "owner" && UserRole != "manager") return StatusCode(403, new { success = false, message = "Forbidden" });

        var filter = Builders<Sale>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(UserStoreId))
            filter &= Builders<Sale>.Filter.Eq(s => s.StoreId, UserStoreId);

        if (!string.IsNullOrWhiteSpace(startDate))
            filter &= Builders<Sale>.Filter.Gte(s => s.CreatedAt, DateTime.Parse(startDate).ToUniversalTime());

        if (!string.IsNullOrWhiteSpace(endDate))
        {
            var end = DateTime.Parse(endDate).Date.AddDays(1).AddMilliseconds(-1).ToUniversalTime();
            filter &= Builders<Sale>.Filter.Lte(s => s.CreatedAt, end);
        }

        if (!string.IsNullOrWhiteSpace(paymentMethod) && paymentMethod != "all")
            filter &= Builders<Sale>.Filter.Eq(s => s.PaymentMethod, paymentMethod);

        var sales = await _db.Sales.Find(filter).SortByDescending(s => s.CreatedAt).ToListAsync();

        var returnFilter = Builders<Return>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(UserStoreId))
            returnFilter = Builders<Return>.Filter.Eq(r => r.StoreId, UserStoreId);
        var returns = await _db.Returns.Find(returnFilter).ToListAsync();

        var returnMap = returns.GroupBy(r => r.SaleId)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.RefundAmount));

        var annotatedSales = sales.Select(s =>
        {
            var returned = returnMap.TryGetValue(s.Id!, out var amt) ? amt : 0m;
            var returnStatus = returned > 0
                ? (returned >= s.TotalAmount ? "returned" : "partial_return")
                : (string?)null;
            return new
            {
                _id = s.Id,
                s.Items,
                s.TotalAmount,
                s.Subtotal,
                s.Tax,
                s.PaymentMethod,
                s.EmployeeId,
                s.CustomerName,
                s.StoreId,
                s.ReceiptNumber,
                s.CreatedAt,
                returnedAmount = returned,
                returnStatus
            };
        }).ToList();

        var grossRevenue = sales.Sum(s => s.TotalAmount);
        var totalReturned = returns.Sum(r => r.RefundAmount);
        var totalRevenue = Math.Max(0, grossRevenue - totalReturned);
        var totalOrders = sales.Count;
        var totalProfit = totalRevenue * 0.2m;
        var profitMargin = totalRevenue > 0 ? Math.Round(totalProfit / totalRevenue * 100, 1) : 0;

        return Ok(new
        {
            sales = annotatedSales,
            summary = new { grossRevenue, totalReturned, totalRevenue, totalOrders, totalProfit, profitMargin }
        });
    }
}
