using PrediCop.Core.Enums;

namespace PrediCop.Core.DTOs;

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string TenantSlug { get; set; } = string.Empty;
}

public class TenantSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime ExpiresAt { get; set; }
    public UserResponse? User { get; set; }
    public bool RequiresTwoFactor { get; set; }
    public string? TempToken { get; set; }
}

public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public class UserResponse
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string BadgeNumber { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public string TenantSlug { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public Guid? VehicleId { get; set; }
}

public class SelectVehicleResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public Guid VehicleId { get; set; }
    public string VehicleCallSign { get; set; } = string.Empty;
}
