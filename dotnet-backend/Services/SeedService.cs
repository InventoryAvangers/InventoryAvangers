using MongoDB.Driver;
using InventoryAvengers.API.Data;
using InventoryAvengers.API.Models;

namespace InventoryAvengers.API.Services;

public static class SeedService
{
    private static readonly (string Name, string Email, string Role, string Password)[] DemoUsers =
    [
        ("Demo Superuser", "superuser@demo.com", "superuser", "password123")
    ];

    // Full feature access - used when provisioning existing stores
    private static readonly FeatureSet AllEnabled = new()
    {
        Inventory = true,
        Pos       = true,
        Returns   = true,
        Reports   = true,
        PdfExport = true,
        Employees = true,
        Payments  = true,
        ApiAccess = true,
        DarkMode  = true
    };

    public static async Task SeedAsync(MongoDbContext db)
    {
        // ─── 1. Provision FeatureFlags for ALL existing stores ──────────────────
        // Without this, FeatureCheck blocks every store-level endpoint with 403.
        var allStores = await db.Stores.Find(_ => true).ToListAsync();
        foreach (var store in allStores)
        {
            if (string.IsNullOrWhiteSpace(store.Id)) continue;
            var hasFlags = await db.FeatureFlags.Find(f => f.StoreId == store.Id).AnyAsync();
            if (!hasFlags)
            {
                await db.FeatureFlags.InsertOneAsync(new FeatureFlag
                {
                    StoreId  = store.Id,
                    Features = AllEnabled
                });
                Console.WriteLine($"[Seed] Provisioned feature flags for store: {store.Name ?? store.Id}");
            }
        }

        // ─── 2. Seed / fix demo users ───────────────────────────────────────────
        foreach (var (name, email, role, password) in DemoUsers)
        {
            var existing = await db.Users.Find(u => u.Email == email).FirstOrDefaultAsync();
            if (existing != null)
            {
                bool canLogin = BCrypt.Net.BCrypt.Verify(password, existing.PasswordHash);
                if (!canLogin)
                {
                    var fixedHash = BCrypt.Net.BCrypt.HashPassword(password, 10);
                    await db.Users.UpdateOneAsync(
                        u => u.Id == existing.Id,
                        Builders<User>.Update
                            .Set(u => u.PasswordHash, fixedHash)
                            .Set(u => u.Status, "approved"));
                    Console.WriteLine($"[Seed] Fixed password for {email}");
                }
                else if (existing.Status != "approved")
                {
                    await db.Users.UpdateOneAsync(
                        u => u.Id == existing.Id,
                        Builders<User>.Update.Set(u => u.Status, "approved"));
                    Console.WriteLine($"[Seed] Fixed status for {email}");
                }
                else
                {
                    Console.WriteLine($"[Seed] Demo user already valid: {email}");
                }
                continue;
            }

            var hash = BCrypt.Net.BCrypt.HashPassword(password, 10);
            await db.Users.InsertOneAsync(new User
            {
                Name               = name,
                Email              = email,
                PasswordHash       = hash,
                Role               = role,
                Status             = "approved",
                MustChangePassword = false
            });
            Console.WriteLine($"[Seed] Created demo user: {email}");
        }



        // ─── 4. Heal all legacy accounts ────────────────────────────────────────
        // Old Node.js accounts may have null/empty status or be stuck as "pending".
        // Approve any account that has a password hash and isn't explicitly blocked.
        var blockedStatuses = new[] { "pending", "rejected", "suspended", "deactivated" };
        var demoEmails = DemoUsers.Select(d => d.Email).ToHashSet();

        var legacyAccounts = await db.Users
            .Find(Builders<User>.Filter.And(
                Builders<User>.Filter.Nin(u => u.Status, blockedStatuses),
                Builders<User>.Filter.Ne(u => u.Status, "approved")
            ))
            .ToListAsync();

        foreach (var u in legacyAccounts)
        {
            if (demoEmails.Contains(u.Email)) continue;
            if (string.IsNullOrWhiteSpace(u.PasswordHash)) continue; // skip incomplete accounts
            await db.Users.UpdateOneAsync(
                x => x.Id == u.Id,
                Builders<User>.Update.Set(x => x.Status, "approved"));
            Console.WriteLine($"[Seed] Auto-approved legacy account: {u.Email}");
        }

        // Also approve real pending users that have a store assigned
        var pendingWithStore = await db.Users
            .Find(Builders<User>.Filter.And(
                Builders<User>.Filter.Ne(u => u.StoreId, null),
                Builders<User>.Filter.Ne(u => u.StoreId, ""),
                Builders<User>.Filter.Eq(u => u.Status, "pending")
            ))
            .ToListAsync();

        foreach (var u in pendingWithStore)
        {
            if (demoEmails.Contains(u.Email)) continue;
            await db.Users.UpdateOneAsync(
                x => x.Id == u.Id,
                Builders<User>.Update.Set(x => x.Status, "approved"));
            Console.WriteLine($"[Seed] Auto-approved pending user with store: {u.Email}");
        }
    }
}
