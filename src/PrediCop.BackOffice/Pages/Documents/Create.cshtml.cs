using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.BackOffice.Models;
using System.Net.Http.Json;

namespace PrediCop.BackOffice.Pages.Documents;

public class CreateModel(IHttpClientFactory httpClientFactory, ILogger<CreateModel> logger) : PageModel
{
    [BindProperty]
    public CreateTrackingDocumentDto Input { get; set; } = new();

    public string? MissionReference { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid missionId, CancellationToken ct)
    {
        Input.MissionId = missionId;
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            var mission = await client.GetFromJsonAsync<MissionDto>($"/api/missions/{missionId}", ct);
            MissionReference = mission?.Reference;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de charger la mission {Id}.", missionId);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return Page();

        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            var payload = new
            {
                missionId = Input.MissionId,
                type = Input.Type,
                title = Input.Title
            };
            var response = await client.PostAsJsonAsync("/api/tracking", payload, ct);
            if (response.IsSuccessStatusCode)
            {
                var created = await response.Content.ReadFromJsonAsync<TrackingDocumentDto>(cancellationToken: ct);
                if (created != null)
                    return RedirectToPage("/Documents/Details", new { id = created.Id });
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                ModelState.AddModelError(string.Empty, $"Erreur : {error}");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Erreur création document de suivi.");
            ModelState.AddModelError(string.Empty, "Erreur de connexion à l'API.");
        }

        return Page();
    }
}
