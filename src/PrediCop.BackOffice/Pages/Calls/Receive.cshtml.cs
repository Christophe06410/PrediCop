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
                    $"Appel {created.Reference} enregistré et mission créée avec succès.";
            }
            else
            {
                var body = await missionResponse.Content.ReadAsStringAsync();
                logger.LogWarning("Create mission failed {Status}: {Body}",
                    (int)missionResponse.StatusCode, body);
                TempData["SuccessMessage"] =
                    $"Appel {created.Reference} enregistré. La création de mission a échoué — lancez le dispatch manuellement.";
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

    public IActionResult OnPostCloseAsync()
    {
        TempData["InfoMessage"] = "Appel fermé sans suite.";
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

