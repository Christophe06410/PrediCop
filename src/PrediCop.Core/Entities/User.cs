using PrediCop.Core.Enums;

namespace PrediCop.Core.Entities;

public class User : TenantEntity
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string BadgeNumber { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public string? DeviceToken { get; set; }

    // 2FA TOTP
    public bool TotpEnabled { get; set; }
    public string? TotpSecretKey { get; set; }
    public string? TotpRecoveryCodes { get; set; } // JSON array de codes usage unique

    public string FullName => $"{FirstName} {LastName}";

    // Géolocalisation individuelle (pour agents et chefs de patrouille)
    public double? LastLatitude { get; set; }
    public double? LastLongitude { get; set; }
    public DateTime? LastPositionUpdate { get; set; }

    public ICollection<VehicleOfficer> VehicleAssignments { get; set; } = [];
}
