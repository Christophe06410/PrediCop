using PrediCop.Core.Enums;

namespace PrediCop.Core.Entities;

public class LeaveEntitlement : TenantEntity
{
    public Guid AgentId { get; set; }
    public User Agent { get; set; } = null!;

    public LeaveType Type { get; set; }      // pas Maladie (illimité)
    public decimal TotalDays { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly ValidTo { get; set; }
}
