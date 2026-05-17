using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.BackOffice.Models;
using System.Net.Http.Json;

namespace PrediCop.BackOffice.Pages.Search;

public class IndexModel(IHttpClientFactory httpClientFactory, ILogger<IndexModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    public SearchResponseDto? Results { get; set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Q))
            return;

        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            Results = await client.GetFromJsonAsync<SearchResponseDto>(
                $"/api/search?q={Uri.EscapeDataString(Q)}", ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de joindre l'API de recherche.");
        }
    }
}
