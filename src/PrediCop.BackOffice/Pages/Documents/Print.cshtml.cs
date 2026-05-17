using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.BackOffice.Models;
using System.Net.Http.Json;

namespace PrediCop.BackOffice.Pages.Documents;

public class PrintModel(IHttpClientFactory httpClientFactory, ILogger<PrintModel> logger) : PageModel
{
    public TrackingDocumentDto? Document { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            Document = await client.GetFromJsonAsync<TrackingDocumentDto>($"/api/tracking/{id}", ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de charger le document {Id} pour impression.", id);
        }

        if (Document is null)
            return NotFound();

        return Page();
    }
}
