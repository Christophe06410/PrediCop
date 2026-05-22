using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.BackOffice.Models;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using System.Text.Json;

namespace PrediCop.BackOffice.Pages.Missions;

[Authorize]
public class ShiftReportModel(
    IHttpClientFactory httpClientFactory,
    ILogger<ShiftReportModel> logger) : PageModel
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    // ---- Données de la page ----
    public List<VehicleDto> Vehicles { get; set; } = [];
    public ShiftReportDto? GeneratedReport { get; set; }
    public string? ErrorMessage { get; set; }

    // ---- Formulaire ----
    [BindProperty]
    [Required(ErrorMessage = "Veuillez sélectionner un véhicule.")]
    public Guid VehicleId { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "La date/heure de début est obligatoire.")]
    public DateTime ShiftStart { get; set; } = DateTime.Today.AddHours(6);

    [BindProperty]
    [Required(ErrorMessage = "La date/heure de fin est obligatoire.")]
    public DateTime ShiftEnd { get; set; } = DateTime.Today.AddHours(14);

    [BindProperty]
    public string? Notes { get; set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        await LoadVehiclesAsync(ct);
    }

    public async Task<IActionResult> OnPostGenerateAsync(CancellationToken ct)
    {
        await LoadVehiclesAsync(ct);

        if (!ModelState.IsValid)
            return Page();

        if (ShiftEnd <= ShiftStart)
        {
            ModelState.AddModelError(nameof(ShiftEnd), "La fin de vacation doit être postérieure au début.");
            return Page();
        }

        var client = httpClientFactory.CreateClient("PrediCopApi");
        var requestBody = new
        {
            VehicleId,
            ShiftStart = ShiftStart.ToUniversalTime(),
            ShiftEnd = ShiftEnd.ToUniversalTime(),
            Notes
        };

        try
        {
            var response = await client.PostAsJsonAsync("/api/shift-reports", requestBody, JsonOpts, ct);
            if (response.IsSuccessStatusCode)
            {
                GeneratedReport = await response.Content.ReadFromJsonAsync<ShiftReportDto>(JsonOpts, ct);
            }
            else
            {
                var detail = await response.Content.ReadAsStringAsync(ct);
                ErrorMessage = $"Erreur lors de la génération du rapport ({(int)response.StatusCode}) : {detail}";
                logger.LogWarning("Erreur génération rapport : {Status} {Detail}", response.StatusCode, detail);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Impossible de joindre le serveur API.";
            logger.LogError(ex, "Erreur lors de la génération du rapport de vacation");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSignAsync(Guid reportId, CancellationToken ct)
    {
        await LoadVehiclesAsync(ct);

        var client = httpClientFactory.CreateClient("PrediCopApi");
        try
        {
            var response = await client.PostAsync($"/api/shift-reports/{reportId}/sign", null, ct);
            if (response.IsSuccessStatusCode)
            {
                // Recharger le rapport signé
                var getResponse = await client.GetFromJsonAsync<ShiftReportDto>(
                    $"/api/shift-reports/{reportId}", JsonOpts, ct);
                GeneratedReport = getResponse;
            }
            else
            {
                ErrorMessage = $"Erreur lors de la signature ({(int)response.StatusCode}).";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Impossible de joindre le serveur API.";
            logger.LogError(ex, "Erreur lors de la signature du rapport {ReportId}", reportId);
        }

        return Page();
    }

    private async Task LoadVehiclesAsync(CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            var vehicles = await client.GetFromJsonAsync<List<VehicleDto>>("/api/vehicles", JsonOpts, ct);
            Vehicles = vehicles ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de charger la liste des véhicules");
            Vehicles = [];
        }
    }
}

// ---- DTO local pour désérialiser la réponse de l'API ----
public class ShiftReportDto
{
    public Guid Id { get; set; }
    public Guid VehicleId { get; set; }
    public string VehicleCallSign { get; set; } = string.Empty;
    public DateTime ShiftStart { get; set; }
    public DateTime ShiftEnd { get; set; }
    public string OfficerNames { get; set; } = string.Empty;
    public int MissionCount { get; set; }
    public int CompletedMissionCount { get; set; }
    public int RefusedMissionCount { get; set; }
    public int PatrolRecordCount { get; set; }
    public double EstimatedKm { get; set; }
    public int DocumentCount { get; set; }
    public string? Notes { get; set; }
    public bool IsSigned { get; set; }
    public DateTime? SignedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public TimeSpan Duration => ShiftEnd - ShiftStart;
}
