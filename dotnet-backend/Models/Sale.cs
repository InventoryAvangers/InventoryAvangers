using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace InventoryAvengers.API.Models;

public class SaleItem
{
    [BsonElement("productId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? ProductId { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("sku")]
    public string? Sku { get; set; }

    [BsonElement("qty")]
    public int Qty { get; set; }

    [BsonElement("price")]
    public decimal Price { get; set; }
}

public class Sale
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    [BsonElement("items")]
    public List<SaleItem> Items { get; set; } = new();

    [BsonElement("totalAmount")]
    public decimal TotalAmount { get; set; }

    [BsonElement("subtotal")]
    public decimal Subtotal { get; set; }

    [BsonElement("tax")]
    public decimal Tax { get; set; }

    [BsonElement("paymentMethod")]
    public string PaymentMethod { get; set; } = string.Empty;

    [BsonElement("employeeId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? EmployeeId { get; set; }

    [BsonElement("customerName")]
    public string CustomerName { get; set; } = "Walk-in";

    [BsonElement("storeId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? StoreId { get; set; }

    [BsonElement("receiptNumber")]
    public string? ReceiptNumber { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

