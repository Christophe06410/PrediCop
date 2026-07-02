using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PrediCop.BackOffice.Pages.Admin.ReportTemplates;

[Authorize(Roles = "Admin,SuperAdmin")]
public class EditModel(IHttpClientFactory httpClientFactory, ILogger<EditModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Type { get; set; } = string.Empty;

    public string TypeLabel { get; set; } = string.Empty;

    [BindProperty]
    public TemplateInput Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("PrediCopApi");
        var tpl = await client.GetFromJsonAsync<TemplateDto>($"/api/report-templates/{Type}", ct);
        if (tpl is null) return NotFound();

        TypeLabel     = tpl.TypeLabel;
        Input.DefaultTitle = tpl.DefaultTitle;
        Input.Body    = tpl.Body;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return Page();

        var payload = new { defaultTitle = Input.DefaultTitle, body = Input.Body };

        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            var response = await client.PutAsJsonAsync($"/api/report-templates/{Type}", payload, ct);

            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = "Masque mis à jour.";
                return RedirectToPage("Index");
            }

            var err = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Erreur maj masque : {Status} — {Body}", (int)response.StatusCode, err);
            ModelState.AddModelError(string.Empty, $"Erreur serveur ({(int)response.StatusCode}).");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Impossible de mettre à jour le masque {Type}", Type);
            ModelState.AddModelError(string.Empty, "Impossible de joindre le serveur.");
        }

        var tpl = await httpClientFactory.CreateClient("PrediCopApi")
            .GetFromJsonAsync<TemplateDto>($"/api/report-templates/{Type}", ct);
        TypeLabel = tpl?.TypeLabel ?? Type;
        return Page();
    }
}

public class TemplateInput
{
    [Required(ErrorMessage = "Le titre suggéré est obligatoire.")]
    [MaxLength(500)]
    public string DefaultTitle { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le corps du masque est obligatoire.")]
    public string Body { get; set; } = string.Empty;
}

public class TemplateDto
{
    public string Type { get; set; } = string.Empty;
    public string TypeLabel { get; set; } = string.Empty;
    public string DefaultTitle { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}
