using Microsoft.AspNetCore.Mvc;
using PrediCop.Core.DTOs;
using System.Net.Http.Json;

namespace PrediCop.BackOffice.ViewComponents;

public class OptionalModulesMenuViewComponent(IHttpClientFactory httpClientFactory) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        TenantFeatureFlagsResponse? flags = null;
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            flags = await client.GetFromJsonAsync<TenantFeatureFlagsResponse>("/api/tenant/features");
        }
        catch
        {
            // API indisponible ou utilisateur non authentifié : on n'affiche rien
        }

        return View(flags);
    }
}
