using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.Core.DTOs;
using PrediCop.Core.Enums;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrediCop.BackOffice.Pages.Admin.Tickets;

[Authorize(Roles = "Admin,Manager,Operator")]
public class CreateModel(IHttpClientFactory httpClientFactory, ILogger<CreateModel> logger) : PageModel
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [BindProperty(SupportsGet = true)]
    public Guid? MissionId { get; set; }

    public string? MissionReference { get; set; }

    [BindProperty]
    public CreateTicketInputModel Input { get; set; } = new();

    public List<AgentItem> Agents { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        await LoadAgentsAsync(ct);

        if (MissionId.HasValue)
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            try
            {
                var mission = await client.GetFromJsonAsync<MissionSummary>(
                    $"/api/missions/{MissionId.Value}", JsonOpts, ct);
                MissionReference = mission?.Reference;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Impossible de charger la mission {Id}", MissionId.Value);
            }
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await LoadAgentsAsync(ct);
            return Page();
        }

        var client = httpClientFactory.CreateClient("PrediCopApi");

        var request = new CreateTicketRequest(
            Input.IssuedById,
            Input.IssuedAtAddress,
            null,
            null,
            Input.PlateNumber,
            string.IsNullOrWhiteSpace(Input.VehicleMake) ? null : Input.VehicleMake,
            string.IsNullOrWhiteSpace(Input.VehicleModel) ? null : Input.VehicleModel,
            string.IsNullOrWhiteSpace(Input.VehicleColor) ? null : Input.VehicleColor,
            Input.InfractionType,
            string.IsNullOrWhiteSpace(Input.ArticleCode) ? null : Input.ArticleCode,
            Input.FineAmount,
            string.IsNullOrWhiteSpace(Input.Notes) ? null : Input.Notes,
            MissionId
        );

        try
        {
            var response = await client.PostAsJsonAsync("/api/tickets", request, JsonOpts, ct);
            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync(ct);
                logger.LogWarning("Erreur création PV : {Status} — {Detail}", (int)response.StatusCode, detail);
                ModelState.AddModelError(string.Empty, $"Erreur serveur ({(int)response.StatusCode}).");
                await LoadAgentsAsync(ct);
                return Page();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Impossible de créer le PV");
            ModelState.AddModelError(string.Empty, "Erreur de connexion à l'API.");
            await LoadAgentsAsync(ct);
            return Page();
        }

        TempData["SuccessMessage"] = "Procès-verbal créé avec succès.";

        if (MissionId.HasValue)
            return RedirectToPage("/Missions/Details", new { id = MissionId.Value });

        return RedirectToPage("/Admin/Tickets/Index");
    }

    private async Task LoadAgentsAsync(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("PrediCopApi");
        try
        {
            var users = await client.GetFromJsonAsync<List<AgentItem>>("/api/users", JsonOpts, ct);
            Agents = users ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de charger la liste des agents.");
        }
    }

    public class CreateTicketInputModel
    {
        [Required(ErrorMessage = "L'agent est obligatoire.")]
        public Guid IssuedById { get; set; }

        [Required(ErrorMessage = "L'adresse est obligatoire."), MaxLength(500)]
        [Display(Name = "Adresse de l'infraction")]
        public string IssuedAtAddress { get; set; } = string.Empty;

        [Required(ErrorMessage = "La plaque est obligatoire."), MaxLength(20)]
        [Display(Name = "Immatriculation")]
        public string PlateNumber { get; set; } = string.Empty;

        [MaxLength(100)]
        [Display(Name = "Marque")]
        public string? VehicleMake { get; set; }

        [MaxLength(100)]
        [Display(Name = "Modèle")]
        public string? VehicleModel { get; set; }

        [MaxLength(50)]
        [Display(Name = "Couleur")]
        public string? VehicleColor { get; set; }

        [Display(Name = "Type d'infraction")]
        public InfractionType InfractionType { get; set; } = InfractionType.StationnementInterdit;

        [MaxLength(50)]
        [Display(Name = "Article réglementaire")]
        public string? ArticleCode { get; set; }

        [Required(ErrorMessage = "Le montant est obligatoire.")]
        [Range(0.01, 9999.99, ErrorMessage = "Le montant doit être entre 0.01 et 9999.99 €.")]
        [Display(Name = "Montant de l'amende (€)")]
        public decimal FineAmount { get; set; } = 35m;

        [MaxLength(2000)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }
    }

    public class AgentItem
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string BadgeNumber { get; set; } = string.Empty;
    }

    private class MissionSummary
    {
        public Guid Id { get; set; }
        public string Reference { get; set; } = string.Empty;
    }
}
