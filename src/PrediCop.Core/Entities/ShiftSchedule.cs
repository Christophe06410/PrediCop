namespace PrediCop.Core.Entities;

public class ShiftSchedule : TenantEntity
{
    public Guid AgentId { get; set; }
    public User Agent { get; set; } = null!;

    public Guid? VehicleId { get; set; }
    public PatrolVehicle? Vehicle { get; set; }

    public DateOnly Date { get; set; }
    public TimeOnly ShiftStart { get; set; }
    public TimeOnly ShiftEnd { get; set; }

    public bool IsPublished { get; set; } = false;
    public string? Notes { get; set; }
}
