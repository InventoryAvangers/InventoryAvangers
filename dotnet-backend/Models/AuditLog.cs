using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace InventoryAvengers.API.Models;

public class AuditLog
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    [BsonElement("actorId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string ActorId { get; set; } = string.Empty;

    [BsonElement("targetId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? TargetId { get; set; }

    [BsonElement("action")]
    public string Action { get; set; } = string.Empty;

    [BsonElement("metadata")]
    public object? Metadata { get; set; }

    [BsonElement("storeId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? StoreId { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

