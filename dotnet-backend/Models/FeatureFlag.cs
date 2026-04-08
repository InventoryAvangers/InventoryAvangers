using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace InventoryAvengers.API.Models;

public class FeatureSet
{
    [BsonElement("inventory")]
    public bool Inventory { get; set; } = true;

    [BsonElement("pos")]
    public bool Pos { get; set; } = true;

    [BsonElement("returns")]
    public bool Returns { get; set; } = false;

    [BsonElement("reports")]
    public bool Reports { get; set; } = false;

    [BsonElement("pdfExport")]
    public bool PdfExport { get; set; } = false;

    [BsonElement("employees")]
    public bool Employees { get; set; } = false;

    [BsonElement("payments")]
    public bool Payments { get; set; } = false;

    [BsonElement("apiAccess")]
    public bool ApiAccess { get; set; } = false;

    [BsonElement("darkMode")]
    public bool DarkMode { get; set; } = true;
}

public class FeatureFlag
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    [BsonElement("storeId")]
    public string StoreId { get; set; } = string.Empty;

    [BsonElement("features")]
    public FeatureSet Features { get; set; } = new();

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public static FeatureSet GetDefaults(string plan) => plan switch
    {
        "pro"   => new FeatureSet { Inventory = true, Pos = true, Returns = true, Reports = true, PdfExport = true, Employees = true, Payments = true, ApiAccess = true,  DarkMode = true },
        _       => new FeatureSet { Inventory = true, Pos = true, Returns = false, Reports = false, PdfExport = false, Employees = true,  Payments = false, ApiAccess = false, DarkMode = true }
    };
}

