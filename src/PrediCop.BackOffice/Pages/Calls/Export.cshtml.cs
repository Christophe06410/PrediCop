using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.BackOffice.Models;
using PrediCop.BackOffice.Services;
using System.Net.Http.Json;
using System.Text.Json;

namespace PrediCop.BackOffice.Pages.Calls;

[Authorize]
public class ExportModel(
    IHttpClientFactory httpClientFactory,
    ILogger<ExportModel> logger) : PageModel
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("PrediCopApi");

        // Fetch call with embedded missions list (assignments only, no intervenants)
        CallDto? call;
        try
        {
            call = await client.GetFromJsonAsync<CallDto>($"/api/calls/{id}", JsonOpts, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Impossible de charger l'appel {Id} pour l'export PDF", id);
            return NotFound();
        }

        if (call is null) return NotFound();

        // Fetch full mission details (with intervenants) for each linked mission
        var missions = new List<MissionDto>(call.Missions.Count);
        foreach (var summary in call.Missions.OrderBy(m => m.CreatedAt))
        {
            try
            {
                var fullMission = await client.GetFromJsonAsync<MissionDto>(
                    $"/api/missions/{summary.Id}", JsonOpts, ct);

                if (fullMission is not null)
                {
                    // Resolve assigned vehicle call sign from assignments if not set
                    fullMission.AssignedVehicleCallSign ??= fullMission.Assignments
                        .FirstOrDefault(a => a.Status is "Accepted" or "InProgress" or "Proposed")
                        ?.VehicleCallSign;

                    missions.Add(fullMission);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Impossible de charger les détails de la mission {MissionId}", summary.Id);
                // Fall back to the summary already embedded in the call
                missions.Add(summary);
            }
        }

        var pdfBytes = CallReportPdfGenerator.Generate(call, missions, DateTime.UtcNow);
        var filename = $"rapport_mc_{SanitizeFilename(call.Reference)}_{DateTime.UtcNow:yyyyMMdd}.pdf";
        return File(pdfBytes, "application/pdf", filename);
    }

    private static string SanitizeFilename(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}
