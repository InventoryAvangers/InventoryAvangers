namespace InventoryAvengers.API.DTOs;

public class TransferEmployeeRequest
{
    public string StoreId { get; set; } = string.Empty;
}

public class ApproveUserRequest
{
    public string? Role { get; set; }
    public string? StoreId { get; set; }
}
