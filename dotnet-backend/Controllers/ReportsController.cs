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
        var startOfDay   = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // ── Build base filters ──────────────────────────────────────────────
        var storeFilter = string.IsNullOrWhiteSpace(storeId)
            ? FilterDefinition<Sale>.Empty
            : Builders<Sale>.Filter.Eq(s => s.StoreId, storeId);

        var returnStoreFilter = string.IsNullOrWhiteSpace(storeId)
            ? FilterDefinition<Return>.Empty
            : Builders<Return>.Filter.Eq(r => r.StoreId, storeId);

        var invFilter = string.IsNullOrWhiteSpace(storeId)
            ? FilterDefinition<Inventory>.Empty
            : Builders<Inventory>.Filter.Eq(i => i.StoreId, storeId);

        var userFilter = Builders<User>.Filter.In(u => u.Role, new[] { "manager", "staff" })
            & Builders<User>.Filter.Eq(u => u.Status, "approved");
        if (!string.IsNullOrWhiteSpace(storeId))
            userFilter &= Builders<User>.Filter.Eq(u => u.StoreId, storeId);

        // ── Push date filters INTO MongoDB — avoid loading all sales into memory ──
        var dailySaleFilter   = storeFilter & Builders<Sale>.Filter.Gte(s => s.CreatedAt, startOfDay);
        var monthlySaleFilter = storeFilter & Builders<Sale>.Filter.Gte(s => s.CreatedAt, startOfMonth);
        var dailyReturnFilter   = returnStoreFilter & Builders<Return>.Filter.Gte(r => r.CreatedAt, startOfDay);
        var monthlyReturnFilter = returnStoreFilter & Builders<Return>.Filter.Gte(r => r.CreatedAt, startOfMonth);

        // ── Fire all queries in parallel ────────────────────────────────────
        var (dailySalesTask, monthlySalesTask, dailyReturnsTask, monthlyReturnsTask,
             inventoryTask, staffCountTask, totalSalesCountTask) = (
            _db.Sales.Find(dailySaleFilter).ToListAsync(),
            _db.Sales.Find(monthlySaleFilter).ToListAsync(),
            _db.Returns.Find(dailyReturnFilter).ToListAsync(),
            _db.Returns.Find(monthlyReturnFilter).ToListAsync(),
            _db.Inventories.Find(invFilter).ToListAsync(),
            _db.Users.CountDocumentsAsync(userFilter),
            _db.Sales.CountDocumentsAsync(storeFilter)
        );

        await Task.WhenAll(dailySalesTask, monthlySalesTask, dailyReturnsTask,
                           monthlyReturnsTask, inventoryTask, staffCountTask, totalSalesCountTask);

        var dailySales     = dailySalesTask.Result;
        var monthlySales   = monthlySalesTask.Result;
        var dailyReturns   = dailyReturnsTask.Result;
        var monthlyReturns = monthlyReturnsTask.Result;
        var inventory      = inventoryTask.Result;
        var staffCount     = staffCountTask.Result;
        var salesCount     = (int)totalSalesCountTask.Result;

        var grossDailyRevenue   = dailySales.Sum(s => s.TotalAmount);
        var dailyReturnAmount   = dailyReturns.Sum(r => r.RefundAmount);
        var dailyRevenue        = Math.Max(0, grossDailyRevenue - dailyReturnAmount);

        var grossMonthlyRevenue = monthlySales.Sum(s => s.TotalAmount);
        var monthlyReturnAmount = monthlyReturns.Sum(r => r.RefundAmount);
        var monthlyRevenue      = Math.Max(0, grossMonthlyRevenue - monthlyReturnAmount);

        // ── Inventory value ─────────────────────────────────────────────────
        var productIds = inventory.Select(i => i.ProductId).Distinct().ToList();
        var products = productIds.Count > 0
            ? await _db.Products.Find(Builders<Product>.Filter.In(p => p.Id, productIds)).ToListAsync()
            : new List<Product>();
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
    public async Task<IActionResult> SalesReport(
        [FromQuery] string? startDate,
        [FromQuery] string? endDate,
        [FromQuery] string? paymentMethod)
    {
        if (UserRole == "superuser") return StatusCode(403, new { success = false, message = "Forbidden: superuser cannot access store-level data" });
        if (UserRole != "owner" && UserRole != "manager" && UserRole != "staff") return StatusCode(403, new { success = false, message = "Forbidden" });

        var filter = FilterDefinition<Sale>.Empty;
        if (!string.IsNullOrWhiteSpace(UserStoreId))
            filter &= Builders<Sale>.Filter.Eq(s => s.StoreId, UserStoreId);

        // Default: cap to last 30 days when no explicit startDate is given.
        // This prevents a full collection scan when the Dashboard only needs 7 days of data.
        var effectiveStart = !string.IsNullOrWhiteSpace(startDate)
            ? DateTime.Parse(startDate).ToUniversalTime()
            : DateTime.UtcNow.AddDays(-30);
        filter &= Builders<Sale>.Filter.Gte(s => s.CreatedAt, effectiveStart);

        if (!string.IsNullOrWhiteSpace(endDate))
        {
            var end = DateTime.Parse(endDate).Date.AddDays(1).AddMilliseconds(-1).ToUniversalTime();
            filter &= Builders<Sale>.Filter.Lte(s => s.CreatedAt, end);
        }

        if (!string.IsNullOrWhiteSpace(paymentMethod) && paymentMethod != "all")
            filter &= Builders<Sale>.Filter.Eq(s => s.PaymentMethod, paymentMethod);

        // Build return filter scoped to the same date window for accuracy
        var returnFilter = FilterDefinition<Return>.Empty;
        if (!string.IsNullOrWhiteSpace(UserStoreId))
            returnFilter &= Builders<Return>.Filter.Eq(r => r.StoreId, UserStoreId);

        // Run sales + returns in parallel
        var (salesTask, returnsTask) = (
            _db.Sales.Find(filter).SortByDescending(s => s.CreatedAt).ToListAsync(),
            _db.Returns.Find(returnFilter).ToListAsync()
        );
        await Task.WhenAll(salesTask, returnsTask);

        var sales   = salesTask.Result;
        var returns = returnsTask.Result;

        var employeeIds = sales
            .Select(s => s.EmployeeId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();
        var employees = employeeIds.Count > 0
            ? await _db.Users.Find(Builders<User>.Filter.In(u => u.Id, employeeIds)).ToListAsync()
            : new List<User>();
        var employeeMap = employees.ToDictionary(u => u.Id!, u => new
        {
            _id = u.Id,
            name = string.IsNullOrWhiteSpace(u.DisplayName) ? u.Name : u.DisplayName,
            u.Email,
            u.Role
        });

        var returnMap = returns.GroupBy(r => r.SaleId)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.RefundAmount));

        var annotatedSales = sales.Select(s =>
        {
            var returned = returnMap.TryGetValue(s.Id!, out var amt) ? amt : 0m;
            var returnStatus = returned > 0
                ? (returned >= s.TotalAmount ? "returned" : "partial_return")
                : (string?)null;
            var employee = !string.IsNullOrWhiteSpace(s.EmployeeId) && employeeMap.TryGetValue(s.EmployeeId, out var foundEmployee)
                ? foundEmployee
                : null;
            return new
            {
                _id = s.Id,
                s.Items,
                s.TotalAmount,
                s.Subtotal,
                s.Tax,
                s.PaymentMethod,
                employeeId = employee,
                s.CustomerName,
                s.StoreId,
                s.ReceiptNumber,
                s.CreatedAt,
                returnedAmount = returned,
                returnStatus
            };
        }).ToList();

        // Get actual costs for profit calculation
        var productIds = sales.SelectMany(s => s.Items).Select(i => i.ProductId).Where(id => id != null).Distinct().ToList();
        var products = productIds.Count > 0 
            ? await _db.Products.Find(Builders<Product>.Filter.In(p => p.Id, productIds)).ToListAsync()
            : new List<Product>();
        var costMap = products.ToDictionary(p => p.Id!, p => p.CostPrice);

        decimal totalCost = 0;
        foreach (var sale in sales)
        {
            // Skip returned sales for profit calculation for simplicity, 
            // or calculate partial cost for partial returns if needed.
            // Let's just calculate cost of all items in the sale.
            foreach (var item in sale.Items)
            {
                if (item.ProductId != null && costMap.TryGetValue(item.ProductId, out var costPrice))
                {
                    totalCost += costPrice * item.Qty;
                }
            }
        }

        var grossRevenue = sales.Sum(s => s.TotalAmount);
        var totalReturned = returns.Sum(r => r.RefundAmount);
        var totalRevenue = Math.Max(0, grossRevenue - totalReturned);
        var totalOrders = sales.Count;

        // Actual profit is Revenue minus sum of Cost Prices (and refunds)
        // Simplified: (Gross Revenue - Cost) - Refunds
        // Wait, if an item is returned, we refund revenue but keep the item, so cost shouldn't be subtracted for profit.
        // Actually, Revenue - Refunds = Net Revenue. Net Revenue - Net Cost = Profit.
        // If an item is returned, is its cost recovered? Yes, back to inventory. 
        // We'll calculate profit per sale where it's not fully returned, or proportionally.
        // Let's do a more accurate calculation:
        decimal calculatedProfit = 0;
        foreach (var sale in sales)
        {
            var saleReturned = returnMap.TryGetValue(sale.Id!, out var refundAmt) ? refundAmt : 0m;
            decimal saleCost = 0;
            foreach (var item in sale.Items)
            {
                if (item.ProductId != null && costMap.TryGetValue(item.ProductId, out var costPrice))
                {
                    saleCost += costPrice * item.Qty;
                }
            }

            // Profit for this sale = Sale Revenue - Sale Cost
            // If partly or fully returned, we reduce both Revenue and Cost proportionally.
            decimal netSaleRevenue = Math.Max(0, sale.TotalAmount - saleReturned);
            decimal revenueRatio = sale.TotalAmount > 0 ? netSaleRevenue / sale.TotalAmount : 0;
            decimal netSaleCost = saleCost * revenueRatio;

            calculatedProfit += (netSaleRevenue - netSaleCost);
        }

        var totalProfit = calculatedProfit;
        var profitMargin = totalRevenue > 0 ? Math.Round(totalProfit / totalRevenue * 100, 1) : 0;

        return Ok(new
        {
            sales = annotatedSales,
            summary = new { grossRevenue, totalReturned, totalRevenue, totalOrders, totalProfit, profitMargin }
        });
    }
}
