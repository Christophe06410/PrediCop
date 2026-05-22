using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Json;
using System.Text.Json;

namespace PrediCop.BackOffice.Pages.Admin.RiskEvents;

[Authorize(Roles = "Admin,Manager")]
public class EditModel(IHttpClientFactory httpClientFactory, ILogger<EditModel> logger) : PageModel
{
    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    [BindProperty] public Guid StreetId { get; set; }
    [BindProperty] public string Title { get; set; } = "";
    [BindProperty] public string Description { get; set; } = "";
    [BindProperty] public int RiskPoints { get; set; } = 10;
    [BindProperty] public DateTime EventDate { get; set; } = DateTime.Now;
    [BindProperty] public DateTime ExpiresAt { get; set; } = DateTime.Now.AddDays(1);
    [BindProperty] public string Source { get; set; } = "";

    public Guid? EventId { get; set; }
    public List<StreetItem> Streets { get; set; } = [];

    public async Task OnGetAsync(Guid? id, CancellationToken ct)
    {
        EventId = id;
        await LoadStreetsAsync(ct);

        if (id == null) return;

        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            // Fetch all events and find the one matching id
            var events = await client.GetFromJsonAsync<List<RiskEventDto>>("/api/streets/risk-events", JsonOpts);
            var ev = events?.FirstOrDefault(e => e.Id == id);
            if (ev == null)
            {
                TempData["ErrorMessage"] = "Événement introuvable.";
                return;
            }

            StreetId = ev.StreetId;
            Title = ev.Title;
            Description = ev.Description;
            RiskPoints = ev.RiskPoints;
            EventDate = ev.EventDate.ToLocalTime();
            ExpiresAt = ev.ExpiresAt.ToLocalTime();
            Source = ev.Source;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de charger l'événement {Id}", id);
            TempData["ErrorMessage"] = "Impossible de charger l'événement.";
        }
    }

    public async Task<IActionResult> OnPostAsync(Guid? id, CancellationToken ct)
    {
        if (StreetId == Guid.Empty)
        {
            TempData["ErrorMessage"] = "Veuillez sélectionner une rue.";
            await LoadStreetsAsync(ct);
            EventId = id;
            return Page();
        }

        var client = httpClientFactory.CreateClient("PrediCopApi");

        try
        {
            if (id == null)
            {
                // Create via POST /api/streets/{streetId}/risk-event
                var body = new
                {
                    title = Title,
                    description = Description,
                    riskPoints = RiskPoints,
                    eventDate = EventDate.ToUniversalTime(),
                    expiresAt = ExpiresAt.ToUniversalTime(),
                    source = Source
                };
                var resp = await client.PostAsJsonAsync($"/api/streets/{StreetId}/risk-event", body);
                if (!resp.IsSuccessStatusCode)
                {
                    var msg = await resp.Content.ReadAsStringAsync();
                    logger.LogWarning("Erreur création risk-event {Status}: {Body}", (int)resp.StatusCode, msg);
                    TempData["ErrorMessage"] = "Erreur lors de la création de l'événement.";
                    await LoadStreetsAsync(ct);
                    EventId = id;
                    return Page();
                }
                TempData["SuccessMessage"] = "Événement créé avec succès.";
            }
            else
            {
                // Update via PUT /api/streets/{streetId}/risk-events/{eventId}
                var body = new
                {
                    title = Title,
                    description = Description,
                    riskPoints = RiskPoints,
                    eventDate = EventDate.ToUniversalTime(),
                    expiresAt = ExpiresAt.ToUniversalTime(),
                    source = Source
                };
                var resp = await client.PutAsJsonAsync($"/api/streets/{StreetId}/risk-events/{id}", body);
                if (!resp.IsSuccessStatusCode)
                {
                    var msg = await resp.Content.ReadAsStringAsync();
                    logger.LogWarning("Erreur mise à jour risk-event {Status}: {Body}", (int)resp.StatusCode, msg);
                    TempData["ErrorMessage"] = "Erreur lors de la mise à jour de l'événement.";
                    await LoadStreetsAsync(ct);
                    EventId = id;
                    return Page();
                }
                TempData["SuccessMessage"] = "Événement mis à jour.";
            }

            return RedirectToPage("/Admin/RiskEvents/Index");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur sauvegarde événement de risque");
            TempData["ErrorMessage"] = "Impossible de joindre le serveur.";
            await LoadStreetsAsync(ct);
            EventId = id;
            return Page();
        }
    }

    private async Task LoadStreetsAsync(CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            var streets = await client.GetFromJsonAsync<List<StreetItem>>("/api/streets", JsonOpts);
            Streets = streets ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de charger les rues");
        }
    }

    public class RiskEventDto
    {
        public Guid Id { get; set; }
        public Guid StreetId { get; set; }
        public string StreetName { get; set; } = "";
        public string StreetDistrict { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public int RiskPoints { get; set; }
        public DateTime EventDate { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string Source { get; set; } = "";
        public bool IsActive { get; set; }
    }

    public class StreetItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string? District { get; set; }
    }
}
