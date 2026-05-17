using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.BackOffice.Models;
using System.Net.Http.Json;

namespace PrediCop.BackOffice.Pages.Calls;

[Authorize]
public class DetailsModel(IHttpClientFactory httpClientFactory, ILogger<DetailsModel> logger) : PageModel
{
    public CallDto? Call { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            Call = await client.GetFromJsonAsync<CallDto>($"/api/calls/{id}");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de charger l'appel {Id} depuis l'API", id);
            TempData["ErrorMessage"] = "Impossible de charger les détails de l'appel.";
        }

        if (Call == null) return NotFound();
        return Page();
    }
}
