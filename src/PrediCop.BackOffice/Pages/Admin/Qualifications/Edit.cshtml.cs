using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.Core.DTOs;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;

namespace PrediCop.BackOffice.Pages.Admin.Qualifications;

[Authorize(Roles = "Admin,Manager")]
public class EditModel(IHttpClientFactory httpClientFactory, ILogger<EditModel> logger) : PageModel
{
    public bool IsEdit => Input.Id != Guid.Empty;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<AgentItem> Agents { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(Guid? id, CancellationToken ct = default)
    {
        await LoadAgentsAsync(ct);

        if (id.HasValue && id.Value != Guid.Empty)
        {
            try
            {
                var client = httpClientFactory.CreateClient("PrediCopApi");
                var qualifications = await client.GetFromJsonAsync<List<QualificationResponse>>(
                    $"/api/qualifications?agentId=00000000-0000-0000-0000-000000000000", ct);

                // Fetch by listing all and filtering — API doesn't have GET by id, so use list
                var allQuals = await client.GetFromJsonAsync<List<QualificationResponse>>("/api/qualifications", ct);
                var q = allQuals?.FirstOrDefault(x => x.Id == id.Value);

                if (q is not null)
                {
                    Input = new InputModel
                    {
                        Id = q.Id,
                        AgentId = q.AgentId,
                        Type = q.Type.ToString(),
                        Reference = q.Reference,
                        IssuingAuthority = q.IssuingAuthority,
                        IssuedAt = q.IssuedAt,
                        ExpiresAt = q.ExpiresAt,
                        Notes = q.Notes
                    };
                }
                else
                {
                    TempData["ErrorMessage"] = "Habilitation introuvable.";
                    return RedirectToPage("/Admin/Qualifications/Index");
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Impossible de charger l'habilitation {Id}", id);
                TempData["ErrorMessage"] = "Impossible de charger l'habilitation.";
                return RedirectToPage("/Admin/Qualifications/Index");
            }
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            await LoadAgentsAsync(ct);
            return Page();
        }

        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");

            if (IsEdit)
            {
                var request = new UpdateQualificationRequest(
                    Enum.Parse<PrediCop.Core.Enums.QualificationType>(Input.Type),
                    Input.Reference,
                    Input.IssuingAuthority,
                    Input.IssuedAt,
                    Input.ExpiresAt,
                    Input.Notes
                );

                var response = await client.PutAsJsonAsync($"/api/qualifications/{Input.Id}", request, ct);
                if (response.IsSuccessStatusCode)
                    TempData["SuccessMessage"] = "Habilitation mise à jour avec succès.";
                else
                {
                    var body = await response.Content.ReadAsStringAsync(ct);
                    logger.LogWarning("Mise à jour habilitation échouée {Status}: {Body}", (int)response.StatusCode, body);
                    TempData["ErrorMessage"] = "Impossible de mettre à jour l'habilitation.";
                    await LoadAgentsAsync(ct);
                    return Page();
                }
            }
            else
            {
                var request = new CreateQualificationRequest(
                    Input.AgentId,
                    Enum.Parse<PrediCop.Core.Enums.QualificationType>(Input.Type),
                    Input.Reference,
                    Input.IssuingAuthority,
                    Input.IssuedAt,
                    Input.ExpiresAt,
                    Input.Notes
                );

                var response = await client.PostAsJsonAsync("/api/qualifications", request, ct);
                if (response.IsSuccessStatusCode)
                    TempData["SuccessMessage"] = "Habilitation créée avec succès.";
                else
                {
                    var body = await response.Content.ReadAsStringAsync(ct);
                    logger.LogWarning("Création habilitation échouée {Status}: {Body}", (int)response.StatusCode, body);
                    TempData["ErrorMessage"] = "Impossible de créer l'habilitation.";
                    await LoadAgentsAsync(ct);
                    return Page();
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Erreur lors de la sauvegarde de l'habilitation");
            TempData["ErrorMessage"] = "Erreur de communication avec le serveur.";
            await LoadAgentsAsync(ct);
            return Page();
        }

        return RedirectToPage("/Admin/Qualifications/Index");
    }

    private async Task LoadAgentsAsync(CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            var users = await client.GetFromJsonAsync<List<AgentItem>>("/api/users", ct);
            Agents = users ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de charger les agents");
            Agents = [];
        }
    }

    public class InputModel
    {
        public Guid Id { get; set; }

        [Required(ErrorMessage = "L'agent est requis")]
        public Guid AgentId { get; set; }

        [Required(ErrorMessage = "Le type est requis")]
        public string Type { get; set; } = string.Empty;

        [Required(ErrorMessage = "La référence est requise")]
        public string Reference { get; set; } = string.Empty;

        [Required(ErrorMessage = "L'autorité émettrice est requise")]
        public string IssuingAuthority { get; set; } = string.Empty;

        [Required(ErrorMessage = "La date d'émission est requise")]
        public DateTime IssuedAt { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "La date d'expiration est requise")]
        public DateTime ExpiresAt { get; set; } = DateTime.Today.AddYears(1);

        public string? Notes { get; set; }
    }

    public class AgentItem
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string BadgeNumber { get; set; } = "";
        public string FullName => $"{FirstName} {LastName}";
    }
}
