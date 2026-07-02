using Microsoft.EntityFrameworkCore;
using PrediCop.Core.Enums;
using PrediCop.Core.Interfaces;
using PrediCop.Infrastructure.Data;

namespace PrediCop.Infrastructure.Services;

public class GpsService(AppDbContext context) : IGpsService
{
    private const double EarthRadiusKm = 6371.0;

    public double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2))
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusKm * c;
    }

    public async Task<IEnumerable<(Guid VehicleId, double Lat, double Lng, double Distance)>> FindNearbyAvailableVehiclesAsync(
        double latitude, double longitude, Guid tenantId, int maxResults = 5, CancellationToken ct = default)
    {
        var vehicles = await context.PatrolVehicles
            .Where(v => v.TenantId == tenantId && v.Status == VehicleStatus.Available)
            .Select(v => new
            {
                v.Id,
                Lat = v.LastLatitude,
                Lng = v.LastLongitude
            })
            .ToListAsync(ct);

        // Vehicles without GPS position get a sentinel distance so they are dispatched last
        return vehicles
            .Select(v => (
                VehicleId: v.Id,
                Lat: v.Lat ?? 0.0,
                Lng: v.Lng ?? 0.0,
                Distance: v.Lat.HasValue && v.Lng.HasValue
                    ? CalculateDistance(latitude, longitude, v.Lat.Value, v.Lng.Value)
                    : 9999.0))
            .OrderBy(v => v.Distance)
            .Take(maxResults)
            .ToList();
    }

    public async Task UpdateVehiclePositionAsync(Guid vehicleId, double latitude, double longitude, CancellationToken ct = default)
    {
        var vehicle = await context.PatrolVehicles.FindAsync([vehicleId], ct)
            ?? throw new InvalidOperationException($"Vehicle {vehicleId} not found.");

        vehicle.LastLatitude = latitude;
        vehicle.LastLongitude = longitude;
        vehicle.LastPositionUpdate = DateTime.UtcNow;

        await context.SaveChangesAsync(ct);
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
