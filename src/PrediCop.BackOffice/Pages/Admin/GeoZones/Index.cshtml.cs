using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Json;

namespace PrediCop.BackOffice.Pages.Admin.GeoZones;

[Authorize(Roles = "Admin,Manager")]
public class IndexModel(IHttpClientFactory httpClientFactory, ILogger<IndexModel> logger) : PageModel
{
    public List<GeoZoneItem> Zones { get; set; } = [];

    public async Task OnGetAsync()
    {
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            var zones = await client.GetFromJsonAsync<List<GeoZoneItem>>("/api/geozones");
            Zones = zones ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de charger les zones");
            TempData["ErrorMessage"] = "Impossible de charger les zones géographiques.";
        }
    }

    public async Task<IActionResult> OnPostDetectStreetsAsync(Guid id)
    {
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            var response = await client.PostAsync($"/api/geozones/{id}/detect-streets", null);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<DetectStreetsResult>();
                var found = result?.OverpassTotal ?? 0;
                var created = result?.Created ?? 0;
                var skipped = result?.Skipped ?? 0;

                if (found == 0)
                    TempData["WarningMessage"] = "Aucune rue trouvée dans cette zone via OpenStreetMap. Vérifiez que le polygone de la zone couvre bien la zone souhaitée.";
                else
                    TempData["SuccessMessage"] = $"Détection terminée : {found} rue(s) trouvée(s) via OSM, {created} créée(s), {skipped} ignorée(s) (déjà présentes).";
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync();
                logger.LogWarning("Detect streets failed {Status}: {Body}", (int)response.StatusCode, body);

                // Try to extract the problem detail title
                string errorMsg = $"Erreur lors de la détection ({(int)response.StatusCode}).";
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("title", out var title))
                        errorMsg = title.GetString() ?? errorMsg;
                }
                catch { }

                TempData["ErrorMessage"] = errorMsg;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Erreur détection rues zone {Id}", id);
            TempData["ErrorMessage"] = "Impossible de joindre le serveur.";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            var response = await client.DeleteAsync($"/api/geozones/{id}");
            if (response.IsSuccessStatusCode)
                TempData["SuccessMessage"] = "Zone supprimée.";
            else
                TempData["ErrorMessage"] = "Impossible de supprimer la zone.";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Erreur suppression zone {Id}", id);
            TempData["ErrorMessage"] = "Erreur lors de la suppression.";
        }
        return RedirectToPage();
    }

    private record DetectStreetsResult(int Created, int Skipped, int OverpassTotal, string? Error);

    public class GeoZoneItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string Color { get; set; } = "#3b82f6";
        public bool IsActive { get; set; }
        public List<object> Vertices { get; set; } = [];
    }
}
