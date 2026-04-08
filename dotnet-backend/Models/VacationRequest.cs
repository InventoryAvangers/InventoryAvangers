using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace InventoryAvengers.API.Models;

public class VacationRequest
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    [BsonElement("employeeId")]
    public string EmployeeId { get; set; } = string.Empty;

    [BsonElement("storeId")]
    public string StoreId { get; set; } = string.Empty;

    [BsonElement("startDate")]
    public DateTime StartDate { get; set; }

    [BsonElement("endDate")]
    public DateTime EndDate { get; set; }

    [BsonElement("reason")]
    public string Reason { get; set; } = string.Empty;

    /// <summary>pending | approved | declined</summary>
    [BsonElement("status")]
    public string Status { get; set; } = "pending";

    [BsonElement("reviewNote")]
    public string? ReviewNote { get; set; }

    [BsonElement("reviewedBy")]
    public string? ReviewedBy { get; set; }

    [BsonElement("reviewedAt")]
    public DateTime? ReviewedAt { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
