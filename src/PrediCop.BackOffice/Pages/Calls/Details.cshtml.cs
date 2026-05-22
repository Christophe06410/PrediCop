using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.BackOffice.Models;
using System.Net.Http.Json;

namespace PrediCop.BackOffice.Pages.Calls;

[Authorize]
public class DetailsModel(IHttpClientFactory httpClientFactory, ILogger<DetailsModel> logger) : PageModel
{
    public CallDto? Call { get; set; }

    /// <summary>Vrai si l'appel a au moins une mission terminée/refusée/annulée mais aucune active.</summary>
    public bool CanReopen { get; private set; }

    /// <summary>Vrai si au moins une mission est en cours (Proposed/Accepted/InProgress).</summary>
    public bool HasActiveMission { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            Call = await client.GetFromJsonAsync<CallDto>($"/api/calls/{id}");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de charger l'appel {Id} depuis l'API", id);
            TempData["ErrorMessage"] = "Impossible de charger les détails de l'appel.";
        }

        if (Call == null) return NotFound();

        ComputeReopenFlags();
        return Page();
    }

    public async Task<IActionResult> OnPostReopenAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            var response = await client.PostAsJsonAsync($"/api/calls/{id}/create-mission", (object?)null, ct);

            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = "Nouvelle mission créée avec succès. Le dispatch automatique est en cours.";
                return RedirectToPage("/Missions/Index");
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Échec création mission reprise pour l'appel {Id} : {Status} — {Body}",
                id, (int)response.StatusCode, body);
            TempData["ErrorMessage"] = (int)response.StatusCode == 400
                ? "Impossible de reprendre : une mission est déjà active sur cet appel."
                : $"Erreur lors de la création de la mission ({(int)response.StatusCode}).";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de la reprise de l'appel {Id}", id);
            TempData["ErrorMessage"] = "Impossible de joindre le serveur.";
        }

        return RedirectToPage(new { id });
    }

    private void ComputeReopenFlags()
    {
        if (Call is null) return;

        HasActiveMission = Call.Missions.Any(m =>
            m.Status is "Proposed" or "Accepted" or "InProgress");

        CanReopen = Call.Missions.Any() && !HasActiveMission;
    }
}
