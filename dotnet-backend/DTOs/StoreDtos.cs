namespace InventoryAvengers.API.DTOs;

public class CreateStoreRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
}

public class UpdateStoreRequest
{
    public string? Name { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Status { get; set; }
}

public class UpdateBrandingRequest
{
    public string? ShopName { get; set; }
    public string? LogoUrl { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? ReceiptFooter { get; set; }
}

public class AssignManagerRequest
{
    public string? ManagerId { get; set; }
}
