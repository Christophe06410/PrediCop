using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.BackOffice.Models;
using System.Net.Http.Json;

namespace PrediCop.BackOffice.Pages.Missions;

public class EditModel(IHttpClientFactory httpClientFactory, ILogger<EditModel> logger) : PageModel
{
    [BindProperty]
    public EditMissionDto Input { get; set; } = new();

    public MissionDto? Mission { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var client = httpClientFactory.CreateClient("PrediCopApi");
        try
        {
            Mission = await client.GetFromJsonAsync<MissionDto>($"/api/missions/{id}");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de charger la mission {Id}", id);
        }

        if (Mission is null) return NotFound();

        Input = new EditMissionDto
        {
            BriefingText = Mission.BriefingText,
            TargetAddress = Mission.TargetAddress
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        ModelState.Remove(nameof(Input.CompletionReport));
        if (!ModelState.IsValid) return await ReloadAndReturn(id);

        var client = httpClientFactory.CreateClient("PrediCopApi");
        try
        {
            var response = await client.PutAsJsonAsync($"/api/missions/{id}", new
            {
                Input.BriefingText,
                Input.TargetAddress
            });

            if (!response.IsSuccessStatusCode)
            {
                ModelState.AddModelError(string.Empty, $"Erreur serveur ({(int)response.StatusCode}).");
                return await ReloadAndReturn(id);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Impossible de mettre à jour la mission {Id}", id);
            ModelState.AddModelError(string.Empty, "Erreur de connexion à l'API.");
            return await ReloadAndReturn(id);
        }

        TempData["SuccessMessage"] = "Mission mise à jour avec succès.";
        return RedirectToPage("/Missions/Details", new { id });
    }

    public async Task<IActionResult> OnPostCompleteAsync(Guid id, string? completionReport)
    {
        var client = httpClientFactory.CreateClient("PrediCopApi");
        try
        {
            var response = await client.PostAsJsonAsync(
                $"/api/missions/{id}/complete",
                new { Report = completionReport ?? string.Empty });

            if (!response.IsSuccessStatusCode)
            {
                TempData["ErrorMessage"] = $"Impossible de clôturer la mission ({(int)response.StatusCode}).";
            }
            else
            {
                TempData["SuccessMessage"] = "Mission clôturée avec succès.";
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Impossible de clôturer la mission {Id}", id);
            TempData["ErrorMessage"] = "Erreur de connexion à l'API.";
        }

        return RedirectToPage("/Missions/Details", new { id });
    }

    private async Task<PageResult> ReloadAndReturn(Guid id)
    {
        var client = httpClientFactory.CreateClient("PrediCopApi");
        try { Mission = await client.GetFromJsonAsync<MissionDto>($"/api/missions/{id}"); }
        catch { Mission = new MissionDto { Id = id, Reference = "—" }; }
        return Page();
    }
}
