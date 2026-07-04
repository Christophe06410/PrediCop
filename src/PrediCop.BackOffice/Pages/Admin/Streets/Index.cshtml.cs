using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;

namespace PrediCop.BackOffice.Pages.Admin.Streets;

[Authorize(Roles = "Admin,Manager")]
public class IndexModel(IHttpClientFactory httpClientFactory, ILogger<IndexModel> logger) : PageModel
{
    [BindProperty]
    public CreateStreetInput Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public Task OnGetAsync() => Task.CompletedTask;

    public async Task<IActionResult> OnGetSearchAsync(
        string? search, string? riskLevel, string? sort,
        int page = 1, int pageSize = 25, CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 10, 200);
        var client = httpClientFactory.CreateClient("PrediCopApi");
        try
        {
            var qs = new List<string>();
            if (!string.IsNullOrWhiteSpace(search)) qs.Add($"search={Uri.EscapeDataString(search)}");
            if (!string.IsNullOrWhiteSpace(riskLevel)) qs.Add($"riskLevel={riskLevel}");
            if (!string.IsNullOrWhiteSpace(sort)) qs.Add($"sort={sort}");
            qs.Add($"page={page}");
            qs.Add($"pageSize={pageSize}");
            var url = "/api/streets/paged?" + string.Join("&", qs);
            var result = await client.GetFromJsonAsync<PagedStreetsResult>(url, ct);
            return new JsonResult(result ?? new());
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Streets search failed");
            return new JsonResult(new PagedStreetsResult());
        }
    }

    private class PagedStreetsResult
    {
        public List<StreetDto> Streets { get; set; } = [];
        public int TotalCount { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
    }

    public async Task<IActionResult> OnPostAddAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");

            // Use Nominatim to geocode start address if coordinates not provided
            var body = new
            {
                Input.Name,
                Input.District,
                Input.City,
                Input.BaseRiskScore,
                CurrentRiskScore = Input.BaseRiskScore,
                Input.StartLatitude,
                Input.StartLongitude,
                Input.EndLatitude,
                Input.EndLongitude
            };

            var response = await client.PostAsJsonAsync("/api/streets", body);
            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = $"Rue \"{Input.Name}\" ajoutée.";
                return RedirectToPage();
            }

            var err = await response.Content.ReadAsStringAsync();
            logger.LogWarning("Add street failed {Status}: {Body}", (int)response.StatusCode, err);
            ErrorMessage = $"Erreur API ({(int)response.StatusCode}).";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de l'ajout d'une rue");
            ErrorMessage = "Impossible de joindre le serveur.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostEditAsync(
        Guid id, int baseRiskScore, double riskGrowthRatePerWeek,
        double nightRiskGrowthRatePerWeek, int nightStartHour, int nightEndHour,
        bool isRiskLocked, int? riskAdjustment)
    {
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            var response = await client.PutAsJsonAsync($"/api/streets/{id}", new
            {
                baseRiskScore,
                riskGrowthRatePerWeek,
                nightRiskGrowthRatePerWeek,
                nightStartHour,
                nightEndHour,
                isRiskLocked,
                riskAdjustment
            });
            if (response.IsSuccessStatusCode)
                TempData["SuccessMessage"] = "Paramètres de risque mis à jour.";
            else
                TempData["ErrorMessage"] = $"Erreur API ({(int)response.StatusCode}).";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur modification rue {Id}", id);
            TempData["ErrorMessage"] = "Impossible de modifier la rue.";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRecomputeAsync()
    {
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            var resp = await client.PostAsync("/api/streets/recompute-risks", null);
            TempData[resp.IsSuccessStatusCode ? "SuccessMessage" : "ErrorMessage"] =
                resp.IsSuccessStatusCode
                    ? "Recalcul des scores de risque terminé."
                    : $"Erreur lors du recalcul ({(int)resp.StatusCode}).";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur recalcul des risques");
            TempData["ErrorMessage"] = "Impossible de lancer le recalcul.";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            await client.DeleteAsync($"/api/streets/{id}");
            TempData["SuccessMessage"] = "Rue supprimée.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur suppression rue {Id}", id);
            TempData["ErrorMessage"] = "Impossible de supprimer la rue.";
        }

        return RedirectToPage();
    }

}

public class StreetDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? District { get; set; }
    public string? City { get; set; }
    public int BaseRiskScore { get; set; }
    public int ComputedBaseRiskScore { get; set; }
    public bool IsRiskLocked { get; set; }
    public int? RiskAdjustment { get; set; }
    public double RiskGrowthRatePerWeek { get; set; }
    public double NightRiskGrowthRatePerWeek { get; set; }
    public int NightStartHour { get; set; }
    public int NightEndHour { get; set; }
    public int CurrentRiskScore { get; set; }
    public double StartLatitude { get; set; }
    public double StartLongitude { get; set; }
    public double EndLatitude { get; set; }
    public double EndLongitude { get; set; }
    public DateTime? LastPatrolledAt { get; set; }
}

public class CreateStreetInput
{
    [Required(ErrorMessage = "Le nom est obligatoire.")]
    [Display(Name = "Nom de la rue")]
    public string Name { get; set; } = "";

    [Display(Name = "Quartier")]
    public string? District { get; set; }

    [Display(Name = "Ville")]
    public string City { get; set; } = "";

    [Required]
    [Range(0, 100, ErrorMessage = "Le score doit être entre 0 et 100.")]
    [Display(Name = "Score de risque de base (0-100)")]
    public int BaseRiskScore { get; set; } = 30;

    [Required]
    [Display(Name = "Latitude début")]
    public double StartLatitude { get; set; }

    [Required]
    [Display(Name = "Longitude début")]
    public double StartLongitude { get; set; }

    [Required]
    [Display(Name = "Latitude fin")]
    public double EndLatitude { get; set; }

    [Required]
    [Display(Name = "Longitude fin")]
    public double EndLongitude { get; set; }
}
