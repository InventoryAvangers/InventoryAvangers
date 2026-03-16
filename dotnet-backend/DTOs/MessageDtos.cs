namespace InventoryAvengers.API.DTOs;

public class CreateMessageRequest
{
    public string? ToId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? ParentMessageId { get; set; }
}
