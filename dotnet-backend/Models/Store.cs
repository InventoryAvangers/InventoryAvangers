using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace InventoryAvengers.API.Models;

public class UsageStats
{
    [BsonElement("totalOrders")]
    public int TotalOrders { get; set; } = 0;

    [BsonElement("totalProducts")]
    public int TotalProducts { get; set; } = 0;

    [BsonElement("revenue")]
    public decimal Revenue { get; set; } = 0;

    [BsonElement("apiCalls")]
    public int ApiCalls { get; set; } = 0;
}

public class Store
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("address")]
    public string Address { get; set; } = string.Empty;

    [BsonElement("code")]
    public string Code { get; set; } = string.Empty;

    [BsonElement("phone")]
    public string Phone { get; set; } = string.Empty;

    [BsonElement("email")]
    public string Email { get; set; } = string.Empty;

    [BsonElement("managerId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? ManagerId { get; set; }

    [BsonElement("ownerId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? OwnerId { get; set; }

    [BsonElement("status")]
    public string Status { get; set; } = "trial";

    [BsonElement("isActive")]
    public bool IsActive { get; set; } = true;

    [BsonElement("plan")]
    public string Plan { get; set; } = "free";

    [BsonElement("trialExpiresAt")]
    public DateTime TrialExpiresAt { get; set; } = DateTime.UtcNow.AddDays(14);

    [BsonElement("subscriptionExpiresAt")]
    public DateTime? SubscriptionExpiresAt { get; set; }

    [BsonElement("usageStats")]
    public UsageStats UsageStats { get; set; } = new();

    [BsonElement("shopName")]
    public string ShopName { get; set; } = string.Empty;

    [BsonElement("logoUrl")]
    public string LogoUrl { get; set; } = string.Empty;

    [BsonElement("receiptFooter")]
    public string ReceiptFooter { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

