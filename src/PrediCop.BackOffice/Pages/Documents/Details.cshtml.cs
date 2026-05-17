using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.BackOffice.Models;
using System.Net.Http.Json;

namespace PrediCop.BackOffice.Pages.Documents;

public class DetailsModel(IHttpClientFactory httpClientFactory, ILogger<DetailsModel> logger) : PageModel
{
    public TrackingDocumentDto? Document { get; set; }
    public List<MediaAttachmentDto> MediaAttachments { get; set; } = [];

    [BindProperty]
    public AddTrackingEntryDto NewEntry { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        return await LoadDocument(id, ct);
    }

    public async Task<IActionResult> OnPostAddEntryAsync(Guid id, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await LoadDocument(id, ct);
            return Page();
        }

        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            var payload = new
            {
                type = NewEntry.Type,
                content = NewEntry.Content,
                occurredAt = NewEntry.OccurredAt.ToUniversalTime()
            };
            var response = await client.PostAsJsonAsync($"/api/tracking/{id}/entries", payload, ct);
            if (!response.IsSuccessStatusCode)
            {
                ModelState.AddModelError(string.Empty, "Erreur lors de l'ajout de l'entrée.");
                await LoadDocument(id, ct);
                return Page();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Erreur ajout entrée document {Id}.", id);
            ModelState.AddModelError(string.Empty, "Erreur de connexion à l'API.");
            await LoadDocument(id, ct);
            return Page();
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostUpdateStatusAsync(Guid id, string newStatus, CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            var payload = new { status = newStatus };
            await client.PutAsJsonAsync($"/api/tracking/{id}", payload, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Erreur mise à jour statut document {Id}.", id);
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            await client.DeleteAsync($"/api/tracking/{id}", ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Erreur suppression document {Id}.", id);
        }

        return RedirectToPage("/Documents/Index");
    }

    public async Task<IActionResult> OnPostDeleteEntryAsync(Guid id, Guid entryId, CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            await client.DeleteAsync($"/api/tracking/{id}/entries/{entryId}", ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Erreur suppression entrée {EntryId}.", entryId);
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteMediaAsync(Guid id, Guid mediaId, CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            await client.DeleteAsync($"/api/media/{mediaId}", ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Erreur suppression média {MediaId}.", mediaId);
        }
        return RedirectToPage(new { id });
    }

    private async Task<IActionResult> LoadDocument(Guid id, CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            var docTask = client.GetFromJsonAsync<TrackingDocumentDto>($"/api/tracking/{id}", ct);
            var mediaTask = client.GetFromJsonAsync<List<MediaAttachmentDto>>($"/api/media?documentId={id}", ct);
            await Task.WhenAll(docTask, mediaTask);
            Document = docTask.Result;
            MediaAttachments = mediaTask.Result ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de charger le document {Id}.", id);
        }

        if (Document == null) return NotFound();

        NewEntry.OccurredAt = DateTime.Now;
        return Page();
    }
}
