namespace PrediCop.Core.Entities;

public class PatrolRecord : TenantEntity
{
    public Guid StreetId { get; set; }
    public Street Street { get; set; } = null!;

    public Guid VehicleId { get; set; }
    public PatrolVehicle Vehicle { get; set; } = null!;

    public DateTime PatrolledAt { get; set; } = DateTime.UtcNow;
    public int RiskScoreAtPatrol { get; set; }
}
