namespace InventoryAvengers.API.DTOs;

public class SaleItemRequest
{
    public string? ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public int Qty { get; set; }
    public decimal Price { get; set; }
}

public class CreateSaleRequest
{
    public List<SaleItemRequest> Items { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
}
