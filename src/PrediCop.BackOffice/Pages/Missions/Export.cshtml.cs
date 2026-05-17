using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.BackOffice.Models;
using PrediCop.BackOffice.Services;
using System.Net.Http.Json;
using System.Text.Json;

namespace PrediCop.BackOffice.Pages.Missions;

[Authorize]
public class ExportModel(IHttpClientFactory httpClientFactory, ILogger<ExportModel> logger) : PageModel
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var client = httpClientFactory.CreateClient("PrediCopApi");
        MissionDto? mission;
        try
        {
            mission = await client.GetFromJsonAsync<MissionDto>($"/api/missions/{id}", JsonOpts);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Impossible de charger la mission {Id} pour l'export PDF", id);
            return NotFound();
        }

        if (mission is null) return NotFound();

        // Derive assigned vehicle the same way as the Details page
        mission.AssignedVehicleCallSign ??= mission.Assignments
            .FirstOrDefault(a => a.Status is "Accepted" or "InProgress" or "Proposed")
            ?.VehicleCallSign;

        var pdfBytes = MissionPdfGenerator.Generate(mission, DateTime.UtcNow);
        var filename = $"mission_{SanitizeFilename(mission.Reference)}_{DateTime.UtcNow:yyyyMMdd}.pdf";
        return File(pdfBytes, "application/pdf", filename);
    }

    private static string SanitizeFilename(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}
