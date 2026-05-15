namespace PoliceMunicipale.Core.Interfaces;

public interface IGpsService
{
    Task UpdateVehiclePositionAsync(Guid vehicleId, double latitude, double longitude, CancellationToken ct = default);
    Task<IEnumerable<(Guid VehicleId, double Lat, double Lng, double Distance)>> FindNearbyAvailableVehiclesAsync(
        double latitude, double longitude, int maxResults = 5, CancellationToken ct = default);
    double CalculateDistance(double lat1, double lon1, double lat2, double lon2);
}
