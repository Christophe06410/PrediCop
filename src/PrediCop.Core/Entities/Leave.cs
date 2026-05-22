using PrediCop.Core.Enums;

namespace PrediCop.Core.Entities;

public class Leave : TenantEntity
{
    public Guid AgentId { get; set; }
    public User Agent { get; set; } = null!;

    public LeaveType Type { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public string? Notes { get; set; }
    public string? RejectionReason { get; set; }
}
