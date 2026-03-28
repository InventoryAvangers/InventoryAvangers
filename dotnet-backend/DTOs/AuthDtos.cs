namespace InventoryAvengers.API.DTOs;

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RegisterRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? StoreId { get; set; }
    public string? Role { get; set; }
}

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class AccessRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? BusinessName { get; set; }
    public string? Message { get; set; }
    public string Password { get; set; } = string.Empty;
    public string? ConfirmPassword { get; set; }
    public string? StoreId { get; set; }
}

public class UserDto
{
    public string? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? StoreId { get; set; }
    public bool MustChangePassword { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Avatar { get; set; } = string.Empty;
    public string Currency { get; set; } = "INR";
}
