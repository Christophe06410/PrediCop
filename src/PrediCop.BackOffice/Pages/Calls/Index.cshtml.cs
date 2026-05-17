using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.BackOffice.Models;
using System.Net.Http.Json;

namespace PrediCop.BackOffice.Pages.Calls;

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

    public List<CallDto> Calls { get; set; } = [];
    public int TotalCount { get; set; }
    public int PageSize { get; set; } = 20;

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public string? FilterDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? FilterStatus { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? FilterCategory { get; set; }

    public int TotalPages => TotalCount > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 1;

    public static readonly List<string> Statuses = new() { "Open", "InProgress", "MissionCreated", "Closed" };
    public static readonly List<string> Categories = new() { "Tapage", "Vol", "Bagarre", "Accident", "Autre" };

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadCallsAsync();
        return Page();
    }

    private async Task LoadCallsAsync()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("PrediCopApi");
            var url = $"/api/calls?page={PageNumber}&size={PageSize}";
            if (!string.IsNullOrWhiteSpace(FilterDate)) url += $"&date={FilterDate}";
            if (!string.IsNullOrWhiteSpace(FilterStatus)) url += $"&status={FilterStatus}";
            if (!string.IsNullOrWhiteSpace(FilterCategory)) url += $"&category={FilterCategory}";
            var result = await client.GetFromJsonAsync<PagedResult<CallDto>>(url);
            Calls = result?.Items ?? [];
            TotalCount = result?.TotalCount ?? 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Impossible de charger les appels depuis l'API.");
            TempData["ErrorMessage"] = "Impossible de charger les appels. Vérifiez que l'API est démarrée.";
            Calls = [];
            TotalCount = 0;
        }
    }
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}
