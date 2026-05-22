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

    public IndexModel(IHttpClientFactory httpClientFactory, ILogger<IndexModel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public int Days { get; set; } = 7;

    public DashboardDto Dashboard { get; set; } = new();
    public TimeSeriesStatsResponse TimeSeriesStats { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        Days = Math.Clamp(Days, 1, 90);

        try
        {
            var client = _httpClientFactory.CreateClient("PrediCopApi");

            var dashTask = client.GetFromJsonAsync<DashboardDto>("/api/dashboard");
            var tsTask = client.GetFromJsonAsync<TimeSeriesStatsResponse>(
                $"/api/dashboard/timeseries?days={Days}");

            await Task.WhenAll(dashTask, tsTask);

            Dashboard = dashTask.Result ?? new DashboardDto();
            TimeSeriesStats = tsTask.Result ?? new TimeSeriesStatsResponse();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de charger le dashboard depuis l'API.");
        }

        return Page();
    }
}
