using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.Core.DTOs;
using System.Net.Http.Json;

namespace PrediCop.BackOffice.Pages.Home;

public class IndexModel(IHttpClientFactory httpClientFactory, ILogger<IndexModel> logger) : PageModel
{
    public TenantFeatureFlagsResponse? Flags { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            Flags = await client.GetFromJsonAsync<TenantFeatureFlagsResponse>("/api/tenant/features", ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de charger les feature flags.");
        }
    }
}
