using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Json;
using System.Text.Json;

namespace PrediCop.BackOffice.Pages.Admin.RiskEvents;

[Authorize(Roles = "Admin,Manager")]
public class IndexModel(IHttpClientFactory httpClientFactory, ILogger<IndexModel> logger) : PageModel
{
    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    public List<RiskEventItem> Events { get; set; } = [];
    public List<StreetItem> Streets { get; set; } = [];

    [BindProperty(SupportsGet = true)] public Guid? StreetFilter { get; set; }
    [BindProperty(SupportsGet = true)] public bool ActiveOnly { get; set; }
    [BindProperty(SupportsGet = true)] public string? SearchTitle { get; set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("PrediCopApi");

        // Load streets for filter dropdown
        try
        {
            var streets = await client.GetFromJsonAsync<List<StreetItem>>("/api/streets", JsonOpts);
            Streets = streets ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de charger les rues");
        }

        // Build query string
        var qs = new List<string>();
        if (StreetFilter.HasValue)
            qs.Add($"streetId={StreetFilter.Value}");
        if (ActiveOnly)
            qs.Add("active=true");

        var url = "/api/streets/risk-events" + (qs.Count > 0 ? "?" + string.Join("&", qs) : "");

        try
        {
            var events = await client.GetFromJsonAsync<List<RiskEventItem>>(url, JsonOpts);
            Events = events ?? [];

            // Client-side title filter (simple contains)
            if (!string.IsNullOrWhiteSpace(SearchTitle))
                Events = Events
                    .Where(e => e.Title.Contains(SearchTitle, StringComparison.OrdinalIgnoreCase))
                    .ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de charger les événements de risque");
            TempData["ErrorMessage"] = "Impossible de charger les événements de risque.";
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid streetId, Guid eventId)
    {
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            var response = await client.DeleteAsync($"/api/streets/{streetId}/risk-events/{eventId}");
            if (response.IsSuccessStatusCode)
                TempData["SuccessMessage"] = "Événement supprimé.";
            else
                TempData["ErrorMessage"] = "Impossible de supprimer l'événement.";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Erreur suppression événement {EventId}", eventId);
            TempData["ErrorMessage"] = "Erreur lors de la suppression.";
        }
        return RedirectToPage();
    }

    public class RiskEventItem
    {
        public Guid Id { get; set; }
        public Guid StreetId { get; set; }
        public string StreetName { get; set; } = "";
        public string StreetDistrict { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public int RiskPoints { get; set; }
        public DateTime EventDate { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string Source { get; set; } = "";
        public bool IsActive { get; set; }
    }

    public class StreetItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string? District { get; set; }
    }
}
