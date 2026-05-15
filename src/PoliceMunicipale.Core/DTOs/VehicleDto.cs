using PoliceMunicipale.Core.Enums;

namespace PoliceMunicipale.Core.DTOs;

public class UpdateVehicleStatusRequest
{
    public VehicleStatus Status { get; set; }
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
}

public class NearbyVehicleResponse
{
    public Guid VehicleId { get; set; }
    public string CallSign { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double DistanceKm { get; set; }
}
