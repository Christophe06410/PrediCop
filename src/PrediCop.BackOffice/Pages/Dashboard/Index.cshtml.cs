using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.BackOffice.Models;
using System.Net.Http.Json;

namespace PrediCop.BackOffice.Pages.Dashboard;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<IndexModel> _logger;

    private static readonly JsonSerializerOptions _jsonOpts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public IndexModel(IHttpClientFactory httpClientFactory, ILogger<IndexModel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public int Days { get; set; } = 7;

    [BindProperty(SupportsGet = true)]
    public DateTime? DateFrom { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? DateTo { get; set; }

    // Valeurs effectives (locales) pour pré-remplir les inputs du formulaire
    public string DateFromInput => (DateFrom ?? DateTime.Today).ToString("yyyy-MM-ddTHH:mm");
    public string DateToInput   => (DateTo   ?? DateTime.Now  ).ToString("yyyy-MM-ddTHH:mm");

    public DashboardDto Dashboard { get; set; } = new();
    public TimeSeriesStatsResponse TimeSeriesStats { get; set; } = new();

    public int ExpiringQualificationsCount { get; set; }
    public int ExpiredQualificationsCount { get; set; }
    public bool HasQualificationAlert => ExpiringQualificationsCount > 0 || ExpiredQualificationsCount > 0;

    public int FleetOverdueCount { get; set; }
    public int FleetUpcomingCount { get; set; }
    public bool HasFleetAlert => FleetOverdueCount > 0 || FleetUpcomingCount > 0;

    public async Task<IActionResult> OnGetTimeSeriesAsync(int days = 7, CancellationToken ct = default)
    {
        days = Math.Clamp(days, 1, 90);
        var client = _httpClientFactory.CreateClient("PrediCopApi");
        try
        {
            var result = await client.GetFromJsonAsync<TimeSeriesStatsResponse>(
                $"/api/dashboard/timeseries?days={days}", _jsonOpts, ct);
            return new JsonResult(result ?? new(), new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                Converters = { new JsonStringEnumConverter() }
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TimeSeries fetch failed for {Days} days", days);
            return new JsonResult(new TimeSeriesStatsResponse(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        Days = Math.Clamp(Days, 1, 90);

        var client = _httpClientFactory.CreateClient("PrediCopApi");

        // Conversion locale → UTC pour l'API
        var fromUtc = (DateFrom ?? DateTime.Today).ToUniversalTime();
        var toUtc   = (DateTo   ?? DateTime.Now  ).ToUniversalTime();
        var dashUrl = $"/api/dashboard?from={Uri.EscapeDataString(fromUtc.ToString("o"))}&to={Uri.EscapeDataString(toUtc.ToString("o"))}";

        var dashTask = client.GetFromJsonAsync<DashboardDto>(dashUrl);
        var tsTask   = client.GetFromJsonAsync<TimeSeriesStatsResponse>(
            $"/api/dashboard/timeseries?days={Days}");

        try { await Task.WhenAll(dashTask, tsTask); }
        catch (Exception ex) { _logger.LogWarning(ex, "Impossible de charger le dashboard depuis l'API."); }

        Dashboard       = dashTask.IsCompletedSuccessfully ? dashTask.Result ?? new() : new();
        TimeSeriesStats = tsTask.IsCompletedSuccessfully   ? tsTask.Result   ?? new() : new();

        // Widget habilitations — visible uniquement pour Admin/Manager (403 ignoré pour les autres rôles)
        if (User.IsInRole("Admin") || User.IsInRole("Manager"))
        {
            try
            {
                var expiring = await client.GetFromJsonAsync<List<QualificationItem>>(
                    "/api/qualifications/expiring", _jsonOpts);
                ExpiringQualificationsCount = expiring?.Count ?? 0;
            }
            catch { ExpiringQualificationsCount = 0; }

            try
            {
                var expired = await client.GetFromJsonAsync<List<QualificationItem>>(
                    "/api/qualifications?expiredOnly=true", _jsonOpts);
                ExpiredQualificationsCount = expired?.Count ?? 0;
            }
            catch { ExpiredQualificationsCount = 0; }

            // Widget flotte — ignoré si le module est désactivé (403)
            try
            {
                var alerts = await client.GetFromJsonAsync<List<FleetAlertItem>>(
                    "/api/fleet/alerts", _jsonOpts);
                if (alerts is not null)
                {
                    var now = DateTime.UtcNow;
                    FleetOverdueCount  = alerts.Count(a => a.DueDate < now);
                    FleetUpcomingCount = alerts.Count(a => a.DueDate >= now);
                }
            }
            catch { /* module désactivé ou indisponible */ }
        }

        return Page();
    }

    private class QualificationItem { public Guid Id { get; set; } }
    private class FleetAlertItem { public DateTime DueDate { get; set; } }
}
