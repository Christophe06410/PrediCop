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

    public DashboardDto Dashboard { get; set; } = new();
    public TimeSeriesStatsResponse TimeSeriesStats { get; set; } = new();

    public int ExpiringQualificationsCount { get; set; }
    public int ExpiredQualificationsCount { get; set; }
    public bool HasQualificationAlert => ExpiringQualificationsCount > 0 || ExpiredQualificationsCount > 0;

    public async Task<IActionResult> OnGetAsync()
    {
        Days = Math.Clamp(Days, 1, 90);

        var client = _httpClientFactory.CreateClient("PrediCopApi");

        var dashTask = client.GetFromJsonAsync<DashboardDto>("/api/dashboard");
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
        }

        return Page();
    }

    private class QualificationItem { public Guid Id { get; set; } }
}
