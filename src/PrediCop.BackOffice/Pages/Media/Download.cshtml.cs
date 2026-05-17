using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PrediCop.BackOffice.Pages.Media;

// Proxies the file download from the API (so the browser benefits from the session JWT auth)
public class DownloadModel(IHttpClientFactory httpClientFactory, ILogger<DownloadModel> logger) : PageModel
{
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            var response = await client.GetAsync($"/api/media/{id}/file", HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode) return NotFound();

            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
            var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                        ?? response.Content.Headers.ContentDisposition?.FileName
                        ?? "video";

            var stream = await response.Content.ReadAsStreamAsync(ct);
            return File(stream, contentType, fileName);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Erreur téléchargement media {Id}.", id);
            return NotFound();
        }
    }
}
