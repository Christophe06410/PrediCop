using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Json;
using System.Text.Json;

namespace PrediCop.BackOffice.Pages.Map;

[Authorize]
public class IndexModel(IHttpClientFactory httpClientFactory, ILogger<IndexModel> logger) : PageModel
{
    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    private static readonly JsonSerializerOptions SerializeOpts =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public string StreetsJson { get; set; } = "[]";
    public string VehiclesJson { get; set; } = "[]";
    public string GeoZonesJson { get; set; } = "[]";

    public async Task<IActionResult> OnGetAsync()
    {
        var client = httpClientFactory.CreateClient("PrediCopApi");
        await Task.WhenAll(LoadStreetsAsync(client), LoadVehiclesAsync(client), LoadGeoZonesAsync(client));
        return Page();
    }

    // Appelé par le JS toutes les 15 s via fetch('?handler=VehiclesJson')
    public async Task<IActionResult> OnGetVehiclesJsonAsync()
    {
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            var vehicles = await client.GetFromJsonAsync<List<VehicleItem>>("/api/vehicles", JsonOpts);
            var enriched = await EnrichWithMissionsAsync(client, vehicles ?? []);
            var positioned = enriched
                .Where(v => v.LastLatitude.HasValue && v.LastLongitude.HasValue)
                .ToList();
            return new JsonResult(positioned, SerializeOpts);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de charger les positions véhicules");
            return new JsonResult(Array.Empty<object>());
        }
    }

    // Appelé par le JS quand l'opérateur clique "Changer de mission"
    public async Task<IActionResult> OnGetPendingMissionsAsync()
    {
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            // Fetch missions with status=Pending (not yet assigned to any vehicle)
            var result = await client.GetFromJsonAsync<PagedMissions>("/api/missions?status=0&size=50", JsonOpts);
            var pendingItems = (result?.Items ?? [])
                .Select(m => new PendingMissionItem
                {
                    Id = m.Id,
                    Reference = m.Reference,
                    TargetAddress = m.TargetAddress,
                    Status = m.Status,
                    CreatedAt = m.CreatedAt
                })
                .ToList();
            return new JsonResult(pendingItems, SerializeOpts);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de charger les missions en attente");
            return new JsonResult(Array.Empty<object>());
        }
    }

    // Appelé par le JS pour rediriger une mission vers un véhicule spécifique
    public async Task<IActionResult> OnPostRerouteMissionAsync(
        [FromBody] RerouteRequest request,
        CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            // Use existing propose endpoint — it will assign to the next available vehicle.
            // We call it directly for the selected mission.
            var resp = await client.PostAsync($"/api/missions/{request.MissionId}/propose", null, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                logger.LogWarning("Reroute failed: {Status} — {Body}", (int)resp.StatusCode, body);
                return new JsonResult(new { success = false, error = $"Erreur {(int)resp.StatusCode}" });
            }
            return new JsonResult(new { success = true });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Erreur lors du rerouting de la mission");
            return new JsonResult(new { success = false, error = ex.Message });
        }
    }

    private async Task LoadGeoZonesAsync(HttpClient client)
    {
        try
        {
            var zones = await client.GetFromJsonAsync<List<GeoZoneItem>>("/api/geozones", JsonOpts);
            if (zones?.Count > 0)
                GeoZonesJson = JsonSerializer.Serialize(zones, SerializeOpts);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de charger les zones géographiques");
        }
    }

    private async Task LoadStreetsAsync(HttpClient client)
    {
        try
        {
            var streets = await client.GetFromJsonAsync<List<StreetItem>>("/api/streets", JsonOpts);
            if (streets?.Count > 0)
                StreetsJson = JsonSerializer.Serialize(streets, SerializeOpts);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de charger les rues");
        }
    }

    private async Task LoadVehiclesAsync(HttpClient client)
    {
        try
        {
            var vehicles = await client.GetFromJsonAsync<List<VehicleItem>>("/api/vehicles", JsonOpts);
            var enriched = await EnrichWithMissionsAsync(client, vehicles ?? []);
            var positioned = enriched
                .Where(v => v.LastLatitude.HasValue && v.LastLongitude.HasValue)
                .ToList();
            if (positioned.Count > 0)
                VehiclesJson = JsonSerializer.Serialize(positioned, SerializeOpts);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de charger les véhicules");
        }
    }

    private async Task<List<VehicleItem>> EnrichWithMissionsAsync(HttpClient client, List<VehicleItem> vehicles)
    {
        try
        {
            var missions = await client.GetFromJsonAsync<List<ActiveMissionItem>>("/api/missions/active", JsonOpts);
            if (missions is null or { Count: 0 }) return vehicles;

            // Build a lookup: vehicleId -> active mission (accepted or in-progress first, then proposed)
            var missionByVehicle = new Dictionary<Guid, ActiveMissionItem>();
            foreach (var m in missions)
            {
                foreach (var a in m.Assignments)
                {
                    // Only consider active assignments (not refused)
                    if (a.Status is "Refused" or "Cancelled") continue;
                    if (!missionByVehicle.TryGetValue(a.VehicleId, out var existing))
                    {
                        missionByVehicle[a.VehicleId] = m;
                    }
                    else
                    {
                        // Prefer Accepted/InProgress over Proposed
                        var existingA = existing.Assignments.FirstOrDefault(x => x.VehicleId == a.VehicleId);
                        if (existingA?.Status is "Proposed" && a.Status is "Accepted" or "InProgress")
                            missionByVehicle[a.VehicleId] = m;
                    }
                }
            }

            foreach (var v in vehicles)
            {
                if (missionByVehicle.TryGetValue(v.Id, out var m))
                {
                    v.CurrentMissionId = m.Id;
                    v.CurrentMissionRef = m.Reference;
                    v.CurrentMissionAddress = m.TargetAddress;
                    v.CurrentMissionStatus = m.Status;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de charger les missions actives pour enrichissement");
        }
        return vehicles;
    }

    public class StreetItem
    {
        public string Name { get; set; } = "";
        public string? District { get; set; }
        public int CurrentRiskScore { get; set; }
        public double StartLatitude { get; set; }
        public double StartLongitude { get; set; }
        public double EndLatitude { get; set; }
        public double EndLongitude { get; set; }
    }

    public class VehicleItem
    {
        public Guid Id { get; set; }
        public string CallSign { get; set; } = "";
        public string Status { get; set; } = "";
        public double? LastLatitude { get; set; }
        public double? LastLongitude { get; set; }
        public DateTime? LastPositionUpdate { get; set; }
        public List<string> OfficerNames { get; set; } = [];

        // Current mission info (populated by joining with active missions)
        public Guid? CurrentMissionId { get; set; }
        public string? CurrentMissionRef { get; set; }
        public string? CurrentMissionAddress { get; set; }
        public string? CurrentMissionStatus { get; set; }
    }

    public class GeoZoneItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string Color { get; set; } = "#3b82f6";
        public bool IsActive { get; set; }
        public List<GeoZoneVertexItem> Vertices { get; set; } = [];
    }

    public class GeoZoneVertexItem
    {
        public int Order { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    public class ActiveMissionItem
    {
        public Guid Id { get; set; }
        public string Reference { get; set; } = "";
        public string Status { get; set; } = "";
        public string TargetAddress { get; set; } = "";
        public List<AssignmentItem> Assignments { get; set; } = [];
    }

    public class AssignmentItem
    {
        public Guid VehicleId { get; set; }
        public string Status { get; set; } = "";
    }

    public class PendingMissionItem
    {
        public Guid Id { get; set; }
        public string Reference { get; set; } = "";
        public string TargetAddress { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    public class PagedMissions
    {
        public List<MissionListItem> Items { get; set; } = [];
        public int TotalCount { get; set; }
    }

    public class MissionListItem
    {
        public Guid Id { get; set; }
        public string Reference { get; set; } = "";
        public string Status { get; set; } = "";
        public string TargetAddress { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    public class RerouteRequest
    {
        public Guid MissionId { get; set; }
        public Guid VehicleId { get; set; }
    }
}
