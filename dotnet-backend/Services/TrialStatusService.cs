using MongoDB.Driver;
using InventoryAvengers.API.Data;
using InventoryAvengers.API.Models;

namespace InventoryAvengers.API.Services;

public class TrialStatusService
{
    private readonly MongoDbContext _db;

    public TrialStatusService(MongoDbContext db)
    {
        _db = db;
    }

    public async Task SyncExpiredTrialsAsync(string? storeId = null)
    {
        var now = DateTime.UtcNow;

        var filter = Builders<Store>.Filter.Eq(s => s.Status, "trial") &
                     Builders<Store>.Filter.Lte(s => s.TrialExpiresAt, now);

        if (!string.IsNullOrWhiteSpace(storeId))
            filter &= Builders<Store>.Filter.Eq(s => s.Id, storeId);

        var expiredStoreIds = await _db.Stores.Find(filter)
            .Project(s => s.Id)
            .ToListAsync();

        if (expiredStoreIds.Count == 0) return;

        await _db.Stores.UpdateManyAsync(
            Builders<Store>.Filter.In(s => s.Id, expiredStoreIds),
            Builders<Store>.Update
                .Set(s => s.Status, "expired")
                .Set(s => s.IsActive, false));

        await _db.Subscriptions.UpdateManyAsync(
            Builders<Subscription>.Filter.In(s => s.StoreId, expiredStoreIds),
            Builders<Subscription>.Update
                .Set(s => s.Status, "expired")
                .Set(s => s.UpdatedAt, now));
    }

    public static int? GetTrialDaysLeft(Store store)
    {
        if (!string.Equals(store.Status, "trial", StringComparison.OrdinalIgnoreCase))
            return null;

        var remaining = store.TrialExpiresAt - DateTime.UtcNow;
        return Math.Max(0, (int)Math.Ceiling(remaining.TotalDays));
    }
}
