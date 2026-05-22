using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.BackOffice.Models;
using System.Net.Http.Json;

namespace PrediCop.BackOffice.Pages.Calls;

[Authorize]
public class EditModel(IHttpClientFactory httpClientFactory, ILogger<EditModel> logger) : PageModel
{
    public static readonly List<string> Categories = ["Tapage", "Vol", "Bagarre", "Accident", "Nuisance", "Autre"];

    [BindProperty]
    public EditCallDto Input { get; set; } = new();

    public CallDto? Call { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var client = httpClientFactory.CreateClient("PrediCopApi");
        try
        {
            Call = await client.GetFromJsonAsync<CallDto>($"/api/calls/{id}");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de charger l'appel {Id}", id);
        }

        if (Call is null) return NotFound();

        Input = new EditCallDto
        {
            CallerName = Call.CallerName,
            CallerPhone = Call.CallerPhone,
            IncidentCategory = Call.IncidentCategory,
            IncidentDescription = Call.IncidentDescription,
            IncidentAddress = Call.IncidentAddress,
            IncidentAddressComplement = Call.IncidentAddressComplement,
            ThirdParties = Call.ThirdParties,
            Notes = Call.Notes,
            InternalNotes = Call.InternalNotes
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        if (!ModelState.IsValid) return await ReloadAndReturn(id);

        var client = httpClientFactory.CreateClient("PrediCopApi");
        try
        {
            var response = await client.PutAsJsonAsync($"/api/calls/{id}", Input);
            if (!response.IsSuccessStatusCode)
            {
                ModelState.AddModelError(string.Empty, $"Erreur serveur ({(int)response.StatusCode}).");
                return await ReloadAndReturn(id);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Impossible de mettre à jour l'appel {Id}", id);
            ModelState.AddModelError(string.Empty, "Erreur de connexion à l'API.");
            return await ReloadAndReturn(id);
        }

        TempData["SuccessMessage"] = "Main courante mise à jour avec succès.";
        return RedirectToPage("/Calls/Details", new { id });
    }

    public async Task<IActionResult> OnPostCloseAsync(Guid id, string? closeNotes)
    {
        var client = httpClientFactory.CreateClient("PrediCopApi");
        try
        {
            var response = await client.PostAsJsonAsync(
                $"/api/calls/{id}/close",
                new { InternalNotes = closeNotes });

            if (!response.IsSuccessStatusCode)
            {
                TempData["ErrorMessage"] = $"Impossible de fermer la main courante ({(int)response.StatusCode}).";
            }
            else
            {
                TempData["SuccessMessage"] = "Main courante fermée avec succès.";
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Impossible de fermer l'appel {Id}", id);
            TempData["ErrorMessage"] = "Erreur de connexion à l'API.";
        }

        return RedirectToPage("/Calls/Details", new { id });
    }

    private async Task<PageResult> ReloadAndReturn(Guid id)
    {
        var client = httpClientFactory.CreateClient("PrediCopApi");
        try { Call = await client.GetFromJsonAsync<CallDto>($"/api/calls/{id}"); }
        catch { Call = new CallDto { Id = id, Reference = "—" }; }
        return Page();
    }
}
