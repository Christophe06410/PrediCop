using PrediCop.Core.Enums;

namespace PrediCop.Core.DTOs;

public class UpdateVehicleStatusRequest
{
    public VehicleStatus Status { get; set; }
}

public class CreateVehicleRequest
{
    public string CallSign { get; set; } = string.Empty;
    public string LicensePlate { get; set; } = string.Empty;
    public string Status { get; set; } = "Offline";
    public string? BeaconUuid { get; set; }
}

public class UpdateVehicleRequest
{
    public string? CallSign { get; set; }
    public string? LicensePlate { get; set; }
    public string? Status { get; set; }
    public string? BeaconUuid { get; set; }
}

public class VehiclePositionUpdate
{
    public Guid VehicleId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class VehicleResponse
{
    public Guid Id { get; set; }
    public string CallSign { get; set; } = string.Empty;
    public string LicensePlate { get; set; } = string.Empty;
    public VehicleStatus Status { get; set; }
    public double? LastLatitude { get; set; }
    public double? LastLongitude { get; set; }
    public DateTime? LastPositionUpdate { get; set; }
    public List<string> OfficerNames { get; set; } = [];
    public string? BeaconUuid { get; set; }
    public Guid? AssignedGeoZoneId { get; set; }
}

public class NearbyVehicleResponse
{
    public Guid VehicleId { get; set; }
    public string CallSign { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double DistanceKm { get; set; }
}

public class AssignGeoZoneRequest
{
    /// <summary>Id de la zone à assigner. Null pour désaffecter.</summary>
    public Guid? GeoZoneId { get; set; }
}

public class VehicleSosResponse
{
    public Guid VehicleId { get; set; }
    public string CallSign { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTime TriggeredAt { get; set; }
}

public class CrewSheetEntryResponse
{
    public Guid VehicleId { get; set; }
    public string CallSign { get; set; } = string.Empty;
    public string LicensePlate { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double? LastLatitude { get; set; }
    public double? LastLongitude { get; set; }
    public List<CrewMemberInfo> Officers { get; set; } = [];
    public ActiveMissionInfo? CurrentMission { get; set; }
}

public class CrewMemberInfo
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string BadgeNumber { get; set; } = string.Empty;
}

public class ActiveMissionInfo
{
    public Guid MissionId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string TargetAddress { get; set; } = string.Empty;
    public DateTime? AcceptedAt { get; set; }
}
