namespace PrediCop.Core.Entities;

public class GeoZone : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = "#3b82f6";
    public bool IsActive { get; set; } = true;
    public List<GeoZoneVertex> Vertices { get; set; } = [];
    public ICollection<PatrolVehicle> AssignedVehicles { get; set; } = [];
}
