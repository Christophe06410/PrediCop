using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.BackOffice.Models;
using System.Net.Http.Json;

namespace PrediCop.BackOffice.Pages.Admin.Audit;

[Authorize(Roles = "Admin")]
public class IndexModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(IHttpClientFactory httpClientFactory, ILogger<IndexModel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    // ---- Résultats ----
    public List<AuditLogDto> Logs { get; set; } = [];
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }

    // ---- Filtres liés ----
    [BindProperty(SupportsGet = true)]
    public string? FilterEntityName { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? FilterAction { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? FilterUserName { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? FilterFrom { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? FilterTo { get; set; }

    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;

    public const int PageSize = 50;

    // Valeurs fixes pour les selects
    public static readonly string[] KnownEntityNames =
    [
        "Call", "Mission", "MissionAssignment", "MissionIntervenant",
        "PatrolVehicle", "PatrolRecord",
        "Street", "StreetRiskEvent",
        "GeoZone", "GeoZoneVertex",
        "TrackingDocument", "TrackingEntry",
        "MediaAttachment", "User", "VehicleOfficer"
    ];

    public static readonly string[] KnownActions = ["Created", "Updated", "Deleted"];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("PrediCopApi");

            var qs = BuildQueryString();
            var result = await client.GetFromJsonAsync<AuditLogPagedDto>($"/api/audit{qs}", ct);

            if (result is not null)
            {
                Logs = result.Items;
                TotalCount = result.TotalCount;
                TotalPages = result.TotalPages;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de charger le journal d'audit depuis l'API.");
        }

        return Page();
    }

    private string BuildQueryString()
    {
        var parts = new List<string> { $"page={CurrentPage}", $"size={PageSize}" };

        if (!string.IsNullOrWhiteSpace(FilterEntityName))
            parts.Add($"entityName={Uri.EscapeDataString(FilterEntityName)}");

        if (!string.IsNullOrWhiteSpace(FilterAction))
            parts.Add($"action={Uri.EscapeDataString(FilterAction)}");

        if (!string.IsNullOrWhiteSpace(FilterUserName))
            parts.Add($"userName={Uri.EscapeDataString(FilterUserName)}");

        if (!string.IsNullOrWhiteSpace(FilterFrom))
            parts.Add($"from={Uri.EscapeDataString(FilterFrom)}");

        if (!string.IsNullOrWhiteSpace(FilterTo))
            parts.Add($"to={Uri.EscapeDataString(FilterTo)}");

        return "?" + string.Join("&", parts);
    }
}
