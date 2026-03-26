using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace InventoryAvengers.API.Models;

public class Coupon
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    [BsonElement("code")]
    public string Code { get; set; } = string.Empty;

    [BsonElement("discountPercent")]
    public int DiscountPercent { get; set; }

    [BsonElement("maxUses")]
    public int MaxUses { get; set; }

    [BsonElement("usedCount")]
    public int UsedCount { get; set; } = 0;

    [BsonElement("expiresAt")]
    public DateTime? ExpiresAt { get; set; }

    [BsonElement("isActive")]
    public bool IsActive { get; set; } = true;

    [BsonElement("applicablePlans")]
    public List<string> ApplicablePlans { get; set; } = new();

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

