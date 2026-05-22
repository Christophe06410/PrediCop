using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.BackOffice.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace PrediCop.BackOffice.Pages.Missions;

[Authorize]
public class DetailsModel(IHttpClientFactory httpClientFactory, ILogger<DetailsModel> logger) : PageModel
{
    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    private static readonly JsonSerializerOptions SerializeOpts =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public MissionDto? Mission { get; set; }
    public string StreetsJson { get; set; } = "[]";
    public string VehiclesJson { get; set; } = "[]";
    public string AssignedVehicleJson { get; set; } = "null";
    public double? DistanceKm { get; set; }
    public List<VehicleDto> OnMissionVehicles { get; set; } = [];
    public bool CanForceAssign =>
        Mission is not null
        && (Mission.Priority == "Critique" || Mission.Priority == "SOS")
        && (Mission.Status is "Pending" or "Proposed");

    /// <summary>Vrai si cet appel a des missions antérieures (mission de reprise).</summary>
    public bool IsResumedMission => Mission?.SiblingMissions.Count > 0;

    private List<VehicleMapItem> _vehicles = [];

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var client = httpClientFactory.CreateClient("PrediCopApi");
        await Task.WhenAll(
            LoadMissionAsync(id, client),
            LoadStreetsAsync(client),
            LoadVehiclesAsync(client));

        if (Mission is null) return NotFound();

        ComputeDistanceToTarget();

        if (CanForceAssign)
            await LoadOnMissionVehiclesAsync(client);

        return Page();
    }

    public async Task<IActionResult> OnPostSaveDetailsAsync(
        Guid id,
        [FromForm] string? locationDetail,
        [FromForm] string? narrativeReport,
        [FromForm] string? dispatchedAt,
        [FromForm] string? arrivedAt,
        CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            var payload = new
            {
                locationDetail = string.IsNullOrWhiteSpace(locationDetail) ? null : locationDetail,
                narrativeReport = string.IsNullOrWhiteSpace(narrativeReport) ? null : narrativeReport,
                dispatchedAt = TryParseDate(dispatchedAt),
                arrivedAt = TryParseDate(arrivedAt)
            };
            var response = await client.PutAsJsonAsync($"/api/missions/{id}", payload);
            if (response.IsSuccessStatusCode)
                TempData["SuccessMessage"] = "Détails sauvegardés.";
            else
                TempData["ErrorMessage"] = "Erreur lors de la sauvegarde.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur sauvegarde détails mission {Id}", id);
            TempData["ErrorMessage"] = "Impossible de joindre le serveur.";
        }
        return RedirectToPage(new { id });
    }

    private static DateTime? TryParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateTime.TryParse(value, out var dt) ? dt.ToUniversalTime() : null;
    }

    public async Task<IActionResult> OnPostDispatchAsync(Guid id)
    {
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            var response = await client.PostAsJsonAsync($"/api/missions/{id}/propose", (object?)null);

            if (response.IsSuccessStatusCode)
                TempData["SuccessMessage"] = "Mission proposée au prochain véhicule disponible.";
            else
            {
                var body = await response.Content.ReadAsStringAsync();
                logger.LogWarning("Dispatch failed {Status}: {Body}", (int)response.StatusCode, body);

                if ((int)response.StatusCode == 503)
                    TempData["ErrorMessage"] = "Aucun véhicule disponible.";
                else
                    TempData["ErrorMessage"] = $"Erreur lors du dispatch ({(int)response.StatusCode}).";
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur dispatch mission {Id}", id);
            TempData["ErrorMessage"] = "Impossible de joindre le serveur.";
        }

        return RedirectToPage(new { id });
    }

    private async Task LoadMissionAsync(Guid id, HttpClient client)
    {
        try
        {
            Mission = await client.GetFromJsonAsync<MissionDto>($"/api/missions/{id}", JsonOpts);
            if (Mission is not null)
            {
                Mission.AssignedVehicleCallSign ??= Mission.Assignments
                    .FirstOrDefault(a => a.Status is "Accepted" or "InProgress" or "Proposed")
                    ?.VehicleCallSign;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Impossible de charger la mission {Id}", id);
        }
    }

    private async Task LoadStreetsAsync(HttpClient client)
    {
        try
        {
            var streets = await client.GetFromJsonAsync<List<StreetMapItem>>("/api/streets", JsonOpts);
            if (streets?.Count > 0)
                StreetsJson = JsonSerializer.Serialize(streets, SerializeOpts);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de charger les rues pour la carte de mission");
        }
    }

    private async Task LoadVehiclesAsync(HttpClient client)
    {
        try
        {
            var vehicles = await client.GetFromJsonAsync<List<VehicleMapItem>>("/api/vehicles", JsonOpts);
            if (vehicles?.Count > 0)
            {
                _vehicles = vehicles
                    .Where(v => v.LastLatitude.HasValue && v.LastLongitude.HasValue)
                    .ToList();
                VehiclesJson = JsonSerializer.Serialize(_vehicles, SerializeOpts);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de charger les véhicules pour la carte de mission");
        }
    }

    private async Task LoadOnMissionVehiclesAsync(HttpClient client)
    {
        try
        {
            var vehicles = await client.GetFromJsonAsync<List<VehicleDto>>("/api/vehicles?status=OnMission", JsonOpts);
            OnMissionVehicles = vehicles ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de charger les véhicules OnMission");
        }
    }

    private void ComputeDistanceToTarget()
    {
        if (Mission?.AssignedVehicleCallSign is null) return;

        var vehicle = _vehicles.FirstOrDefault(
            v => string.Equals(v.CallSign, Mission.AssignedVehicleCallSign, StringComparison.OrdinalIgnoreCase));

        if (vehicle?.LastLatitude is null || vehicle.LastLongitude is null) return;

        DistanceKm = Haversine(
            vehicle.LastLatitude.Value, vehicle.LastLongitude.Value,
            Mission.TargetLatitude, Mission.TargetLongitude);

        AssignedVehicleJson = JsonSerializer.Serialize(vehicle, SerializeOpts);
    }

    private static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private class StreetMapItem
    {
        public string Name { get; set; } = "";
        public string? District { get; set; }
        public int CurrentRiskScore { get; set; }
        public double StartLatitude { get; set; }
        public double StartLongitude { get; set; }
        public double EndLatitude { get; set; }
        public double EndLongitude { get; set; }
    }

    private class VehicleMapItem
    {
        public string CallSign { get; set; } = "";
        public string Status { get; set; } = "";
        public double? LastLatitude { get; set; }
        public double? LastLongitude { get; set; }
        public List<string> OfficerNames { get; set; } = [];
    }
}
