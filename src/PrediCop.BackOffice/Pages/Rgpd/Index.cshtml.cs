using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using System.Text.Json;

namespace PrediCop.BackOffice.Pages.Rgpd;

[AllowAnonymous]
public class IndexModel(IHttpClientFactory httpClientFactory, ILogger<IndexModel> logger) : PageModel
{
    [BindProperty]
    public RgpdFormInput Input { get; set; } = new();

    public bool SubmitSuccess { get; set; }
    public string? SubmitError { get; set; }
    public string? DpoEmail { get; set; }
    public string TenantSlug { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;

    public async Task OnGetAsync([FromQuery] string? tenant, CancellationToken ct = default)
    {
        // Résoudre le TenantSlug : query param ou session
        TenantSlug = tenant
            ?? HttpContext.Session.GetString("TenantSlug")
            ?? string.Empty;

        await LoadTenantInfoAsync(ct);
    }

    public async Task<IActionResult> OnPostAsync([FromQuery] string? tenant, CancellationToken ct = default)
    {
        TenantSlug = tenant
            ?? HttpContext.Session.GetString("TenantSlug")
            ?? string.Empty;

        await LoadTenantInfoAsync(ct);

        if (!ModelState.IsValid)
            return Page();

        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApiAnon");
            var payload = new
            {
                TenantSlug,
                Input.RequestType,
                Input.RequesterName,
                Input.RequesterEmail,
                Input.Description
            };

            var response = await client.PostAsJsonAsync("/api/rgpd/requests", payload, ct);

            if (response.IsSuccessStatusCode)
            {
                SubmitSuccess = true;
                Input = new RgpdFormInput(); // reset form
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                logger.LogWarning("Soumission RGPD échouée {Status}: {Body}", (int)response.StatusCode, body);

                try
                {
                    using var doc = JsonDocument.Parse(body);
                    SubmitError = doc.RootElement.TryGetProperty("title", out var titleProp)
                        ? titleProp.GetString()
                        : "Une erreur est survenue lors de l'envoi de votre demande.";
                }
                catch
                {
                    SubmitError = "Une erreur est survenue lors de l'envoi de votre demande.";
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Erreur soumission demande RGPD");
            SubmitError = "Impossible de contacter le serveur. Veuillez réessayer ultérieurement.";
        }

        return Page();
    }

    private async Task LoadTenantInfoAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(TenantSlug))
            return;

        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApiAnon");
            var tenants = await client.GetFromJsonAsync<List<TenantSummary>>("/api/auth/tenants", ct);
            var found = tenants?.FirstOrDefault(t => t.Slug == TenantSlug);
            if (found is not null)
                TenantName = found.Name;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de charger le tenant {Slug}", TenantSlug);
        }
    }

    public class RgpdFormInput
    {
        [Required(ErrorMessage = "Le nom complet est requis")]
        [MaxLength(200)]
        public string RequesterName { get; set; } = string.Empty;

        [Required(ErrorMessage = "L'adresse email est requise")]
        [EmailAddress(ErrorMessage = "Adresse email invalide")]
        [MaxLength(200)]
        public string RequesterEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "Le type de demande est requis")]
        public string RequestType { get; set; } = string.Empty;

        [Required(ErrorMessage = "La description est requise")]
        [MinLength(10, ErrorMessage = "La description doit comporter au moins 10 caractères")]
        [MaxLength(2000)]
        public string Description { get; set; } = string.Empty;
    }

    private record TenantSummary(Guid Id, string Name, string Slug);
}
