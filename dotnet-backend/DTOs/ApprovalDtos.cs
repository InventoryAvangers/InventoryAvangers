namespace InventoryAvengers.API.DTOs;

public class CreateApprovalRequest
{
    public string Action { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public object? Metadata { get; set; }
}

public class UpdateApprovalRequest
{
    public string Status { get; set; } = string.Empty;
}
