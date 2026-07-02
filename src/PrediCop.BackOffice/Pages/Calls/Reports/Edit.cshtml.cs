using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.BackOffice.Models;

namespace PrediCop.BackOffice.Pages.Calls.Reports;

[Authorize]
public class EditModel(IHttpClientFactory httpClientFactory, ILogger<EditModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public Guid CallId { get; set; }
    public string CallReference { get; set; } = string.Empty;
    public string TemplatesJson { get; set; } = "{}";

    [BindProperty]
    public EditReportInput Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("PrediCopApi");
        var report = await client.GetFromJsonAsync<ReportDto>($"/api/call-reports/{Id}", ct);
        if (report is null) return NotFound();

        CallId = report.CallId;

        var callTask      = client.GetFromJsonAsync<CallDto>($"/api/calls/{CallId}", ct);
        var templatesTask = client.GetFromJsonAsync<List<TemplateEntry>>("/api/report-templates", ct);
        await Task.WhenAll(callTask, templatesTask);
        var call      = callTask.Result;
        var templates = templatesTask.Result;
        CallReference = call?.Reference ?? string.Empty;
        TemplatesJson = System.Text.Json.JsonSerializer.Serialize(
            (templates ?? []).ToDictionary(t => t.Type, t => new { t.DefaultTitle, t.Body }),
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });

        Input = new EditReportInput
        {
            Type                = report.Type,
            Title               = report.Title,
            Body                = report.Body,
            IsDraft             = report.IsDraft,
            RecipientMayor      = (report.Recipients & 1)  != 0,
            RecipientProsecutor = (report.Recipients & 2)  != 0,
            RecipientPrefecture = (report.Recipients & 4)  != 0,
            RecipientHierarchy  = (report.Recipients & 8)  != 0,
            RecipientDepartment = (report.Recipients & 16) != 0,
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return Page();

        int recipients = 0;
        if (Input.RecipientMayor)      recipients |= 1;
        if (Input.RecipientProsecutor) recipients |= 2;
        if (Input.RecipientPrefecture) recipients |= 4;
        if (Input.RecipientHierarchy)  recipients |= 8;
        if (Input.RecipientDepartment) recipients |= 16;

        var payload = new
        {
            type       = Input.Type,
            title      = Input.Title,
            body       = Input.Body,
            recipients,
            isDraft    = Input.IsDraft,
        };

        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            var response = await client.PutAsJsonAsync($"/api/call-reports/{Id}", payload, ct);

            if (response.IsSuccessStatusCode)
            {
                var updated = await response.Content.ReadFromJsonAsync<ReportDto>(cancellationToken: ct);
                TempData["SuccessMessage"] = "Rapport mis à jour.";
                return RedirectToPage("/Calls/Details", new { id = updated!.CallId });
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Erreur maj rapport : {Status} — {Body}", (int)response.StatusCode, body);
            ModelState.AddModelError(string.Empty, $"Erreur serveur ({(int)response.StatusCode}).");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Impossible de mettre à jour le rapport {Id}", Id);
            ModelState.AddModelError(string.Empty, "Impossible de joindre le serveur.");
        }

        return Page();
    }
}

public class EditReportInput
{
    [Required]
    public string Type { get; set; } = "Information";

    [Required(ErrorMessage = "Le titre est obligatoire.")]
    [MaxLength(500)]
    [Display(Name = "Titre")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le corps du rapport est obligatoire.")]
    [Display(Name = "Corps du rapport")]
    public string Body { get; set; } = string.Empty;

    public bool RecipientMayor { get; set; }
    public bool RecipientProsecutor { get; set; }
    public bool RecipientPrefecture { get; set; }
    public bool RecipientHierarchy { get; set; }
    public bool RecipientDepartment { get; set; }

    [Display(Name = "Brouillon")]
    public bool IsDraft { get; set; } = true;
}
