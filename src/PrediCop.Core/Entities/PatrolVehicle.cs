using PrediCop.Core.Enums;

namespace PrediCop.Core.Entities;

public class PatrolVehicle : TenantEntity
{
    public string CallSign { get; set; } = string.Empty;
    public string LicensePlate { get; set; } = string.Empty;
    public VehicleStatus Status { get; set; } = VehicleStatus.Offline;

    /// <summary>Nombre d'agents pouvant être assignés à ce véhicule simultanément.</summary>
    public int Capacity { get; set; } = 2;
    public double? LastLatitude { get; set; }
    public double? LastLongitude { get; set; }
    public DateTime? LastPositionUpdate { get; set; }

    /// <summary>UUID du beacon BLE installé dans le véhicule (optionnel).</summary>
    public string? BeaconUuid { get; set; }

    /// <summary>Zone de patrouille assignée pour le géofencing (optionnel).</summary>
    public Guid? AssignedGeoZoneId { get; set; }
    public GeoZone? AssignedGeoZone { get; set; }

    // ---- Session de patrouille (renseignés à l'activation, remis à null à la désactivation) ----

    /// <summary>Indicatif radio pour la session en cours (ex : "Sierra 1", "Victor 2").</summary>
    public string? Indicatif { get; set; }

    /// <summary>Type de patrouille pour la session en cours.</summary>
    public PatrolType? PatrolType { get; set; }

    /// <summary>Heure de prise de service.</summary>
    public DateTime? SessionStartedAt { get; set; }

    public ICollection<VehicleOfficer> Officers { get; set; } = [];
    public ICollection<MissionAssignment> Missions { get; set; } = [];
    public ICollection<PatrolRecord> PatrolRecords { get; set; } = [];
}
