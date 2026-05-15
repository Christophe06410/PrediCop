using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PoliceMunicipale.BackOffice.Models;
using System.Net.Http.Json;

namespace PoliceMunicipale.BackOffice.Pages.Calls;

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
            var client = _httpClientFactory.CreateClient("PoliceMunicipaleApi");
            var url = $"/api/calls?page={PageNumber}&pageSize={PageSize}";
            if (!string.IsNullOrWhiteSpace(FilterDate)) url += $"&date={FilterDate}";
            if (!string.IsNullOrWhiteSpace(FilterStatus)) url += $"&status={FilterStatus}";
            if (!string.IsNullOrWhiteSpace(FilterCategory)) url += $"&category={FilterCategory}";
            var result = await client.GetFromJsonAsync<PagedResult<CallDto>>(url);
            Calls = result?.Items ?? [];
            TotalCount = result?.TotalCount ?? 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de charger les appels depuis l'API.");
            Calls = GetFakeCalls();
            TotalCount = Calls.Count;
        }
    }

    private static List<CallDto> GetFakeCalls()
    {
        var now = DateTime.Now;
        return new List<CallDto>
        {
            new() { Id = Guid.NewGuid(), Reference = "APP-2026-001", ReceivedAt = now.AddHours(-5), CallerName = "M. Dupont Pierre", CallerPhone = "06 12 34 56 78", IncidentCategory = "Tapage", IncidentAddress = "12 rue de la Paix, 75001 Paris", Status = "MissionCreated" },
            new() { Id = Guid.NewGuid(), Reference = "APP-2026-002", ReceivedAt = now.AddHours(-3), CallerName = "Mme Martin Sophie", CallerPhone = "06 98 76 54 32", IncidentCategory = "Vol", IncidentAddress = "Place du Marché, 75003 Paris", Status = "InProgress" },
            new() { Id = Guid.NewGuid(), Reference = "APP-2026-003", ReceivedAt = now.AddHours(-1), CallerName = "M. Bernard Luc", CallerPhone = "07 11 22 33 44", IncidentCategory = "Accident", IncidentAddress = "5 Avenue Gambetta, 75020 Paris", Status = "Open" },
            new() { Id = Guid.NewGuid(), Reference = "APP-2026-004", ReceivedAt = now.AddDays(-1), CallerName = "Mme Petit Claire", CallerPhone = "06 55 44 33 22", IncidentCategory = "Bagarre", IncidentAddress = "Rue Saint-Denis, 75010 Paris", Status = "Closed" },
            new() { Id = Guid.NewGuid(), Reference = "APP-2026-005", ReceivedAt = now.AddDays(-1).AddHours(-2), CallerName = "M. Moreau Jean", CallerPhone = "07 66 77 88 99", IncidentCategory = "Autre", IncidentAddress = "Boulevard Voltaire, 75011 Paris", Status = "Closed" },
        };
    }
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}
