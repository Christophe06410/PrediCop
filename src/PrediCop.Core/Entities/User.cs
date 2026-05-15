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

    public string FullName => $"{FirstName} {LastName}";

    public ICollection<VehicleOfficer> VehicleAssignments { get; set; } = [];
}
