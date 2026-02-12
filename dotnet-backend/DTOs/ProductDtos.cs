namespace InventoryAvengers.API.DTOs;

public class CreateProductRequest
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal CostPrice { get; set; }
    public decimal SellingPrice { get; set; }
    public int Quantity { get; set; } = 0;
    public int Threshold { get; set; } = 10;
    public string? Sku { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string BarcodeType { get; set; } = "CODE128";
}

public class UpdateProductRequest
{
    public string? Name { get; set; }
    public string? Category { get; set; }
    public decimal? CostPrice { get; set; }
    public decimal? SellingPrice { get; set; }
    public int? Quantity { get; set; }
    public int? Threshold { get; set; }
    public string? Sku { get; set; }
    public string? Barcode { get; set; }
    public string? BarcodeType { get; set; }
}
