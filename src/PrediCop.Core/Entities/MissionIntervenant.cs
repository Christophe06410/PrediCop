namespace PrediCop.Core.Entities;

public class MissionIntervenant : TenantEntity
{
    public Guid MissionId { get; set; }
    public Mission Mission { get; set; } = null!;

    public string FullName { get; set; } = string.Empty;
    public string? Role { get; set; }
    public string? PhoneNumber { get; set; }
    public bool IsInjured { get; set; }
    public string? Notes { get; set; }
    public int Order { get; set; }
}
