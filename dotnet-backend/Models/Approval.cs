using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace InventoryAvengers.API.Models;

public class Approval
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    [BsonElement("action")]
    public string Action { get; set; } = string.Empty;

    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;

    [BsonElement("requestedBy")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? RequestedBy { get; set; }

    [BsonElement("approvedBy")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? ApprovedBy { get; set; }

    [BsonElement("storeId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? StoreId { get; set; }

    [BsonElement("status")]
    public string Status { get; set; } = "pending";

    [BsonElement("metadata")]
    public object? Metadata { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

