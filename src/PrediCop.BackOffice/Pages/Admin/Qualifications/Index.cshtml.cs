using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.Core.DTOs;
using PrediCop.Core.Enums;
using System.Net.Http.Json;

namespace PrediCop.BackOffice.Pages.Admin.Qualifications;

[Authorize(Roles = "Admin,Manager")]
public class IndexModel(IHttpClientFactory httpClientFactory, ILogger<IndexModel> logger) : PageModel
{
    public List<QualificationResponse> Qualifications { get; set; } = [];
    public List<AgentItem> Agents { get; set; } = [];
    public int ExpiringCount { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid? AgentFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? TypeFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool ExpiredOnly { get; set; }

    public List<(string Value, string Label)> QualificationTypes { get; } =
    [
        ("APJA", "APJA"),
        ("PorteArme", "Port d'arme"),
        ("PermisConduire", "Permis de conduire"),
        ("FormationSecours", "Formation secours"),
        ("HabilitationPrefectorale", "Habilitation préfectorale"),
        ("Autre", "Autre")
    ];

    public string GetTypeLabel(QualificationType type) => type switch
    {
        QualificationType.APJA => "APJA",
        QualificationType.PorteArme => "Port d'arme",
        QualificationType.PermisConduire => "Permis de conduire",
        QualificationType.FormationSecours => "Formation secours",
        QualificationType.HabilitationPrefectorale => "Habilitation préfectorale",
        QualificationType.Autre => "Autre",
        _ => type.ToString()
    };

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task OnGetAsync(CancellationToken ct = default)
    {
        var client = httpClientFactory.CreateClient("PrediCopApi");

        // Charger les agents
        try
        {
            var users = await client.GetFromJsonAsync<List<AgentItem>>("/api/users", JsonOpts, ct);
            Agents = users ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de charger les agents");
        }

        // Charger les habilitations avec filtres
        try
        {
            var url = "/api/qualifications?";
            if (AgentFilter.HasValue)
                url += $"agentId={AgentFilter}&";
            if (!string.IsNullOrWhiteSpace(TypeFilter))
                url += $"type={TypeFilter}&";
            if (ExpiredOnly)
                url += "expiredOnly=true&";

            var qualifications = await client.GetFromJsonAsync<List<QualificationResponse>>(url.TrimEnd('&', '?'), JsonOpts, ct);
            Qualifications = qualifications ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de charger les habilitations");
            TempData["ErrorMessage"] = "Impossible de charger les habilitations.";
        }

        // Charger le nombre d'habilitations qui expirent bientôt
        try
        {
            var expiring = await client.GetFromJsonAsync<List<QualificationResponse>>("/api/qualifications/expiring", JsonOpts, ct);
            ExpiringCount = expiring?.Count ?? 0;
        }
        catch
        {
            ExpiringCount = 0;
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApi");
            var response = await client.DeleteAsync($"/api/qualifications/{id}", ct);
            if (response.IsSuccessStatusCode)
                TempData["SuccessMessage"] = "Habilitation supprimée.";
            else
                TempData["ErrorMessage"] = "Impossible de supprimer l'habilitation.";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Erreur suppression habilitation {Id}", id);
            TempData["ErrorMessage"] = "Erreur lors de la suppression.";
        }
        return RedirectToPage();
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
