using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using InventoryAvengers.API.Data;
using InventoryAvengers.API.Models;
using InventoryAvengers.API.Services;

namespace InventoryAvengers.API.Controllers;

[ApiController]
[Route("api/superuser")]
[Authorize]
public class SuperuserController : ControllerBase
{
    private readonly MongoDbContext _db;
    private readonly AuthService _authService;
    private readonly TrialStatusService _trialStatusService;

    public SuperuserController(MongoDbContext db, AuthService authService, TrialStatusService trialStatusService)
    {
        _db = db;
        _authService = authService;
        _trialStatusService = trialStatusService;
    }

    private string? UserId   => User.FindFirst("id")?.Value;
    private string? UserRole => User.FindFirst("role")?.Value;

    private static readonly string[] ValidPlans = { "free", "pro" };

    private IActionResult ForbidIfNotSuperuser()
    {
        if (UserRole != "superuser")
            return StatusCode(403, new { success = false, message = "Forbidden" });
        return null!;
    }

    // ── PROFILE ────────────────────────────────────────────────────────────

    // GET /api/superuser
    [HttpGet]
    public async Task<IActionResult> GetProfile()
    {
        if (ForbidIfNotSuperuser() is { } f) return f;
        var user = await _db.Users.Find(u => u.Id == UserId).FirstOrDefaultAsync();
        if (user == null) return NotFound(new { success = false, message = "Superuser not found" });
        return Ok(new { success = true, data = new {
            _id = user.Id,
            user.Name,
            user.Email,
            user.Role,
            user.Status,
            user.StoreId,
            user.CreatedAt,
            user.LastLogin
        } });
    }

    // ── ACCESS REQUESTS ────────────────────────────────────────────────────

    // GET /api/superuser/access-requests
    [HttpGet("access-requests")]
    public async Task<IActionResult> GetAccessRequests()
    {
        if (ForbidIfNotSuperuser() is { } f) return f;
        var requests = await _db.AccessRequests.Find(_ => true)
            .SortByDescending(r => r.CreatedAt).ToListAsync();
        return Ok(new { success = true, data = requests });
    }

    // POST /api/superuser/access-requests/:id/approve
    [HttpPost("access-requests/{id}/approve")]
    public async Task<IActionResult> ApproveAccessRequest(string id)
    {
        if (ForbidIfNotSuperuser() is { } f) return f;

        var request = await _db.AccessRequests.Find(r => r.Id == id).FirstOrDefaultAsync();
        if (request == null) return NotFound(new { success = false, message = "Request not found" });
        if (request.Status != "pending")
            return BadRequest(new { success = false, message = "Request already processed" });

        var existing = await _db.Users.Find(u => u.Email == request.Email).FirstOrDefaultAsync();
        if (existing != null)
            return BadRequest(new { success = false, message = "A user with this email already exists" });

        Store store;
        if (!string.IsNullOrWhiteSpace(request.StoreId))
        {
            store = await _db.Stores.Find(s => s.Id == request.StoreId).FirstOrDefaultAsync()
                ?? throw new Exception("Selected store not found");
        }
        else
        {
            store = new Store
            {
                Name  = !string.IsNullOrWhiteSpace(request.BusinessName) ? request.BusinessName : $"{request.Name}'s Store",
                Code  = HelperService.GenerateStoreCode(),
                Status = "trial",
                TrialExpiresAt = DateTime.UtcNow.AddDays(14)
            };
            await _db.Stores.InsertOneAsync(store);
        }

        string passwordValue;
        bool mustChangePassword = false;
        if (!string.IsNullOrWhiteSpace(request.PasswordHash))
        {
            passwordValue = request.PasswordHash;
        }
        else
        {
            var random = System.Security.Cryptography.RandomNumberGenerator.GetBytes(9);
            passwordValue = _authService.HashPassword(Convert.ToBase64String(random) + "A1!");
            mustChangePassword = true;
        }

        var owner = new User
        {
            Name               = request.Name,
            Email              = request.Email,
            PasswordHash       = passwordValue,
            Role               = "owner",
            Status             = "approved",
            StoreId            = store.Id,
            MustChangePassword = mustChangePassword
        };
        await _db.Users.InsertOneAsync(owner);

        await _db.Stores.UpdateOneAsync(s => s.Id == store.Id,
            Builders<Store>.Update.Set(s => s.OwnerId, owner.Id));

        await _db.Subscriptions.InsertOneAsync(new Subscription
        {
            StoreId        = store.Id!,
            Plan           = "free",
            Status         = "trial",
            TrialExpiresAt = store.TrialExpiresAt
        });

        var featureDefaults = FeatureFlag.GetDefaults("free");
        await _db.FeatureFlags.InsertOneAsync(new FeatureFlag
        {
            StoreId  = store.Id!,
            Features = featureDefaults
        });

        await _db.AccessRequests.UpdateOneAsync(r => r.Id == id,
            Builders<AccessRequest>.Update
                .Set(r => r.Status, "approved")
                .Set(r => r.ReviewedBy, UserId)
                .Set(r => r.ReviewedAt, DateTime.UtcNow)
                .Set(r => r.CreatedOwner, owner.Id));

        return Ok(new { success = true, message = "Access request approved", ownerId = owner.Id, storeId = store.Id });
    }

    // POST /api/superuser/access-requests/:id/reject
    [HttpPost("access-requests/{id}/reject")]
    public async Task<IActionResult> RejectAccessRequest(string id, [FromBody] RejectRequest? req)
    {
        if (ForbidIfNotSuperuser() is { } f) return f;

        var request = await _db.AccessRequests.Find(r => r.Id == id).FirstOrDefaultAsync();
        if (request == null) return NotFound(new { success = false, message = "Request not found" });

        await _db.AccessRequests.UpdateOneAsync(r => r.Id == id,
            Builders<AccessRequest>.Update
                .Set(r => r.Status, "rejected")
                .Set(r => r.ReviewedBy, UserId)
                .Set(r => r.ReviewedAt, DateTime.UtcNow));

        return Ok(new { success = true, message = "Access request rejected" });
    }

    // ── SHOPS ──────────────────────────────────────────────────────────────

    // GET /api/superuser/stores (alias: shops)
    [HttpGet("stores")]
    [HttpGet("shops")]
    public async Task<IActionResult> GetStores([FromQuery] string? status, [FromQuery] string? plan, [FromQuery] string? search)
    {
        if (ForbidIfNotSuperuser() is { } f) return f;

        await _trialStatusService.SyncExpiredTrialsAsync();

        var filter = Builders<Store>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(status)) filter &= Builders<Store>.Filter.Eq(s => s.Status, status);
        if (!string.IsNullOrWhiteSpace(plan))   filter &= Builders<Store>.Filter.Eq(s => s.Plan,   plan);
        if (!string.IsNullOrWhiteSpace(search))
            filter &= Builders<Store>.Filter.Regex(s => s.Name, new MongoDB.Bson.BsonRegularExpression(search, "i"));

        var stores = await _db.Stores.Find(filter).SortByDescending(s => s.CreatedAt).ToListAsync();

        var storeIds = stores.Select(s => s.Id).ToList();
        var subs = await _db.Subscriptions
            .Find(Builders<Subscription>.Filter.In(s => s.StoreId, storeIds))
            .ToListAsync();
        var subMap = subs.ToDictionary(s => s.StoreId, s => s);

        var data = stores.Select(store => new
        {
            _id          = store.Id,
            store.Name,
            store.Code,
            store.Status,
            store.Plan,
            store.IsActive,
            store.TrialExpiresAt,
            store.SubscriptionExpiresAt,
            store.OwnerId,
            store.ManagerId,
            store.CreatedAt,
            trialDaysLeft = TrialStatusService.GetTrialDaysLeft(store),
            subscription = subMap.TryGetValue(store.Id!, out var sub) ? sub : null
        }).ToList();

        return Ok(new { success = true, data });
    }

    // GET /api/superuser/shops/:id
    [HttpGet("shops/{id}")]
    public async Task<IActionResult> GetShop(string id)
    {
        if (ForbidIfNotSuperuser() is { } f) return f;

        await _trialStatusService.SyncExpiredTrialsAsync(id);

        var store = await _db.Stores.Find(s => s.Id == id).FirstOrDefaultAsync();
        if (store == null) return NotFound(new { success = false, message = "Shop not found" });

        var sub   = await _db.Subscriptions.Find(s => s.StoreId == id).FirstOrDefaultAsync();
        var flags = await _db.FeatureFlags.Find(f => f.StoreId == id).FirstOrDefaultAsync();
        var totalOrders   = await _db.Sales.CountDocumentsAsync(s => s.StoreId == id);
        var totalProducts = await _db.Products.CountDocumentsAsync(p => p.StoreId == id);

        return Ok(new { success = true, data = new {
            _id          = store.Id,
            store.Name,
            store.Code,
            store.Status,
            store.Plan,
            store.IsActive,
            store.TrialExpiresAt,
            store.SubscriptionExpiresAt,
            store.OwnerId,
            store.ManagerId,
            store.CreatedAt,
            trialDaysLeft = TrialStatusService.GetTrialDaysLeft(store),
            subscription  = sub,
            featureFlags  = flags,
            totalOrders,
            totalProducts
        } });
    }

    // PATCH /api/superuser/shops/:id/approve
    [HttpPatch("shops/{id}/approve")]
    public async Task<IActionResult> ApproveShop(string id)
    {
        if (ForbidIfNotSuperuser() is { } f) return f;

        var store = await _db.Stores.FindOneAndUpdateAsync(
            s => s.Id == id,
            Builders<Store>.Update.Set(s => s.Status, "active").Set(s => s.IsActive, true),
            new FindOneAndUpdateOptions<Store> { ReturnDocument = ReturnDocument.After });

        if (store == null) return NotFound(new { success = false, message = "Shop not found" });

        await _db.Subscriptions.UpdateOneAsync(s => s.StoreId == id,
            Builders<Subscription>.Update.Set(s => s.Status, "active"));

        await LogActivity("shop.approved", id, "store");
        return Ok(new { success = true, message = "Shop approved and activated.", store });
    }

    // PATCH /api/superuser/shops/:id/reject
    [HttpPatch("shops/{id}/reject")]
    public async Task<IActionResult> RejectShop(string id, [FromBody] RejectRequest? req)
    {
        if (ForbidIfNotSuperuser() is { } f) return f;

        var store = await _db.Stores.FindOneAndUpdateAsync(
            s => s.Id == id,
            Builders<Store>.Update.Set(s => s.Status, "inactive").Set(s => s.IsActive, false),
            new FindOneAndUpdateOptions<Store> { ReturnDocument = ReturnDocument.After });

        if (store == null) return NotFound(new { success = false, message = "Shop not found" });

        await LogActivity("shop.rejected", id, "store");
        return Ok(new { success = true, message = "Shop rejected.", store });
    }

    // PATCH /api/superuser/shops/:id/suspend
    [HttpPatch("shops/{id}/suspend")]
    public async Task<IActionResult> SuspendShop(string id, [FromBody] RejectRequest? req)
    {
        if (ForbidIfNotSuperuser() is { } f) return f;

        var store = await _db.Stores.FindOneAndUpdateAsync(
            s => s.Id == id,
            Builders<Store>.Update.Set(s => s.Status, "suspended").Set(s => s.IsActive, false),
            new FindOneAndUpdateOptions<Store> { ReturnDocument = ReturnDocument.After });

        if (store == null) return NotFound(new { success = false, message = "Shop not found" });

        if (!string.IsNullOrWhiteSpace(store.OwnerId))
            await _db.Users.UpdateOneAsync(u => u.Id == store.OwnerId,
                Builders<User>.Update.Set(u => u.Status, "suspended"));

        await LogActivity("shop.suspended", id, "store");
        return Ok(new { success = true, message = "Shop suspended.", store });
    }

    // PATCH /api/superuser/shops/:id/unsuspend
    [HttpPatch("shops/{id}/unsuspend")]
    public async Task<IActionResult> UnsuspendShop(string id)
    {
        if (ForbidIfNotSuperuser() is { } f) return f;

        var store = await _db.Stores.FindOneAndUpdateAsync(
            s => s.Id == id,
            Builders<Store>.Update.Set(s => s.Status, "active").Set(s => s.IsActive, true),
            new FindOneAndUpdateOptions<Store> { ReturnDocument = ReturnDocument.After });

        if (store == null) return NotFound(new { success = false, message = "Shop not found" });

        if (!string.IsNullOrWhiteSpace(store.OwnerId))
            await _db.Users.UpdateOneAsync(u => u.Id == store.OwnerId,
                Builders<User>.Update.Set(u => u.Status, "approved"));

        await LogActivity("shop.unsuspended", id, "store");
        return Ok(new { success = true, message = "Shop unsuspended.", store });
    }

    // DELETE /api/superuser/shops/:id
    [HttpDelete("shops/{id}")]
    public async Task<IActionResult> DeleteShop(string id)
    {
        if (ForbidIfNotSuperuser() is { } f) return f;

        var store = await _db.Stores.Find(s => s.Id == id).FirstOrDefaultAsync();
        if (store == null) return NotFound(new { success = false, message = "Shop not found" });

        await Task.WhenAll(
            _db.Users.DeleteManyAsync(u => u.StoreId == id),
            _db.Subscriptions.DeleteManyAsync(s => s.StoreId == id),
            _db.FeatureFlags.DeleteManyAsync(f => f.StoreId == id),
            _db.Sales.DeleteManyAsync(s => s.StoreId == id),
            _db.Products.DeleteManyAsync(p => p.StoreId == id),
            _db.Stores.DeleteOneAsync(s => s.Id == id)
        );

        await LogActivity("shop.deleted", null, "store", new { storeName = store.Name, storeId = id });
        return Ok(new { success = true, message = "Shop and all associated data permanently deleted." });
    }

    // PATCH /api/superuser/shops/:id/extend-trial
    [HttpPatch("shops/{id}/extend-trial")]
    public async Task<IActionResult> ExtendTrial(string id, [FromBody] ExtendTrialRequest req)
    {
        if (ForbidIfNotSuperuser() is { } f) return f;

        if (req.Days < 1)
            return BadRequest(new { success = false, message = "Provide a positive number of days (minimum 1)" });

        var store = await _db.Stores.Find(s => s.Id == id).FirstOrDefaultAsync();
        if (store == null) return NotFound(new { success = false, message = "Shop not found" });

        var baseDate = store.TrialExpiresAt > DateTime.UtcNow ? store.TrialExpiresAt : DateTime.UtcNow;
        var newExpiry = baseDate.AddDays(req.Days);
        var newStatus = store.Status == "expired" ? "trial" : store.Status;

        await _db.Stores.UpdateOneAsync(s => s.Id == id,
            Builders<Store>.Update
                .Set(s => s.TrialExpiresAt, newExpiry)
                .Set(s => s.Status, newStatus));

        await _db.Subscriptions.UpdateOneAsync(s => s.StoreId == id,
            Builders<Subscription>.Update
                .Set(s => s.TrialExpiresAt, newExpiry)
                .Set(s => s.Status, "trial"));

        await LogActivity("shop.trial_extended", id, "store", new { days = req.Days, newExpiry });
        return Ok(new { success = true, message = $"Trial extended by {req.Days} day(s).", trialExpiresAt = newExpiry });
    }

    // PATCH /api/superuser/shops/:id/override-plan
    [HttpPatch("shops/{id}/override-plan")]
    public async Task<IActionResult> OverridePlan(string id, [FromBody] OverridePlanRequest req)
    {
        if (ForbidIfNotSuperuser() is { } f) return f;

        if (!ValidPlans.Contains(req.Plan))
            return BadRequest(new { success = false, message = "Plan must be free or pro" });

        var store = await _db.Stores.FindOneAndUpdateAsync(
            s => s.Id == id,
            Builders<Store>.Update
                .Set(s => s.Plan, req.Plan)
                .Set(s => s.Status, "active")
                .Set(s => s.IsActive, true),
            new FindOneAndUpdateOptions<Store> { ReturnDocument = ReturnDocument.After });

        if (store == null) return NotFound(new { success = false, message = "Shop not found" });

        await _db.Subscriptions.UpdateOneAsync(s => s.StoreId == id,
            Builders<Subscription>.Update
                .Set(s => s.Plan, req.Plan)
                .Set(s => s.Status, "active"));

        var defaults = FeatureFlag.GetDefaults(req.Plan);
        await _db.FeatureFlags.UpdateOneAsync(
            f => f.StoreId == id,
            Builders<FeatureFlag>.Update.Set(f => f.Features, defaults),
            new UpdateOptions { IsUpsert = true });

        await LogActivity("shop.plan_overridden", id, "store", new { plan = req.Plan, reason = req.Reason ?? "" });
        return Ok(new { success = true, message = $"Plan overridden to {req.Plan}.", store });
    }

    // PUT /api/superuser/stores/:id/status (legacy)
    [HttpPut("stores/{id}/status")]
    public async Task<IActionResult> UpdateStoreStatus(string id, [FromBody] UpdateStatusRequest req)
    {
        if (ForbidIfNotSuperuser() is { } f) return f;

        var store = await _db.Stores.Find(s => s.Id == id).FirstOrDefaultAsync();
        if (store == null) return NotFound(new { success = false, message = "Store not found" });

        await _db.Stores.UpdateOneAsync(s => s.Id == id,
            Builders<Store>.Update.Set(s => s.Status, req.Status));

        return Ok(new { success = true, message = $"Store status updated to {req.Status}" });
    }

    // ── OWNERS ─────────────────────────────────────────────────────────────

    // GET /api/superuser/owners
    [HttpGet("owners")]
    public async Task<IActionResult> GetOwners()
    {
        if (ForbidIfNotSuperuser() is { } f) return f;
        return await GetUsersInternal(role: "owner", status: null, storeId: null);
    }

    // PUT /api/superuser/owners/:id/deactivate
    [HttpPut("owners/{id}/deactivate")]
    public async Task<IActionResult> DeactivateOwner(string id)
    {
        if (ForbidIfNotSuperuser() is { } f) return f;

        var owner = await _db.Users.FindOneAndUpdateAsync(
            u => u.Id == id && u.Role == "owner",
            Builders<User>.Update.Set(u => u.Status, "deactivated"),
            new FindOneAndUpdateOptions<User> { ReturnDocument = ReturnDocument.After });

        if (owner == null) return NotFound(new { success = false, message = "Owner not found" });
        await LogActivity("owner.deactivated", id, "user");
        return Ok(new { success = true, message = "Owner deactivated", owner = new { _id = owner.Id, owner.Name, owner.Email, owner.Role, owner.Status } });
    }

    // PUT /api/superuser/owners/:id/activate
    [HttpPut("owners/{id}/activate")]
    public async Task<IActionResult> ActivateOwner(string id)
    {
        if (ForbidIfNotSuperuser() is { } f) return f;

        var owner = await _db.Users.FindOneAndUpdateAsync(
            u => u.Id == id && u.Role == "owner",
            Builders<User>.Update.Set(u => u.Status, "approved"),
            new FindOneAndUpdateOptions<User> { ReturnDocument = ReturnDocument.After });

        if (owner == null) return NotFound(new { success = false, message = "Owner not found" });
        await LogActivity("owner.activated", id, "user");
        return Ok(new { success = true, message = "Owner activated", owner = new { _id = owner.Id, owner.Name, owner.Email, owner.Role, owner.Status } });
    }

    // DELETE /api/superuser/owners/:id
    [HttpDelete("owners/{id}")]
    public async Task<IActionResult> DeleteOwner(string id)
    {
        if (ForbidIfNotSuperuser() is { } f) return f;

        var owner = await _db.Users.FindOneAndDeleteAsync(u => u.Id == id && u.Role == "owner");
        if (owner == null) return NotFound(new { success = false, message = "Owner not found" });
        await LogActivity("owner.deleted", id, "user");
        return Ok(new { success = true, message = "Owner deleted" });
    }

    // ── USERS ──────────────────────────────────────────────────────────────

    // GET /api/superuser/users
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers([FromQuery] string? role, [FromQuery] string? status, [FromQuery] string? storeId)
    {
        if (ForbidIfNotSuperuser() is { } f) return f;
        return await GetUsersInternal(role, status, storeId);
    }

    private async Task<IActionResult> GetUsersInternal(string? role, string? status, string? storeId)
    {
        var filter = Builders<User>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(role))    filter &= Builders<User>.Filter.Eq(u => u.Role,    role);
        if (!string.IsNullOrWhiteSpace(status))  filter &= Builders<User>.Filter.Eq(u => u.Status,  status);
        if (!string.IsNullOrWhiteSpace(storeId)) filter &= Builders<User>.Filter.Eq(u => u.StoreId, storeId);

        var users = await _db.Users.Find(filter).SortByDescending(u => u.CreatedAt).ToListAsync();
        var result = users.Select(u => new {
            _id = u.Id,
            u.Name,
            u.Email,
            u.Role,
            u.Status,
            u.StoreId,
            u.CreatedAt,
            u.LastLogin
        });
        return Ok(new { success = true, data = result });
    }

    // PUT /api/superuser/users/:id/status
    [HttpPut("users/{id}/status")]
    public async Task<IActionResult> UpdateUserStatus(string id, [FromBody] UpdateStatusRequest req)
    {
        if (ForbidIfNotSuperuser() is { } f) return f;

        var validStatuses = new[] { "approved", "suspended", "deactivated", "pending", "rejected" };
        if (!validStatuses.Contains(req.Status))
            return BadRequest(new { success = false, message = "Invalid status" });

        var user = await _db.Users.Find(u => u.Id == id).FirstOrDefaultAsync();
        if (user == null) return NotFound(new { success = false, message = "User not found" });

        await _db.Users.UpdateOneAsync(u => u.Id == id,
            Builders<User>.Update.Set(u => u.Status, req.Status));

        return Ok(new { success = true, message = $"User status updated to {req.Status}" });
    }

    // PUT /api/superuser/users/:id/superuser-role
    [HttpPut("users/{id}/superuser-role")]
    public async Task<IActionResult> AssignSuperuserRole(string id, [FromBody] AssignRoleRequest req)
    {
        if (ForbidIfNotSuperuser() is { } f) return f;

        var user = await _db.Users.Find(u => u.Id == id).FirstOrDefaultAsync();
        if (user == null) return NotFound(new { success = false, message = "User not found" });
        if (user.Role != "superuser")
            return BadRequest(new { success = false, message = "User is not a superuser" });

        await _db.Users.UpdateOneAsync(u => u.Id == id,
            Builders<User>.Update.Set(u => u.SuperuserRoleId, req.SuperuserRoleId));

        return Ok(new { success = true, data = new { _id = user.Id, user.Name, user.Email, user.Role, superuserRoleId = req.SuperuserRoleId } });
    }

    // ── FEATURE FLAGS ──────────────────────────────────────────────────────

    // GET /api/superuser/feature-flags (optional query: ?storeId=)
    [HttpGet("feature-flags")]
    public async Task<IActionResult> GetFeatureFlags([FromQuery] string? storeId)
    {
        if (ForbidIfNotSuperuser() is { } f) return f;

        var filter = Builders<FeatureFlag>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(storeId))
            filter = Builders<FeatureFlag>.Filter.Eq(ff => ff.StoreId, storeId);

        var flags = await _db.FeatureFlags.Find(filter).ToListAsync();
        return Ok(new { success = true, data = flags });
    }

    // GET /api/superuser/feature-flags/:storeId
    [HttpGet("feature-flags/{storeId}")]
    public async Task<IActionResult> GetFeatureFlagsByStore(string storeId)
    {
        if (ForbidIfNotSuperuser() is { } f) return f;

        var flags = await _db.FeatureFlags.Find(f => f.StoreId == storeId).FirstOrDefaultAsync();
        if (flags == null) return NotFound(new { success = false, message = "Feature flags not found for this store" });
        return Ok(new { success = true, data = flags });
    }

    // PUT /api/superuser/feature-flags/:storeId (full replace)
    [HttpPut("feature-flags/{storeId}")]
    public async Task<IActionResult> UpdateFeatureFlags(string storeId, [FromBody] FeatureSet featureSet)
    {
        if (ForbidIfNotSuperuser() is { } f) return f;

        var result = await _db.FeatureFlags.FindOneAndUpdateAsync(
            ff => ff.StoreId == storeId,
            Builders<FeatureFlag>.Update
                .Set(ff => ff.Features, featureSet)
                .Set(ff => ff.UpdatedAt, DateTime.UtcNow),
            new FindOneAndUpdateOptions<FeatureFlag> { ReturnDocument = ReturnDocument.After, IsUpsert = true });

        return Ok(new { success = true, data = result });
    }

    // PATCH /api/superuser/feature-flags/:storeId (partial update)
    [HttpPatch("feature-flags/{storeId}")]
    public async Task<IActionResult> PatchFeatureFlags(string storeId, [FromBody] PatchFeaturesRequest req)
    {
        if (ForbidIfNotSuperuser() is { } f) return f;

        if (req.Features == null || req.Features.Count == 0)
            return BadRequest(new { success = false, message = "features object required" });

        var flags = await _db.FeatureFlags.Find(f => f.StoreId == storeId).FirstOrDefaultAsync();
        var features = flags?.Features ?? FeatureFlag.GetDefaults("free");

        foreach (var kv in req.Features)
        {
            switch (kv.Key)
            {
                case "inventory":  features.Inventory  = kv.Value; break;
                case "pos":        features.Pos        = kv.Value; break;
                case "returns":    features.Returns    = kv.Value; break;
                case "reports":    features.Reports    = kv.Value; break;
                case "pdfExport":  features.PdfExport  = kv.Value; break;
                case "employees":  features.Employees  = kv.Value; break;
                case "payments":   features.Payments   = kv.Value; break;
                case "apiAccess":  features.ApiAccess  = kv.Value; break;
                case "darkMode":   features.DarkMode   = kv.Value; break;
            }
        }

        var result = await _db.FeatureFlags.FindOneAndUpdateAsync(
            f => f.StoreId == storeId,
            Builders<FeatureFlag>.Update
                .Set(f => f.Features, features)
                .Set(f => f.UpdatedAt, DateTime.UtcNow),
            new FindOneAndUpdateOptions<FeatureFlag> { ReturnDocument = ReturnDocument.After, IsUpsert = true });

        await LogActivity("feature_flags.updated", storeId, "store");
        return Ok(new { success = true, message = "Feature flags updated.", data = result });
    }

    // ── COUPONS ────────────────────────────────────────────────────────────

    // GET /api/superuser/coupons
    [HttpGet("coupons")]
    public async Task<IActionResult> GetCoupons()
    {
        if (ForbidIfNotSuperuser() is { } f) return f;
        var coupons = await _db.Coupons.Find(_ => true).SortByDescending(c => c.CreatedAt).ToListAsync();
        return Ok(new { success = true, data = coupons });
    }

    // POST /api/superuser/coupons
    [HttpPost("coupons")]
    public async Task<IActionResult> CreateCoupon([FromBody] CreateCouponRequest req)
    {
        if (ForbidIfNotSuperuser() is { } f) return f;

        if (string.IsNullOrWhiteSpace(req.Code) || req.DiscountPercent <= 0)
            return BadRequest(new { success = false, message = "code and discountPercent are required" });

        var coupon = new Coupon
        {
            Code             = req.Code.ToUpper(),
            DiscountPercent  = req.DiscountPercent,
            MaxUses          = req.MaxUses,
            ExpiresAt        = req.ExpiresAt,
            ApplicablePlans  = req.ApplicablePlans ?? new List<string>(ValidPlans)
        };

        try
        {
            await _db.Coupons.InsertOneAsync(coupon);
        }
        catch (MongoDB.Driver.MongoWriteException ex) when (ex.WriteError.Category == MongoDB.Driver.ServerErrorCategory.DuplicateKey)
        {
            return BadRequest(new { success = false, message = "Coupon code already exists" });
        }

        await LogActivity("coupon.created", coupon.Id, "coupon");
        return StatusCode(201, new { success = true, message = "Coupon created.", data = coupon });
    }

    // PUT /api/superuser/coupons/:id
    [HttpPut("coupons/{id}")]
    public async Task<IActionResult> UpdateCoupon(string id, [FromBody] CreateCouponRequest req)
    {
        if (ForbidIfNotSuperuser() is { } f) return f;

        var updates = new List<UpdateDefinition<Coupon>>();
        if (!string.IsNullOrWhiteSpace(req.Code))      updates.Add(Builders<Coupon>.Update.Set(c => c.Code, req.Code.ToUpper()));
        if (req.DiscountPercent > 0)                   updates.Add(Builders<Coupon>.Update.Set(c => c.DiscountPercent, req.DiscountPercent));
        if (req.MaxUses >= 0)                          updates.Add(Builders<Coupon>.Update.Set(c => c.MaxUses, req.MaxUses));
        if (req.ExpiresAt.HasValue)                    updates.Add(Builders<Coupon>.Update.Set(c => c.ExpiresAt, req.ExpiresAt));
        if (req.ApplicablePlans != null)               updates.Add(Builders<Coupon>.Update.Set(c => c.ApplicablePlans, req.ApplicablePlans));

        if (updates.Count == 0)
            return BadRequest(new { success = false, message = "No fields to update" });

        var coupon = await _db.Coupons.FindOneAndUpdateAsync(
            c => c.Id == id,
            Builders<Coupon>.Update.Combine(updates),
            new FindOneAndUpdateOptions<Coupon> { ReturnDocument = ReturnDocument.After });

        if (coupon == null) return NotFound(new { success = false, message = "Coupon not found" });
        return Ok(new { success = true, data = coupon });
    }

    // DELETE /api/superuser/coupons/:id
    [HttpDelete("coupons/{id}")]
    public async Task<IActionResult> DeleteCoupon(string id)
    {
        if (ForbidIfNotSuperuser() is { } f) return f;

        var coupon = await _db.Coupons.FindOneAndDeleteAsync(c => c.Id == id);
        if (coupon == null) return NotFound(new { success = false, message = "Coupon not found" });
        await LogActivity("coupon.deleted", id, "coupon");
        return Ok(new { success = true, message = "Coupon deleted." });
    }

    // ── DASHBOARD ──────────────────────────────────────────────────────────

    // GET /api/superuser/stats (alias: dashboard)
    [HttpGet("stats")]
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetStats()
    {
        if (ForbidIfNotSuperuser() is { } f) return f;

        await _trialStatusService.SyncExpiredTrialsAsync();

        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var expiringSoonCutoff = now.AddDays(7);

        var totalUsers       = await _db.Users.CountDocumentsAsync(u => u.Status == "approved");
        var activeShops      = await _db.Stores.CountDocumentsAsync(s => s.Status == "active");
        var trialShops       = await _db.Stores.CountDocumentsAsync(s => s.Status == "trial");
        var pendingRequests  = await _db.AccessRequests.CountDocumentsAsync(r => r.Status == "pending");
        var newShopsThisMonth = await _db.Stores.CountDocumentsAsync(s => s.CreatedAt >= startOfMonth);
        var stores = await _db.Stores.Find(_ => true).ToListAsync();

        var shopStatusBreakdown = stores
            .GroupBy(s => string.IsNullOrWhiteSpace(s.Status) ? "inactive" : s.Status.Trim().ToLowerInvariant())
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.Count());

        var trialExpiringSoon = stores
            .Where(s => s.Status == "trial" && s.TrialExpiresAt >= now && s.TrialExpiresAt <= expiringSoonCutoff)
            .OrderBy(s => s.TrialExpiresAt)
            .Take(5)
            .Select(s => new
            {
                _id = s.Id,
                name = s.Name,
                code = s.Code,
                trialExpiresAt = s.TrialExpiresAt,
                trialDaysLeft = TrialStatusService.GetTrialDaysLeft(s)
            })
            .ToList();

        return Ok(new
        {
            success = true,
            data = new
            {
                totalUsers,
                activeShops,
                trialShops,
                pendingRequests,
                newShopsThisMonth,
                shopStatusBreakdown,
                trialExpiringSoon
            }
        });
    }

    // ── LOGS ───────────────────────────────────────────────────────────────

    // GET /api/superuser/logs
    [HttpGet("logs")]
    public async Task<IActionResult> GetLogs([FromQuery] string? storeId, [FromQuery] string? action,
        [FromQuery] string? from, [FromQuery] string? to,
        [FromQuery] int page = 1, [FromQuery] int limit = 50)
    {
        if (ForbidIfNotSuperuser() is { } f) return f;

        var filter = Builders<ActivityLog>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(storeId)) filter &= Builders<ActivityLog>.Filter.Eq(l => l.StoreId, storeId);
        if (!string.IsNullOrWhiteSpace(action))
            filter &= Builders<ActivityLog>.Filter.Regex(l => l.Action, new MongoDB.Bson.BsonRegularExpression(action, "i"));
        if (!string.IsNullOrWhiteSpace(from))
        {
            if (!DateTime.TryParse(from, out var fromDate))
                return BadRequest(new { success = false, message = "Invalid 'from' date format" });
            filter &= Builders<ActivityLog>.Filter.Gte(l => l.CreatedAt, fromDate.ToUniversalTime());
        }
        if (!string.IsNullOrWhiteSpace(to))
        {
            if (!DateTime.TryParse(to, out var toDate))
                return BadRequest(new { success = false, message = "Invalid 'to' date format" });
            filter &= Builders<ActivityLog>.Filter.Lte(l => l.CreatedAt, toDate.ToUniversalTime());
        }

        var logs = await _db.ActivityLogs.Find(filter)
            .SortByDescending(l => l.CreatedAt)
            .Skip((page - 1) * limit)
            .Limit(limit)
            .ToListAsync();
        var total = await _db.ActivityLogs.CountDocumentsAsync(filter);

        var actorIds = logs.Select(l => l.ActorId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        var storeIds = logs.Select(l => !string.IsNullOrEmpty(l.StoreId) ? l.StoreId : (l.TargetType == "store" ? l.TargetId : null))
                           .Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();

        var users = await _db.Users.Find(Builders<User>.Filter.In(u => u.Id, actorIds)).ToListAsync();
        var stores = await _db.Stores.Find(Builders<Store>.Filter.In(s => s.Id, storeIds)).ToListAsync();

        var userMap = users.ToDictionary(u => u.Id!, u => new { _id = u.Id, name = u.Name, email = u.Email, role = u.Role });
        var storeMap = stores.ToDictionary(s => s.Id!, s => new { _id = s.Id, name = s.Name });

        var annotatedLogs = logs.Select(l => 
        {
            var rStoreId = !string.IsNullOrEmpty(l.StoreId) ? l.StoreId : (l.TargetType == "store" ? l.TargetId : null);
            var isUserFound = l.ActorId != null && userMap.TryGetValue(l.ActorId, out var actor);
            var rRole = !string.IsNullOrEmpty(l.ActorRole) ? l.ActorRole : (isUserFound ? userMap[l.ActorId!].role : "system");

            return new
            {
                _id = l.Id,
                action = l.Action,
                metadata = l.Metadata,
                timestamp = l.CreatedAt, // Frontend mapping
                createdAt = l.CreatedAt,
                actorRole = rRole,
                actorId = isUserFound ? userMap[l.ActorId!] : null,
                storeId = rStoreId != null && storeMap.TryGetValue(rStoreId, out var store) ? store : null
            };
        }).ToList();

        return Ok(new { success = true, data = annotatedLogs, total, page, pages = (int)Math.Ceiling(total / (double)limit) });
    }

    // ── MESSAGES ───────────────────────────────────────────────────────────

    // POST /api/superuser/messages/broadcast
    [HttpPost("messages/broadcast")]
    public async Task<IActionResult> BroadcastMessage([FromBody] BroadcastRequest req)
    {
        if (ForbidIfNotSuperuser() is { } f) return f;

        if (string.IsNullOrWhiteSpace(req.Subject) || string.IsNullOrWhiteSpace(req.Body))
            return BadRequest(new { success = false, message = "subject and body are required" });

        var owners = await _db.Users
            .Find(u => u.Role == "owner" && u.Status == "approved")
            .ToListAsync();

        var msgs = owners.Select(o => new Message
        {
            FromId      = UserId,
            FromRole    = "superuser",
            ToId        = o.Id,
            ToRole      = "owner",
            Subject     = req.Subject,
            Body        = req.Body,
            IsBroadcast = true
        }).ToList();

        if (msgs.Count > 0)
            await _db.Messages.InsertManyAsync(msgs);

        // Create bell notifications for each recipient
        var notifications = owners.Select(o => new Notification
        {
            UserId  = o.Id!,
            Type    = "message",
            Title   = "New Broadcast from Admin",
            Message = req.Subject
        }).ToList();
        if (notifications.Count > 0)
            await _db.Notifications.InsertManyAsync(notifications);

        await LogActivity("message.broadcast", null, null);
        return Ok(new { success = true, message = $"Broadcast sent to {owners.Count} owner(s)." });
    }

    // POST /api/superuser/messages/send
    [HttpPost("messages/send")]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest req)
    {
        if (ForbidIfNotSuperuser() is { } f) return f;

        if (string.IsNullOrWhiteSpace(req.ToId) || string.IsNullOrWhiteSpace(req.Subject) || string.IsNullOrWhiteSpace(req.Body))
            return BadRequest(new { success = false, message = "toId, subject, and body are required" });

        var recipient = await _db.Users.Find(u => u.Id == req.ToId).FirstOrDefaultAsync();
        if (recipient == null) return NotFound(new { success = false, message = "Recipient not found" });

        var msg = new Message
        {
            FromId   = UserId,
            FromRole = "superuser",
            ToId     = req.ToId,
            ToRole   = recipient.Role,
            Subject  = req.Subject,
            Body     = req.Body
        };
        await _db.Messages.InsertOneAsync(msg);

        // Create bell notification for the recipient
        await _db.Notifications.InsertOneAsync(new Notification
        {
            UserId  = req.ToId!,
            Type    = "message",
            Title   = "New Message from Admin",
            Message = req.Subject
        });

        await LogActivity("message.sent", req.ToId, "user");
        return StatusCode(201, new { success = true, message = "Message sent.", data = msg });
    }

    // GET /api/superuser/messages/sent
    [HttpGet("messages/sent")]
    public async Task<IActionResult> GetSentMessages()
    {
        if (ForbidIfNotSuperuser() is { } f) return f;

        var messages = await _db.Messages
            .Find(m => m.FromId == UserId)
            .SortByDescending(m => m.SentAt)
            .Limit(100)
            .ToListAsync();

        return Ok(new { success = true, data = messages });
    }

    // GET /api/superuser/support-inbox
    [HttpGet("support-inbox")]
    public async Task<IActionResult> GetSupportInbox()
    {
        if (ForbidIfNotSuperuser() is { } f) return f;

        var filter = Builders<Message>.Filter.Eq(m => m.ToRole, "superuser");

        var messages = await _db.Messages.Find(filter)
            .SortByDescending(m => m.SentAt)
            .Limit(200)
            .ToListAsync();

        return Ok(new { success = true, data = messages });
    }

    // ── ROLES ──────────────────────────────────────────────────────────────

    // GET /api/superuser/roles
    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles()
    {
        if (ForbidIfNotSuperuser() is { } f) return f;
        var roles = await _db.SuperuserRoles.Find(_ => true).SortBy(r => r.Name).ToListAsync();
        return Ok(new { success = true, data = roles });
    }

    // POST /api/superuser/roles
    [HttpPost("roles")]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest req)
    {
        if (ForbidIfNotSuperuser() is { } f) return f;
        if (string.IsNullOrWhiteSpace(req.RoleName))
            return BadRequest(new { success = false, message = "roleName is required" });

        var role = new SuperuserRole { Name = req.RoleName, Permissions = req.Permissions ?? new() };
        await _db.SuperuserRoles.InsertOneAsync(role);
        return StatusCode(201, new { success = true, data = role });
    }

    // PUT /api/superuser/roles/:id
    [HttpPut("roles/{id}")]
    public async Task<IActionResult> UpdateRole(string id, [FromBody] CreateRoleRequest req)
    {
        if (ForbidIfNotSuperuser() is { } f) return f;

        var updates = new List<UpdateDefinition<SuperuserRole>>();
        if (!string.IsNullOrWhiteSpace(req.RoleName)) updates.Add(Builders<SuperuserRole>.Update.Set(r => r.Name, req.RoleName));
        if (req.Permissions != null)                  updates.Add(Builders<SuperuserRole>.Update.Set(r => r.Permissions, req.Permissions));

        if (updates.Count == 0)
            return BadRequest(new { success = false, message = "No fields to update" });

        var role = await _db.SuperuserRoles.FindOneAndUpdateAsync(
            r => r.Id == id,
            Builders<SuperuserRole>.Update.Combine(updates),
            new FindOneAndUpdateOptions<SuperuserRole> { ReturnDocument = ReturnDocument.After });

        if (role == null) return NotFound(new { success = false, message = "Role not found" });
        return Ok(new { success = true, data = role });
    }

    // DELETE /api/superuser/roles/:id
    [HttpDelete("roles/{id}")]
    public async Task<IActionResult> DeleteRole(string id)
    {
        if (ForbidIfNotSuperuser() is { } f) return f;

        var role = await _db.SuperuserRoles.FindOneAndDeleteAsync(r => r.Id == id);
        if (role == null) return NotFound(new { success = false, message = "Role not found" });

        await _db.Users.UpdateManyAsync(
            u => u.SuperuserRoleId == id,
            Builders<User>.Update.Set(u => u.SuperuserRoleId, (string?)null));

        return Ok(new { success = true, message = "Role deleted" });
    }

    // ── MY PERMISSIONS ─────────────────────────────────────────────────────

    // GET /api/superuser/my-permissions
    [HttpGet("my-permissions")]
    public async Task<IActionResult> GetMyPermissions()
    {
        if (ForbidIfNotSuperuser() is { } f) return f;
        var user = await _db.Users.Find(u => u.Id == UserId).FirstOrDefaultAsync();
        string[]? permissions = null;
        if (!string.IsNullOrWhiteSpace(user?.SuperuserRoleId))
        {
            var role = await _db.SuperuserRoles.Find(r => r.Id == user.SuperuserRoleId).FirstOrDefaultAsync();
            permissions = role?.Permissions?.ToArray();
        }
        return Ok(new { success = true, data = new { permissions } });
    }

    // ── HELPERS ────────────────────────────────────────────────────────────

    private async Task LogActivity(string action, string? targetId, string? targetType, object? metadata = null)
    {
        string? storeId = null;
        if (targetType == "store") storeId = targetId;

        await _db.ActivityLogs.InsertOneAsync(new ActivityLog
        {
            ActorId    = UserId,
            ActorRole  = UserRole,
            Action     = action,
            TargetId   = targetId,
            TargetType = targetType ?? string.Empty,
            StoreId    = storeId,
            Metadata   = metadata
        });
    }
}

// ── DTOs ───────────────────────────────────────────────────────────────────

public class RejectRequest        { public string? Reason { get; set; } }
public class UpdateStatusRequest  { public string Status { get; set; } = string.Empty; }
public class ExtendTrialRequest   { public int Days { get; set; } }
public class OverridePlanRequest  { public string Plan { get; set; } = string.Empty; public string? Reason { get; set; } }
public class AssignRoleRequest    { public string? SuperuserRoleId { get; set; } }
public class BroadcastRequest     { public string Subject { get; set; } = string.Empty; public string Body { get; set; } = string.Empty; }
public class SendMessageRequest   { public string? ToId { get; set; } public string Subject { get; set; } = string.Empty; public string Body { get; set; } = string.Empty; }
public class PatchFeaturesRequest { public Dictionary<string, bool>? Features { get; set; } }
public class CreateCouponRequest
{
    public string Code { get; set; } = string.Empty;
    public int DiscountPercent { get; set; }
    public int MaxUses { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public List<string>? ApplicablePlans { get; set; }
}
public class CreateRoleRequest
{
    public string RoleName { get; set; } = string.Empty;
    public List<string>? Permissions { get; set; }
}
