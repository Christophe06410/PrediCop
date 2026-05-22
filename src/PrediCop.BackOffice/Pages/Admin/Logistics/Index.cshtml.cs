using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.Core.DTOs;
using PrediCop.Core.Enums;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrediCop.BackOffice.Pages.Admin.Logistics;

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

    public List<EquipmentCatalogResponse> Catalog { get; set; } = [];
    public List<EquipmentIssuanceResponse> Issuances { get; set; } = [];
    public List<LogisticsAlertResponse> Alerts { get; set; } = [];
    public List<AgentItem> Agents { get; set; } = [];
    public int ExpiringCount { get; set; }

    [BindProperty(SupportsGet = true)]
    public string ActiveTab { get; set; } = "catalog";

    [BindProperty(SupportsGet = true)]
    public Guid? AgentFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool ExpiredOnly { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("PrediCopApi");

        // Load agents
        try
        {
            var users = await client.GetFromJsonAsync<List<AgentItem>>("/api/users", JsonOpts, ct);
            Agents = users ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de charger la liste des agents.");
        }

        // Load catalogue
        try
        {
            var catalog = await client.GetFromJsonAsync<List<EquipmentCatalogResponse>>(
                "/api/logistics/catalog?activeOnly=false", JsonOpts, ct);
            Catalog = catalog ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de charger le catalogue équipements.");
        }

        // Load issuances
        try
        {
            var url = "/api/logistics/issuances?notReturned=true";
            if (AgentFilter.HasValue)
                url += $"&agentId={AgentFilter.Value}";
            if (ExpiredOnly)
                url += "&expiredOnly=true";

            var issuances = await client.GetFromJsonAsync<List<EquipmentIssuanceResponse>>(url, JsonOpts, ct);
            Issuances = issuances ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de charger les dotations.");
        }

        // Load alerts
        try
        {
            var alerts = await client.GetFromJsonAsync<List<LogisticsAlertResponse>>(
                "/api/logistics/alerts", JsonOpts, ct);
            Alerts = alerts ?? [];
            ExpiringCount = Alerts.Count;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de charger les alertes logistiques.");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostReturnAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("PrediCopApi");
            var response = await client.PostAsJsonAsync($"/api/logistics/issuances/{id}/return", new { }, ct);
            if (!response.IsSuccessStatusCode)
                TempData["ErrorMessage"] = "Impossible de marquer la dotation comme rendue.";
            else
                TempData["SuccessMessage"] = "Dotation marquée comme rendue.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erreur lors du retour de la dotation {Id}.", id);
            TempData["ErrorMessage"] = "Une erreur est survenue.";
        }

        return RedirectToPage(new { ActiveTab = "issuances", AgentFilter, ExpiredOnly });
    }

    public string GetCategoryLabel(EquipmentCategory category) => category switch
    {
        EquipmentCategory.Uniforme => "Uniforme",
        EquipmentCategory.EquipementProtection => "Protection",
        EquipmentCategory.Armement => "Armement",
        EquipmentCategory.Materiel => "Matériel",
        EquipmentCategory.Vehicule => "Véhicule",
        EquipmentCategory.Informatique => "Informatique",
        EquipmentCategory.Autre => "Autre",
        _ => category.ToString()
    };

    public string GetCategoryBadgeClass(EquipmentCategory category) => category switch
    {
        EquipmentCategory.Uniforme => "bg-primary",
        EquipmentCategory.EquipementProtection => "bg-warning text-dark",
        EquipmentCategory.Armement => "bg-danger",
        EquipmentCategory.Materiel => "bg-info text-dark",
        EquipmentCategory.Vehicule => "bg-dark",
        EquipmentCategory.Informatique => "bg-secondary",
        EquipmentCategory.Autre => "bg-light text-dark border",
        _ => "bg-secondary"
    };

    public class AgentItem
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string BadgeNumber { get; set; } = string.Empty;
        public string FullName => $"{FirstName} {LastName}";
    }
}
