using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace InventoryAvengers.API.Models;

public class Return
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    [BsonElement("saleId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string SaleId { get; set; } = string.Empty;

    [BsonElement("productId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string ProductId { get; set; } = string.Empty;

    [BsonElement("storeId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? StoreId { get; set; }

    [BsonElement("quantity")]
    public int Quantity { get; set; }

    [BsonElement("reason")]
    public string Reason { get; set; } = string.Empty;

    [BsonElement("refundAmount")]
    public decimal RefundAmount { get; set; }

    [BsonElement("processedBy")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? ProcessedBy { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

