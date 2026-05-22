using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.BackOffice.Models;
using PrediCop.BackOffice.Services;
using System.Net.Http.Json;
using System.Text.Json;

namespace PrediCop.BackOffice.Pages.Missions;

[Authorize]
public class ExportWordModel(
    IHttpClientFactory httpClientFactory,
    ILogger<ExportWordModel> logger) : PageModel
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
            logger.LogError(ex, "Impossible de charger la mission {Id} pour l'export Word", id);
            return NotFound();
        }

        if (mission is null) return NotFound();

        mission.AssignedVehicleCallSign ??= mission.Assignments
            .FirstOrDefault(a => a.Status is "Accepted" or "InProgress" or "Proposed")
            ?.VehicleCallSign;

        var bytes = MissionWordGenerator.Generate(mission, DateTime.UtcNow);
        var filename = $"mission_{SanitizeFilename(mission.Reference)}_{DateTime.UtcNow:yyyyMMdd}.docx";
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            filename);
    }

    private static string SanitizeFilename(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}
