using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.Core.Enums;
using System.Net.Http.Json;
using PrediCop.BackOffice.Helpers;
using System.Text.Json;

namespace PrediCop.BackOffice.Pages.Admin.RiskEvents;

[Authorize(Roles = "Admin,Manager")]
public class EditModel(IHttpClientFactory httpClientFactory, ILogger<EditModel> logger) : PageModel
{
    private static readonly JsonSerializerOptions JsonOpts =
        ApiJsonOptions.Default;

    [BindProperty] public Guid StreetId { get; set; }
    [BindProperty] public string StreetName { get; set; } = "";
    [BindProperty] public string Title { get; set; } = "";
    [BindProperty] public string Description { get; set; } = "";
    [BindProperty] public int RiskPoints { get; set; } = 10;
    [BindProperty] public DateTime EventDate { get; set; } = DateTime.Now;
    [BindProperty] public DateTime ExpiresAt { get; set; } = DateTime.Now.AddDays(1);
    [BindProperty] public string Source { get; set; } = "";
    [BindProperty] public RecurrenceType RecurrenceType { get; set; } = RecurrenceType.None;
    [BindProperty] public DateTime? RecurrenceEndDate { get; set; }

    public Guid? EventId { get; set; }

    public async Task OnGetAsync(Guid? id, CancellationToken ct)
    {
        EventId = id;

        if (id == null) return;

        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            var events = await client.GetFromJsonAsync<List<RiskEventDto>>("/api/streets/risk-events", JsonOpts, ct);
            var ev = events?.FirstOrDefault(e => e.Id == id);
            if (ev == null)
            {
                TempData["ErrorMessage"] = "Événement introuvable.";
                return;
            }

            StreetId = ev.StreetId;
            StreetName = ev.StreetName + (string.IsNullOrEmpty(ev.StreetDistrict) ? "" : $" — {ev.StreetDistrict}");
            Title = ev.Title;
            Description = ev.Description;
            RiskPoints = ev.RiskPoints;
            EventDate = ev.EventDate.ToLocalTime();
            ExpiresAt = ev.ExpiresAt.ToLocalTime();
            Source = ev.Source;
            RecurrenceType = ev.RecurrenceType;
            RecurrenceEndDate = ev.RecurrenceEndDate?.ToLocalTime();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de charger l'événement {Id}", id);
            TempData["ErrorMessage"] = "Impossible de charger l'événement.";
        }
    }

    public async Task<IActionResult> OnGetSearchStreetsAsync(string? q, CancellationToken ct = default)
    {
        var client = httpClientFactory.CreateClient("PrediCopApi");
        try
        {
            var url = string.IsNullOrWhiteSpace(q)
                ? "/api/streets/paged?pageSize=10&sort=name-asc"
                : $"/api/streets/paged?search={Uri.EscapeDataString(q)}&pageSize=10&sort=name-asc";
            var result = await client.GetFromJsonAsync<PagedResult>(url, JsonOpts, ct);
            return new JsonResult(result?.Streets ?? []);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Street search failed");
            return new JsonResult(Array.Empty<object>());
        }
    }

    public async Task<IActionResult> OnPostAsync(Guid? id, CancellationToken ct)
    {
        if (StreetId == Guid.Empty)
        {
            TempData["ErrorMessage"] = "Veuillez sélectionner une rue.";
            EventId = id;
            return Page();
        }

        var client = httpClientFactory.CreateClient("PrediCopApi");

        try
        {
            var body = new
            {
                title = Title,
                description = Description,
                riskPoints = RiskPoints,
                eventDate = EventDate.ToUniversalTime(),
                expiresAt = ExpiresAt.ToUniversalTime(),
                source = Source,
                recurrenceType = (int)RecurrenceType,
                recurrenceEndDate = RecurrenceEndDate?.ToUniversalTime()
            };

            HttpResponseMessage resp;
            if (id == null)
            {
                resp = await client.PostAsJsonAsync($"/api/streets/{StreetId}/risk-event", body, ct);
            }
            else
            {
                resp = await client.PutAsJsonAsync($"/api/streets/{StreetId}/risk-events/{id}", body, ct);
            }

            if (!resp.IsSuccessStatusCode)
            {
                var msg = await resp.Content.ReadAsStringAsync(ct);
                logger.LogWarning("Erreur {Op} risk-event {Status}: {Body}",
                    id == null ? "création" : "mise à jour", (int)resp.StatusCode, msg);
                TempData["ErrorMessage"] = id == null
                    ? $"Erreur lors de la création de l'événement (HTTP {(int)resp.StatusCode}): {msg}"
                    : $"Erreur lors de la mise à jour de l'événement (HTTP {(int)resp.StatusCode}): {msg}";
                EventId = id;
                return Page();
            }

            TempData["SuccessMessage"] = id == null ? "Événement créé avec succès." : "Événement mis à jour.";
            return RedirectToPage("/Admin/RiskEvents/Index");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur sauvegarde événement de risque");
            TempData["ErrorMessage"] = "Impossible de joindre le serveur.";
            EventId = id;
            return Page();
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
        public RecurrenceType RecurrenceType { get; set; }
        public DateTime? RecurrenceEndDate { get; set; }
    }

    private class PagedResult
    {
        public List<StreetSearchItem> Streets { get; set; } = [];
    }

    public class StreetSearchItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string? District { get; set; }
    }
}
