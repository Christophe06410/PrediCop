namespace PrediCop.Core.Entities;

public class AgentProfile : TenantEntity
{
    public Guid AgentId { get; set; }
    public User Agent { get; set; } = null!;

    public string? BloodType { get; set; }

    public string? EmergencyContact1Name { get; set; }
    public string? EmergencyContact1Phone { get; set; }
    public string? EmergencyContact1Relationship { get; set; }

    public string? EmergencyContact2Name { get; set; }
    public string? EmergencyContact2Phone { get; set; }

    public string? Notes { get; set; }
}
