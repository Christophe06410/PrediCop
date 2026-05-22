using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.Core.DTOs;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrediCop.BackOffice.Pages.Admin.Tickets;

[Authorize(Roles = "Admin,Manager")]
public class StatsModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<StatsModel> _logger;

    public StatsModel(IHttpClientFactory httpClientFactory, ILogger<StatsModel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public TicketStatsResponse? Stats { get; set; }

    [BindProperty(SupportsGet = true)]
    public string DateFrom { get; set; } = DateTime.Today.AddDays(-30).ToString("yyyy-MM-dd");

    [BindProperty(SupportsGet = true)]
    public string DateTo { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");

    [BindProperty(SupportsGet = true)]
    public Guid? AgentFilter { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("PrediCopApi");

            var url = $"/api/tickets/stats?dateFrom={DateFrom}&dateTo={DateTo}";
            if (AgentFilter.HasValue)
                url += $"&agentId={AgentFilter.Value}";

            Stats = await client.GetFromJsonAsync<TicketStatsResponse>(url, JsonOpts, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de charger les statistiques de verbalisation.");
        }

        return Page();
    }
}
