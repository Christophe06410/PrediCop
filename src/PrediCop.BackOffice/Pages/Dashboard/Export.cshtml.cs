using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PrediCop.BackOffice.Pages.Dashboard;

[Authorize(Roles = "Admin,Manager")]
public class ExportModel(IHttpClientFactory httpClientFactory, ILogger<ExportModel> logger) : PageModel
{
    [BindProperty]
    public string Period { get; set; } = string.Empty; // "2026-05"

    [BindProperty]
    public string Format { get; set; } = "xlsx";

    public string? ErrorMessage { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Period) || !Period.Contains('-'))
        {
            ErrorMessage = "Période invalide.";
            return Page();
        }

        var parts = Period.Split('-');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var year) || !int.TryParse(parts[1], out var month))
        {
            ErrorMessage = "Format de période invalide (attendu : YYYY-MM).";
            return Page();
        }

        var fmt = Format == "csv" ? "csv" : "xlsx";
        var url = $"/api/dashboard/export?year={year}&month={month}&format={fmt}";

        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            var response = await client.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = $"L'API a renvoyé une erreur ({(int)response.StatusCode}). Vérifiez que l'API est démarrée.";
                return Page();
            }

            var content = await response.Content.ReadAsByteArrayAsync(ct);
            var contentType = fmt == "csv"
                ? "text/csv; charset=utf-8"
                : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            var fileName = $"predicop-{(fmt == "csv" ? "missions" : "stats")}-{Period}.{fmt}";

            return File(content, contentType, fileName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors du téléchargement de l'export");
            ErrorMessage = "Impossible de joindre l'API. Vérifiez qu'elle est démarrée.";
            return Page();
        }
    }
}
