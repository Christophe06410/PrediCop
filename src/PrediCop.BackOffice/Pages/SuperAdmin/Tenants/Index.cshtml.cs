using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.Core.DTOs;
using System.Net.Http.Json;

namespace PrediCop.BackOffice.Pages.SuperAdmin.Tenants;

[Authorize(Roles = "SuperAdmin")]
public class IndexModel(IHttpClientFactory httpClientFactory, ILogger<IndexModel> logger) : PageModel
{
    public List<TenantAdminDto> Tenants { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            var result = await client.GetFromJsonAsync<List<TenantAdminDto>>("/api/superadmin/tenants");
            Tenants = result ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de charger les tenants.");
        }
        return Page();
    }

    public async Task<IActionResult> OnPostToggleActiveAsync(Guid id)
    {
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            await client.PostAsync($"/api/superadmin/tenants/{id}/toggle-active", null);
            TempData["SuccessMessage"] = "Statut du tenant mis à jour.";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de modifier le tenant {Id}.", id);
            TempData["ErrorMessage"] = "Erreur lors de la modification.";
        }
        return RedirectToPage();
    }
}
