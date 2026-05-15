using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PoliceMunicipale.BackOffice.Models;
using System.Net.Http.Json;

namespace PoliceMunicipale.BackOffice.Pages.Calls;

public class ReceiveModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ReceiveModel> _logger;

    public ReceiveModel(IHttpClientFactory httpClientFactory, ILogger<ReceiveModel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [BindProperty]
    public CreateCallDto Input { get; set; } = new();

    public List<CallDto> TodayCalls { get; set; } = [];

    public DateTime CallStartTime { get; set; } = DateTime.Now;

    public static readonly List<string> Categories = new()
    {
        "Tapage", "Vol", "Bagarre", "Accident", "Autre"
    };

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
            var client = _httpClientFactory.CreateClient("PoliceMunicipaleApi");
            var response = await client.PostAsJsonAsync("/api/calls", Input);
            if (response.IsSuccessStatusCode)
            {
                var created = await response.Content.ReadFromJsonAsync<CallDto>();
                TempData["SuccessMessage"] = $"Appel {created?.Reference} enregistré. Mission créée avec succès.";
                return RedirectToPage("/Calls/Index");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Erreur lors de la création de l'appel. Veuillez réessayer.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "API non disponible, simulation de création d'appel.");
            TempData["SuccessMessage"] = "Appel enregistré (mode simulation). Mission créée avec succès.";
            return RedirectToPage("/Calls/Index");
        }

        await LoadTodayCallsAsync();
        return Page();
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
            var client = _httpClientFactory.CreateClient("PoliceMunicipaleApi");
            var calls = await client.GetFromJsonAsync<List<CallDto>>("/api/calls?date=today");
            TodayCalls = calls ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de charger les appels du jour depuis l'API.");
            TodayCalls = GetFakeTodayCalls();
        }
    }

    private static List<CallDto> GetFakeTodayCalls()
    {
        var now = DateTime.Now;
        return new List<CallDto>
        {
            new() { Id = Guid.NewGuid(), Reference = "APP-001", ReceivedAt = now.AddHours(-3), CallerName = "M. Dupont", IncidentCategory = "Tapage", IncidentAddress = "12 rue de la Paix", Status = "MissionCreated" },
            new() { Id = Guid.NewGuid(), Reference = "APP-002", ReceivedAt = now.AddHours(-1.5), CallerName = "Mme Martin", IncidentCategory = "Vol", IncidentAddress = "Place du marché", Status = "InProgress" },
            new() { Id = Guid.NewGuid(), Reference = "APP-003", ReceivedAt = now.AddMinutes(-20), CallerName = "M. Bernard", IncidentCategory = "Accident", IncidentAddress = "Avenue Gambetta", Status = "Open" },
        };
    }
}
