using MongoDB.Driver;
using InventoryAvengers.API.Models;

namespace InventoryAvengers.API.Data;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IConfiguration configuration)
    {
        var connectionString = Environment.GetEnvironmentVariable("MONGO_URI")
            ?? configuration["MongoDb:ConnectionString"]
            ?? throw new InvalidOperationException("MongoDB connection string not configured");

        var databaseName = configuration["MongoDb:DatabaseName"] ?? "inventory-avengers";

        // MongoClient is thread-safe and manages its own connection pool.
        // A single instance should be reused for the lifetime of the application.
        var settings = MongoClientSettings.FromConnectionString(connectionString);
        settings.MaxConnectionPoolSize = 50;
        var client = new MongoClient(settings);
        _database = client.GetDatabase(databaseName);
    }

    public IMongoCollection<User> Users => _database.GetCollection<User>("users");
    public IMongoCollection<Product> Products => _database.GetCollection<Product>("products");
    public IMongoCollection<Sale> Sales => _database.GetCollection<Sale>("sales");
    public IMongoCollection<Return> Returns => _database.GetCollection<Return>("returns");
    public IMongoCollection<Store> Stores => _database.GetCollection<Store>("stores");
    public IMongoCollection<Inventory> Inventories => _database.GetCollection<Inventory>("inventories");
    public IMongoCollection<AuditLog> AuditLogs => _database.GetCollection<AuditLog>("auditlogs");
    public IMongoCollection<Notification> Notifications => _database.GetCollection<Notification>("notifications");
    public IMongoCollection<Approval> Approvals => _database.GetCollection<Approval>("approvals");
    public IMongoCollection<AccessRequest> AccessRequests => _database.GetCollection<AccessRequest>("accessrequests");
    public IMongoCollection<Subscription> Subscriptions => _database.GetCollection<Subscription>("subscriptions");
    public IMongoCollection<FeatureFlag> FeatureFlags => _database.GetCollection<FeatureFlag>("featureflags");
    public IMongoCollection<Message> Messages => _database.GetCollection<Message>("messages");
    public IMongoCollection<SuperuserRole> SuperuserRoles => _database.GetCollection<SuperuserRole>("superuserroles");
    public IMongoCollection<ActivityLog> ActivityLogs => _database.GetCollection<ActivityLog>("activitylogs");
    public IMongoCollection<Coupon> Coupons => _database.GetCollection<Coupon>("coupons");
    public IMongoCollection<VacationRequest> VacationRequests => _database.GetCollection<VacationRequest>("vacationrequests");

    /// <summary>
    /// Creates all performance-critical indexes if they don't already exist.
    /// Safe to call on every startup (MongoDB ignores duplicate index creation).
    /// </summary>
    public async Task EnsureIndexesAsync()
    {
        // Sales: most queries filter by storeId + sort by createdAt
        await Sales.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<Sale>(
                Builders<Sale>.IndexKeys.Ascending(s => s.StoreId).Descending(s => s.CreatedAt),
                new CreateIndexOptions { Name = "storeId_createdAt" }),
            new CreateIndexModel<Sale>(
                Builders<Sale>.IndexKeys.Ascending(s => s.StoreId).Ascending(s => s.PaymentMethod),
                new CreateIndexOptions { Name = "storeId_paymentMethod" }),
        });

        // Returns: filter by storeId + createdAt, and lookup by saleId
        await Returns.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<Return>(
                Builders<Return>.IndexKeys.Ascending(r => r.StoreId).Descending(r => r.CreatedAt),
                new CreateIndexOptions { Name = "storeId_createdAt" }),
            new CreateIndexModel<Return>(
                Builders<Return>.IndexKeys.Ascending(r => r.SaleId),
                new CreateIndexOptions { Name = "saleId" }),
        });

        // Inventories: primary lookup is storeId; productId+storeId uniqueness
        await Inventories.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<Inventory>(
                Builders<Inventory>.IndexKeys.Ascending(i => i.StoreId),
                new CreateIndexOptions { Name = "storeId" }),
            new CreateIndexModel<Inventory>(
                Builders<Inventory>.IndexKeys.Ascending(i => i.ProductId).Ascending(i => i.StoreId),
                new CreateIndexOptions { Name = "productId_storeId", Unique = true }),
        });

        // Products: lookup by storeId (used in inventory joins)
        await Products.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<Product>(
                Builders<Product>.IndexKeys.Ascending(p => p.StoreId),
                new CreateIndexOptions { Name = "storeId" }),
        });

        // Users: role/status filter + storeId filter (employee management, dashboard stats)
        await Users.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<User>(
                Builders<User>.IndexKeys.Ascending(u => u.StoreId).Ascending(u => u.Role).Ascending(u => u.Status),
                new CreateIndexOptions { Name = "storeId_role_status" }),
        });

        // Approvals: most queries filter by status
        await Approvals.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<Approval>(
                Builders<Approval>.IndexKeys.Ascending(a => a.Status),
                new CreateIndexOptions { Name = "status" }),
        });

        // AuditLogs: filter by storeId + descending time
        await AuditLogs.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<AuditLog>(
                Builders<AuditLog>.IndexKeys.Ascending(a => a.StoreId).Descending(a => a.CreatedAt),
                new CreateIndexOptions { Name = "storeId_createdAt" }),
        });
    }
}
