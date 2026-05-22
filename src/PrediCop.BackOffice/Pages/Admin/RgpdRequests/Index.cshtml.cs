using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.Core.DTOs;
using System.Net.Http.Json;
using System.Text.Json;

namespace PrediCop.BackOffice.Pages.Admin.RgpdRequests;

[Authorize(Roles = "Admin")]
public class IndexModel(IHttpClientFactory httpClientFactory, ILogger<IndexModel> logger) : PageModel
{
    public List<RgpdRequestResponse> Requests { get; set; } = [];

    [BindProperty]
    public Guid ProcessId { get; set; }

    [BindProperty]
    public string? ProcessNotes { get; set; }

    public string GetTypeLabel(PrediCop.Core.Enums.RgpdRequestType type) => type switch
    {
        PrediCop.Core.Enums.RgpdRequestType.AccesData => "Droit d'accès",
        PrediCop.Core.Enums.RgpdRequestType.Rectification => "Droit de rectification",
        PrediCop.Core.Enums.RgpdRequestType.Suppression => "Droit à l'effacement",
        PrediCop.Core.Enums.RgpdRequestType.Portabilite => "Droit à la portabilité",
        PrediCop.Core.Enums.RgpdRequestType.Opposition => "Droit d'opposition",
        PrediCop.Core.Enums.RgpdRequestType.Limitation => "Droit à la limitation",
        _ => type.ToString()
    };

    public async Task OnGetAsync(CancellationToken ct = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            var requests = await client.GetFromJsonAsync<List<RgpdRequestResponse>>("/api/rgpd/requests", ct);
            Requests = requests ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de charger les demandes RGPD");
            TempData["ErrorMessage"] = "Impossible de charger les demandes RGPD.";
        }
    }

    public async Task<IActionResult> OnPostProcessAsync(CancellationToken ct = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            var payload = new ProcessRgpdRequest(ProcessNotes);
            var response = await client.PatchAsJsonAsync($"/api/rgpd/requests/{ProcessId}/process", payload, ct);

            if (response.IsSuccessStatusCode)
                TempData["SuccessMessage"] = "Demande marquée comme traitée.";
            else
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                logger.LogWarning("Traitement RGPD échoué {Status}: {Body}", (int)response.StatusCode, body);
                TempData["ErrorMessage"] = "Impossible de marquer la demande comme traitée.";
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Erreur traitement demande RGPD {Id}", ProcessId);
            TempData["ErrorMessage"] = "Erreur de communication avec le serveur.";
        }

        return RedirectToPage();
    }
}
