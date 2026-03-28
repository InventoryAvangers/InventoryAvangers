using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace InventoryAvengers.API.Models;

public class Subscription
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    [BsonElement("storeId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string StoreId { get; set; } = string.Empty;

    [BsonElement("plan")]
    public string Plan { get; set; } = "free";

    [BsonElement("status")]
    public string Status { get; set; } = "trial";

    [BsonElement("trialExpiresAt")]
    public DateTime? TrialExpiresAt { get; set; }

    [BsonElement("subscriptionExpiresAt")]
    public DateTime? SubscriptionExpiresAt { get; set; }

    [BsonElement("paymentProvider")]
    public string? PaymentProvider { get; set; }

    [BsonElement("paymentId")]
    public string PaymentId { get; set; } = string.Empty;

    [BsonElement("couponUsed")]
    public string CouponUsed { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

