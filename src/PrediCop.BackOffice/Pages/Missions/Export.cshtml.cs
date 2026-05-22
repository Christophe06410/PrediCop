using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.BackOffice.Models;
using PrediCop.BackOffice.Services;
using System.Net.Http.Json;
using System.Text.Json;

namespace PrediCop.BackOffice.Pages.Missions;

[Authorize]
public class ExportModel(
    IHttpClientFactory httpClientFactory,
    ILogger<ExportModel> logger,
    MapSnapshotService mapSnapshot) : PageModel
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("PrediCopApi");
        MissionDto? mission;
        try
        {
            mission = await client.GetFromJsonAsync<MissionDto>($"/api/missions/{id}", JsonOpts, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Impossible de charger la mission {Id} pour l'export PDF", id);
            return NotFound();
        }

        if (mission is null) return NotFound();

        mission.AssignedVehicleCallSign ??= mission.Assignments
            .FirstOrDefault(a => a.Status is "Accepted" or "InProgress" or "Proposed")
            ?.VehicleCallSign;

        // Fetch streets for map overlay
        IEnumerable<MapSnapshotService.StreetSegment> streets = [];
        try
        {
            var streetList = await client.GetFromJsonAsync<List<ExportStreetDto>>("/api/streets", JsonOpts, ct);
            streets = streetList?.Select(s => new MapSnapshotService.StreetSegment(
                s.StartLatitude, s.StartLongitude, s.EndLatitude, s.EndLongitude)) ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de charger les rues pour la carte PDF");
        }

        byte[]? mapImage = null;
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows))
        {
            mapImage = await mapSnapshot.GetMapSnapshotAsync(
                mission.TargetLatitude, mission.TargetLongitude, streets, ct);
        }

        var pdfBytes = MissionPdfGenerator.Generate(mission, DateTime.UtcNow, mapImage);
        var filename = $"mission_{SanitizeFilename(mission.Reference)}_{DateTime.UtcNow:yyyyMMdd}.pdf";
        return File(pdfBytes, "application/pdf", filename);
    }

    private static string SanitizeFilename(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    private class ExportStreetDto
    {
        public double StartLatitude { get; set; }
        public double StartLongitude { get; set; }
        public double EndLatitude { get; set; }
        public double EndLongitude { get; set; }
    }
}
