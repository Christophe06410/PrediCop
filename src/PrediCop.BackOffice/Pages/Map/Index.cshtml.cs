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
            var positioned = (vehicles ?? [])
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
            var positioned = (vehicles ?? [])
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
}
