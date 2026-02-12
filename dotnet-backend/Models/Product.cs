using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace InventoryAvengers.API.Models;

public class Product
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("category")]
    public string Category { get; set; } = string.Empty;

    [BsonElement("costPrice")]
    public decimal CostPrice { get; set; }

    [BsonElement("sellingPrice")]
    public decimal SellingPrice { get; set; }

    [BsonElement("quantity")]
    public int Quantity { get; set; } = 0;

    [BsonElement("threshold")]
    public int Threshold { get; set; } = 10;

    [BsonElement("sku")]
    public string? Sku { get; set; }

    [BsonElement("barcode")]
    public string Barcode { get; set; } = string.Empty;

    [BsonElement("barcodeType")]
    public string BarcodeType { get; set; } = "CODE128";

    [BsonElement("storeId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? StoreId { get; set; }

    [BsonElement("createdBy")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? CreatedBy { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonIgnore]
    public decimal Profit => SellingPrice - CostPrice;
}

