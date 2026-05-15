namespace PrediCop.Core.Entities;

public class VehicleOfficer : BaseEntity
{
    public Guid VehicleId { get; set; }
    public PatrolVehicle Vehicle { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UnassignedAt { get; set; }
    public bool IsActive { get; set; } = true;
}
