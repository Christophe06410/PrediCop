using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.Core.DTOs;
using PrediCop.Core.Enums;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrediCop.BackOffice.Pages.Admin.Fourriere;

[Authorize(Roles = "Admin,Manager,Operator")]
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

    public List<ImpoundedVehicleResponse> Vehicles { get; set; } = [];
    public FourriereStatsResponse? Stats { get; set; }
    public List<AgentItem> Agents { get; set; } = [];

    [BindProperty(SupportsGet = true)]
    public string? StatusFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? AgentFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? PlateSearch { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("PrediCopApi");

        // Charger les agents
        try
        {
            var agents = await client.GetFromJsonAsync<List<AgentItem>>("/api/users", JsonOpts, ct);
            Agents = agents ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de charger la liste des agents.");
        }

        // Charger les statistiques
        try
        {
            Stats = await client.GetFromJsonAsync<FourriereStatsResponse>("/api/fourriere/stats", JsonOpts, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de charger les statistiques de fourrière.");
        }

        // Charger les véhicules avec les filtres actifs
        try
        {
            var qs = BuildQueryString();
            var vehicles = await client.GetFromJsonAsync<List<ImpoundedVehicleResponse>>(
                $"/api/fourriere{qs}", JsonOpts, ct);
            Vehicles = vehicles ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de charger les véhicules en fourrière.");
            TempData["ErrorMessage"] = "Impossible de joindre le serveur.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostReleaseAsync(
        Guid id, string releasedToName, string releasedToIdNumber, CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("PrediCopApi");
            var response = await client.PostAsJsonAsync(
                $"/api/fourriere/{id}/release",
                new ReleaseVehicleRequest(releasedToName, releasedToIdNumber, null),
                JsonOpts,
                ct);

            if (response.IsSuccessStatusCode)
                TempData["SuccessMessage"] = "Véhicule restitué avec succès.";
            else
                TempData["ErrorMessage"] = "Impossible de restituer le véhicule.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erreur lors de la restitution du véhicule {Id}.", id);
            TempData["ErrorMessage"] = "Une erreur est survenue.";
        }

        return RedirectToPage(new { StatusFilter, AgentFilter, PlateSearch });
    }

    public async Task<IActionResult> OnPostDestroyAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("PrediCopApi");
            var response = await client.PostAsync($"/api/fourriere/{id}/destroy", null, ct);

            if (response.IsSuccessStatusCode)
                TempData["SuccessMessage"] = "Véhicule marqué comme détruit.";
            else
                TempData["ErrorMessage"] = "Impossible de marquer le véhicule comme détruit.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erreur lors de la destruction du véhicule {Id}.", id);
            TempData["ErrorMessage"] = "Une erreur est survenue.";
        }

        return RedirectToPage(new { StatusFilter, AgentFilter, PlateSearch });
    }

    // -------- Helpers labels --------

    public static string GetReasonLabel(ImpoundReason reason) => reason switch
    {
        ImpoundReason.StationnementGenant => "Stationnement gênant",
        ImpoundReason.StationnementDangereux => "Stationnement dangereux",
        ImpoundReason.Epave => "Épave",
        ImpoundReason.Abandon => "Abandon",
        ImpoundReason.StationnementHandicap => "Stationnement handicap",
        ImpoundReason.SansAssurance => "Sans assurance",
        ImpoundReason.SansControleTechnique => "Sans contrôle technique",
        ImpoundReason.Autre => "Autre",
        _ => reason.ToString()
    };

    public static string GetStatusLabel(ImpoundStatus status) => status switch
    {
        ImpoundStatus.InStorage => "En fourrière",
        ImpoundStatus.Released => "Restitué",
        ImpoundStatus.Destroyed => "Détruit",
        _ => status.ToString()
    };

    public static string GetStatusBadgeClass(ImpoundStatus status) => status switch
    {
        ImpoundStatus.InStorage => "bg-primary",
        ImpoundStatus.Released => "bg-success",
        ImpoundStatus.Destroyed => "bg-secondary",
        _ => "bg-secondary"
    };

    // -------- Helpers privés --------

    private string BuildQueryString()
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(StatusFilter))
            parts.Add($"status={Uri.EscapeDataString(StatusFilter)}");

        if (!string.IsNullOrWhiteSpace(AgentFilter))
            parts.Add($"agentId={Uri.EscapeDataString(AgentFilter)}");

        if (!string.IsNullOrWhiteSpace(PlateSearch))
            parts.Add($"plate={Uri.EscapeDataString(PlateSearch)}");

        return parts.Count > 0 ? "?" + string.Join("&", parts) : string.Empty;
    }

    // -------- Inner types --------

    public class AgentItem
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string BadgeNumber { get; set; } = string.Empty;
        public string FullName => $"{FirstName} {LastName}";
    }
}
