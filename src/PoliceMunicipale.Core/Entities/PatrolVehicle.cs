using PoliceMunicipale.Core.Enums;

namespace PoliceMunicipale.Core.Entities;

public class PatrolVehicle : TenantEntity
{
    public string CallSign { get; set; } = string.Empty;
    public string LicensePlate { get; set; } = string.Empty;
    public VehicleStatus Status { get; set; } = VehicleStatus.Offline;
    public double? LastLatitude { get; set; }
    public double? LastLongitude { get; set; }
    public DateTime? LastPositionUpdate { get; set; }

    public ICollection<VehicleOfficer> Officers { get; set; } = [];
    public ICollection<MissionAssignment> Missions { get; set; } = [];
    public ICollection<PatrolRecord> PatrolRecords { get; set; } = [];
}
