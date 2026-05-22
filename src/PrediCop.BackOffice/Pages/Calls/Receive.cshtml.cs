using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.BackOffice.Models;
using System.Net.Http.Json;

namespace PrediCop.BackOffice.Pages.Calls;

[Authorize(Roles = "Admin,Manager,Operator")]
public class ReceiveModel(IHttpClientFactory httpClientFactory, ILogger<ReceiveModel> logger) : PageModel
{
    [BindProperty]
    public CreateCallDto Input { get; set; } = new();

    public List<CallDto> TodayCalls { get; set; } = [];
    public DateTime CallStartTime { get; set; } = DateTime.Now;

    public static readonly List<string> Categories =
        ["Tapage", "Vol", "Bagarre", "Accident", "Incivilité", "Trouble à l'ordre public", "Autre"];

    public async Task<IActionResult> OnGetAsync()
    {
        CallStartTime = DateTime.Now;
        ViewData["ActiveCall"] = true;
        ViewData["CallStartTime"] = CallStartTime;
        await LoadTodayCallsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostCreateMissionAsync()
    {
        ViewData["ActiveCall"] = true;
        ViewData["CallStartTime"] = DateTime.Now;

        if (!ModelState.IsValid)
        {
            await LoadTodayCallsAsync();
            return Page();
        }

        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");

            // Step 1 — create the call
            var callResponse = await client.PostAsJsonAsync("/api/calls", Input);
            if (!callResponse.IsSuccessStatusCode)
            {
                var body = await callResponse.Content.ReadAsStringAsync();
                logger.LogWarning("Create call failed {Status}: {Body}", (int)callResponse.StatusCode, body);
                ModelState.AddModelError(string.Empty,
                    $"Erreur lors de l'enregistrement de l'appel ({(int)callResponse.StatusCode}).");
                await LoadTodayCallsAsync();
                return Page();
            }

            var created = await callResponse.Content.ReadFromJsonAsync<CallDto>();
            if (created is null)
            {
                ModelState.AddModelError(string.Empty, "Réponse inattendue du serveur.");
                await LoadTodayCallsAsync();
                return Page();
            }

            // Step 2 — create the mission from the call
            var missionResponse = await client.PostAsJsonAsync(
                $"/api/calls/{created.Id}/create-mission", (object?)null);

            if (missionResponse.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] =
                    $"Main courante {created.Reference} enregistrée et mission créée avec succès.";
            }
            else
            {
                var body = await missionResponse.Content.ReadAsStringAsync();
                logger.LogWarning("Create mission failed {Status}: {Body}",
                    (int)missionResponse.StatusCode, body);
                TempData["SuccessMessage"] =
                    $"Main courante {created.Reference} enregistrée. La création de mission a échoué — lancez le dispatch manuellement.";
            }

            return RedirectToPage("/Calls/Index");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de la création de l'appel/mission");
            ModelState.AddModelError(string.Empty,
                "Impossible de joindre le serveur. Vérifiez que l'API est démarrée.");
            await LoadTodayCallsAsync();
            return Page();
        }
    }

    /// <summary>
    /// Saves the current form as a Draft call via the API.
    /// Does NOT create a mission and does NOT redirect away — the operator stays on this page
    /// to continue filling in details or to handle the next incoming call.
    /// The timer bar keeps running because ViewData["ActiveCall"] stays true.
    /// </summary>
    public async Task<IActionResult> OnPostSaveDraftAsync()
    {
        // Keep the active-call UI (timer bar) alive
        ViewData["ActiveCall"] = true;
        ViewData["CallStartTime"] = DateTime.Now;

        // Minimal validation: we only require enough to persist a draft
        // (full validation is done on CreateMission, so we clear required-field errors here)
        ModelState.Clear();

        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");

            // Send the call body with status = Draft so the API stores it without creating a mission
            var draftBody = new
            {
                Input.CallerName,
                Input.CallerPhone,
                Input.IncidentCategory,
                Input.IncidentDescription,
                Input.IncidentAddress,
                Input.IncidentAddressComplement,
                Input.IncidentLatitude,
                Input.IncidentLongitude,
                Input.ThirdParties,
                Input.InternalNotes,
                Status = "Draft"
            };

            var callResponse = await client.PostAsJsonAsync("/api/calls", draftBody);

            if (!callResponse.IsSuccessStatusCode)
            {
                var body = await callResponse.Content.ReadAsStringAsync();
                logger.LogWarning("Save draft failed {Status}: {Body}", (int)callResponse.StatusCode, body);
                TempData["DraftErrorMessage"] =
                    $"Impossible de sauvegarder le brouillon ({(int)callResponse.StatusCode}). Vérifiez que l'API est démarrée.";
                await LoadTodayCallsAsync();
                return Page();
            }

            var created = await callResponse.Content.ReadFromJsonAsync<CallDto>();
            TempData["DraftSuccessMessage"] =
                $"Brouillon sauvegardé — Réf. {created?.Reference ?? "?"} — vous pouvez continuer la saisie ou commencer un nouvel appel.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de la sauvegarde du brouillon");
            TempData["DraftErrorMessage"] =
                "Impossible de joindre le serveur. Vérifiez que l'API est démarrée.";
        }

        await LoadTodayCallsAsync();
        return Page();
    }

    public IActionResult OnPostCloseAsync()
    {
        TempData["InfoMessage"] = "Main courante fermée sans suite.";
        return RedirectToPage("/Calls/Index");
    }

    private async Task LoadTodayCallsAsync()
    {
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            var today = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
            var calls = await client.GetFromJsonAsync<PagedResult<CallDto>>($"/api/calls?date={today}&size=50");
            TodayCalls = calls?.Items ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de charger les appels du jour.");
            TodayCalls = [];
        }
    }
}

