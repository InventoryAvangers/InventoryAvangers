using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using InventoryAvengers.API.Data;
using InventoryAvengers.API.Models;

namespace InventoryAvengers.API.Controllers;

[ApiController]
[Route("api/billing")]
public class BillingController : ControllerBase
{
    private readonly MongoDbContext _db;

    public BillingController(MongoDbContext db) => _db = db;

    private string? UserStoreId => User.FindFirst("storeId")?.Value is { Length: > 0 } s ? s : null;

    // POST /api/billing/validate-coupon
    [HttpPost("validate-coupon")]
    [Authorize]
    public async Task<IActionResult> ValidateCoupon([FromBody] ValidateCouponRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Code))
            return BadRequest(new { success = false, message = "Coupon code required" });

        var coupon = await _db.Coupons
            .Find(c => c.Code == req.Code.ToUpper() && c.IsActive)
            .FirstOrDefaultAsync();

        if (coupon == null)
            return NotFound(new { success = false, message = "Invalid or inactive coupon code" });

        if (coupon.ExpiresAt.HasValue && coupon.ExpiresAt < DateTime.UtcNow)
            return BadRequest(new { success = false, message = "Coupon has expired" });

        if (coupon.MaxUses > 0 && coupon.UsedCount >= coupon.MaxUses)
            return BadRequest(new { success = false, message = "Coupon usage limit reached" });

        if (!string.IsNullOrWhiteSpace(req.Plan)
            && coupon.ApplicablePlans.Count > 0
            && !coupon.ApplicablePlans.Contains(req.Plan))
            return BadRequest(new { success = false, message = $"Coupon not applicable to the {req.Plan} plan" });

        return Ok(new
        {
            success = true,
            coupon = new { coupon.Code, coupon.DiscountPercent, coupon.ApplicablePlans }
        });
    }

    // GET /api/billing/subscription
    [HttpGet("subscription")]
    [Authorize]
    public async Task<IActionResult> GetSubscription()
    {
        if (string.IsNullOrWhiteSpace(UserStoreId))
            return BadRequest(new { success = false, message = "No store associated" });

        var sub = await _db.Subscriptions.Find(s => s.StoreId == UserStoreId).FirstOrDefaultAsync();
        return Ok(new { success = true, data = sub });
    }
}

public class ValidateCouponRequest
{
    public string Code { get; set; } = string.Empty;
    public string? Plan { get; set; }
}
