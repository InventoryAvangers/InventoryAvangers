using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace InventoryAvengers.API.Models;

public class Message
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    [BsonElement("fromId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? FromId { get; set; }

    [BsonElement("fromRole")]
    public string? FromRole { get; set; }

    [BsonElement("toId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? ToId { get; set; }

    [BsonElement("toRole")]
    public string? ToRole { get; set; }

    [BsonElement("storeId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? StoreId { get; set; }

    [BsonElement("subject")]
    public string Subject { get; set; } = string.Empty;

    [BsonElement("body")]
    public string Body { get; set; } = string.Empty;

    [BsonElement("read")]
    public bool Read { get; set; } = false;

    [BsonElement("readAt")]
    public DateTime? ReadAt { get; set; }

    [BsonElement("isBroadcast")]
    public bool IsBroadcast { get; set; } = false;

    [BsonElement("parentMessageId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? ParentMessageId { get; set; }

    [BsonElement("sentAt")]
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
