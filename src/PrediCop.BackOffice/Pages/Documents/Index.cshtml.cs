using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.BackOffice.Models;
using System.Net.Http.Json;

namespace PrediCop.BackOffice.Pages.Documents;

public class IndexModel(IHttpClientFactory httpClientFactory, ILogger<IndexModel> logger) : PageModel
{
    public List<TrackingDocumentDto> Documents { get; set; } = [];

    [BindProperty(SupportsGet = true)]
    public string? FilterType { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? FilterStatus { get; set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            var url = "/api/tracking";
            var qs = new List<string>();
            if (!string.IsNullOrEmpty(FilterStatus)) qs.Add($"status={FilterStatus}");
            if (!string.IsNullOrEmpty(FilterType)) qs.Add($"type={FilterType}");
            if (qs.Count > 0) url += "?" + string.Join("&", qs);

            Documents = await client.GetFromJsonAsync<List<TrackingDocumentDto>>(url, ct) ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de charger les documents de suivi.");
        }
    }
}
