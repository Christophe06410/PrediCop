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
