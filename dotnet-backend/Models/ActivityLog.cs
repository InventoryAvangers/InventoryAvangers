using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace InventoryAvengers.API.Models;

public class ActivityLog
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    [BsonElement("actorId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? ActorId { get; set; }

    [BsonElement("actorRole")]
    public string? ActorRole { get; set; }

    [BsonElement("action")]
    public string Action { get; set; } = string.Empty;

    [BsonElement("storeId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? StoreId { get; set; }

    [BsonElement("targetType")]
    public string TargetType { get; set; } = string.Empty;

    [BsonElement("targetId")]
    public string? TargetId { get; set; }

    [BsonElement("metadata")]
    public object? Metadata { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

