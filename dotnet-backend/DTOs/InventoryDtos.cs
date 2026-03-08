namespace InventoryAvengers.API.DTOs;

public class AdjustInventoryRequest
{
    public string ProductId { get; set; } = string.Empty;
    public int? Quantity { get; set; }
    public int? Threshold { get; set; }
}
