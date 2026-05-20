using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PrediCop.Core.Entities;
using PrediCop.Infrastructure.Data;

namespace PrediCop.Api.Services;

public class StreetRiskComputeService(
    AppDbContext db,
    IHttpClientFactory httpClientFactory,
    ILogger<StreetRiskComputeService> logger)
{
    private const double HalfLifeDays = 180.0;
    private const double InnerRadiusM = 220.0;
    private const double OuterRadiusM = 600.0;
    private static readonly TimeSpan DensityRefreshInterval = TimeSpan.FromDays(365);

    public async Task ComputeAllTenantsAsync(bool refreshDensity, CancellationToken ct)
    {
        var tenantIds = await db.Streets
            .Select(s => s.TenantId)
            .Distinct()
            .ToListAsync(ct);

        foreach (var tenantId in tenantIds)
            await ComputeForTenantAsync(tenantId, refreshDensity, ct);
    }

    public async Task ComputeForTenantAsync(Guid tenantId, bool refreshDensity, CancellationToken ct)
    {
        var streets = await db.Streets
            .Where(s => s.TenantId == tenantId)
            .ToListAsync(ct);

        if (streets.Count == 0) return;

        var cutoff = DateTime.UtcNow.AddYears(-2);
        var now = DateTime.UtcNow;

        var calls = await db.Calls
            .Include(c => c.Missions)
            .Where(c => c.TenantId == tenantId
                     && c.ReceivedAt >= cutoff
                     && c.IncidentLatitude != null
                     && c.IncidentLongitude != null)
            .ToListAsync(ct);

        foreach (var street in streets)
        {
            if (street.IsRiskLocked) continue;

            if (refreshDensity)
                await UpdateDensityIfNeededAsync(street, ct);

            var midLat = (street.StartLatitude + street.EndLatitude) / 2.0;
            var midLon = (street.StartLongitude + street.EndLongitude) / 2.0;

            if (midLat == 0 && midLon == 0) continue;

            const double latDelta = 0.0055;
            const double lonDelta = 0.0085;

            double rawScore = 0;
            foreach (var call in calls)
            {
                if (Math.Abs(call.IncidentLatitude!.Value - midLat) >= latDelta) continue;
                if (Math.Abs(call.IncidentLongitude!.Value - midLon) >= lonDelta) continue;

                var distM = HaversineM(midLat, midLon,
                    call.IncidentLatitude.Value, call.IncidentLongitude.Value);
                if (distM > OuterRadiusM) continue;

                var daysAgo = (now - call.ReceivedAt).TotalDays;
                var recency = Math.Pow(2.0, -daysAgo / HalfLifeDays);
                var distWeight = distM <= InnerRadiusM ? 1.0 : 0.5;
                var severity = GetSeverity(call);

                rawScore += recency * distWeight * severity;
            }

            var densityFactor = GetDensityFactor(street.BuildingDensityScore);
            var computed = (int)Math.Clamp(
                5 + Math.Log2(1 + rawScore * 10) * 10 * densityFactor,
                5, 85);

            street.ComputedBaseRiskScore = computed;
            street.BaseRiskScore = street.RiskAdjustment.HasValue
                ? Math.Clamp(computed + street.RiskAdjustment.Value, 0, 100)
                : computed;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Risk scores recomputed for tenant {TenantId}: {Count} streets, {Calls} calls analysed",
            tenantId, streets.Count(s => !s.IsRiskLocked), calls.Count);
    }

    private async Task UpdateDensityIfNeededAsync(Street street, CancellationToken ct)
    {
        if (street.StartLatitude == 0 && street.StartLongitude == 0) return;
        if (street.BuildingDensityFetchedAt.HasValue &&
            DateTime.UtcNow - street.BuildingDensityFetchedAt.Value < DensityRefreshInterval)
            return;

        var midLat = (street.StartLatitude + street.EndLatitude) / 2.0;
        var midLon = (street.StartLongitude + street.EndLongitude) / 2.0;

        try
        {
            var count = await FetchBuildingCountAsync(midLat, midLon, ct);
            street.BuildingDensityScore = count;
            street.BuildingDensityFetchedAt = DateTime.UtcNow;

            // Rate-limit: be polite to Overpass
            await Task.Delay(700, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Building density fetch failed for street {Id}", street.Id);
        }
    }

    private async Task<int> FetchBuildingCountAsync(double lat, double lon, CancellationToken ct)
    {
        var latStr = lat.ToString("F6", CultureInfo.InvariantCulture);
        var lonStr = lon.ToString("F6", CultureInfo.InvariantCulture);
        var radius = (int)InnerRadiusM;
        var query = $"[out:json][timeout:10];(node[\"building\"](around:{radius},{latStr},{lonStr});way[\"building\"](around:{radius},{latStr},{lonStr}););out count;";

        var client = httpClientFactory.CreateClient("Overpass");
        var content = new FormUrlEncodedContent([new KeyValuePair<string, string>("data", query)]);
        var resp = await client.PostAsync("https://overpass-api.de/api/interpreter", content, ct);
        if (!resp.IsSuccessStatusCode) return 0;

        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("elements", out var elements) && elements.GetArrayLength() > 0)
        {
            var first = elements[0];
            if (first.TryGetProperty("tags", out var tags) &&
                tags.TryGetProperty("total", out var total) &&
                int.TryParse(total.GetString(), out var count))
                return count;
        }

        return 0;
    }

    private static double GetDensityFactor(int? buildingCount)
    {
        if (!buildingCount.HasValue || buildingCount.Value == 0) return 0.8;
        return Math.Min(1.3, 0.8 + buildingCount.Value / 200.0);
    }

    private static double GetSeverity(PrediCop.Core.Entities.Call call)
    {
        if (!call.Missions.Any()) return 0.3;
        if (call.Missions.Any(m => m.CompletedAt.HasValue)) return 1.0;
        if (call.Missions.Any(m => m.AcceptedAt.HasValue || m.ArrivedAt.HasValue)) return 0.8;
        return 0.6;
    }

    private static double HaversineM(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6_371_000;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
