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
        var client = new MongoClient(connectionString);
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
}
