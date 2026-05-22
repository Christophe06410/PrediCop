using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.Core.DTOs;
using PrediCop.Core.Enums;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrediCop.BackOffice.Pages.Admin.Fleet;

[Authorize(Roles = "Admin,Manager")]
public class IndexModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(IHttpClientFactory httpClientFactory, ILogger<IndexModel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    // ---- Résultats ----
    public List<VehicleSummaryResponse> VehicleSummaries { get; set; } = [];
    public List<VehicleMaintenanceResponse> UpcomingMaintenances { get; set; } = [];
    public List<FleetAlertResponse> Alerts { get; set; } = [];
    public List<VehicleItem> Vehicles { get; set; } = [];

    // ---- Filtres ----
    [BindProperty(SupportsGet = true)]
    public string ActiveTab { get; set; } = "overview";

    [BindProperty(SupportsGet = true)]
    public Guid? VehicleFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? DateFrom { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? DateTo { get; set; }

    public List<VehicleLogEntryResponse> LogEntries { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("PrediCopApi");

        // Véhicules (pour le filtre)
        try
        {
            var vehiclesRaw = await client.GetFromJsonAsync<List<VehicleItemDto>>("/api/vehicles", ct);
            Vehicles = vehiclesRaw?.Select(v => new VehicleItem(v.Id, v.CallSign, v.LicensePlate)).ToList() ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de charger la liste des véhicules.");
        }

        // Résumé flotte
        try
        {
            VehicleSummaries = await client.GetFromJsonAsync<List<VehicleSummaryResponse>>("/api/fleet/summary", JsonOpts, ct) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de charger le résumé de la flotte.");
        }

        // Maintenances à venir
        try
        {
            UpcomingMaintenances = await client.GetFromJsonAsync<List<VehicleMaintenanceResponse>>("/api/fleet/maintenance?upcoming=true", JsonOpts, ct) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de charger les maintenances à venir.");
        }

        // Alertes
        try
        {
            Alerts = await client.GetFromJsonAsync<List<FleetAlertResponse>>("/api/fleet/alerts", JsonOpts, ct) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de charger les alertes de flotte.");
        }

        // Carnet de bord si onglet actif
        if (ActiveTab == "logbook")
        {
            try
            {
                var qs = BuildLogbookQueryString();
                LogEntries = await client.GetFromJsonAsync<List<VehicleLogEntryResponse>>($"/api/fleet/log-entries{qs}", JsonOpts, ct) ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Impossible de charger le carnet de bord.");
            }
        }

        return Page();
    }

    public async Task<IActionResult> OnPostCompleteMaintenanceAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("PrediCopApi");
            var body = new { completedAt = DateTime.UtcNow, kmAtService = (int?)null };
            var content = new StringContent(JsonSerializer.Serialize(body, JsonOpts), Encoding.UTF8, "application/json");
            await client.PostAsync($"/api/fleet/maintenance/{id}/complete", content, ct);
            TempData["SuccessMessage"] = "Maintenance marquée comme effectuée.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de marquer la maintenance {Id} comme effectuée.", id);
            TempData["ErrorMessage"] = "Erreur lors de la mise à jour de la maintenance.";
        }

        return RedirectToPage(new { ActiveTab = "maintenances" });
    }

    public static string GetMaintenanceTypeLabel(MaintenanceType t) => t switch
    {
        MaintenanceType.Revision => "Révision",
        MaintenanceType.ControleTechnique => "Contrôle technique",
        MaintenanceType.Reparation => "Réparation",
        MaintenanceType.Nettoyage => "Nettoyage",
        MaintenanceType.Pneumatiques => "Pneumatiques",
        MaintenanceType.Carrosserie => "Carrosserie",
        MaintenanceType.Autre => "Autre",
        _ => t.ToString()
    };

    private string BuildLogbookQueryString()
    {
        var parts = new List<string>();
        if (VehicleFilter.HasValue) parts.Add($"vehicleId={VehicleFilter.Value}");
        if (!string.IsNullOrWhiteSpace(DateFrom)) parts.Add($"dateFrom={Uri.EscapeDataString(DateFrom)}");
        if (!string.IsNullOrWhiteSpace(DateTo)) parts.Add($"dateTo={Uri.EscapeDataString(DateTo)}");
        return parts.Count > 0 ? "?" + string.Join("&", parts) : string.Empty;
    }

    // ---- Classes internes ----

    public record VehicleItem(Guid Id, string CallSign, string LicensePlate);

    private record VehicleItemDto(Guid Id, string CallSign, string LicensePlate);
}
