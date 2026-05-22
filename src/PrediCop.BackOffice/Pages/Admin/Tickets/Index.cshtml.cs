using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.Core.DTOs;
using PrediCop.Core.Enums;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrediCop.BackOffice.Pages.Admin.Tickets;

[Authorize(Roles = "Admin,Manager,Operator")]
public class IndexModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(IHttpClientFactory httpClientFactory, ILogger<IndexModel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public List<ElectronicTicketResponse> Tickets { get; set; } = [];
    public TicketStatsResponse? Stats { get; set; }
    public List<AgentItem> Agents { get; set; } = [];
    public int TotalCount { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid? AgentFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? StatusFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? PlateSearch { get; set; }

    [BindProperty(SupportsGet = true)]
    public string DateFrom { get; set; } = DateTime.Today.AddDays(-30).ToString("yyyy-MM-dd");

    [BindProperty(SupportsGet = true)]
    public string DateTo { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("PrediCopApi");

        // Charger les agents
        try
        {
            var users = await client.GetFromJsonAsync<List<AgentItem>>("/api/users", JsonOpts, ct);
            Agents = users ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de charger la liste des agents.");
        }

        // Charger les statistiques
        try
        {
            var statsUrl = $"/api/tickets/stats?dateFrom={DateFrom}&dateTo={DateTo}";
            if (AgentFilter.HasValue)
                statsUrl += $"&agentId={AgentFilter.Value}";

            Stats = await client.GetFromJsonAsync<TicketStatsResponse>(statsUrl, JsonOpts, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de charger les statistiques.");
        }

        // Charger les tickets avec filtres
        try
        {
            var ticketsUrl = $"/api/tickets?dateFrom={DateFrom}&dateTo={DateTo}";
            if (AgentFilter.HasValue)
                ticketsUrl += $"&agentId={AgentFilter.Value}";
            if (!string.IsNullOrWhiteSpace(StatusFilter))
                ticketsUrl += $"&status={Uri.EscapeDataString(StatusFilter)}";
            if (!string.IsNullOrWhiteSpace(PlateSearch))
                ticketsUrl += $"&plate={Uri.EscapeDataString(PlateSearch)}";

            var tickets = await client.GetFromJsonAsync<List<ElectronicTicketResponse>>(ticketsUrl, JsonOpts, ct);
            Tickets = tickets ?? [];
            TotalCount = Tickets.Count;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de charger les PV.");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostPayAsync(Guid id)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("PrediCopApi");
            var body = new { Status = "Paid", Notes = (string?)null };
            await client.PutAsJsonAsync($"/api/tickets/{id}/status", body);
            TempData["SuccessMessage"] = "PV marqué comme payé.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de marquer le PV {Id} comme payé.", id);
            TempData["ErrorMessage"] = "Impossible de mettre à jour le statut.";
        }
        return RedirectToPage(new { AgentFilter, StatusFilter, PlateSearch, DateFrom, DateTo });
    }

    public async Task<IActionResult> OnPostContestAsync(Guid id, string? notes)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("PrediCopApi");
            var body = new { Status = "Contested", Notes = notes };
            await client.PutAsJsonAsync($"/api/tickets/{id}/status", body);
            TempData["SuccessMessage"] = "PV marqué comme contesté.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de marquer le PV {Id} comme contesté.", id);
            TempData["ErrorMessage"] = "Impossible de mettre à jour le statut.";
        }
        return RedirectToPage(new { AgentFilter, StatusFilter, PlateSearch, DateFrom, DateTo });
    }

    public async Task<IActionResult> OnPostCancelAsync(Guid id, string reason)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("PrediCopApi");
            var body = new { Status = "Cancelled", Notes = reason };
            await client.PutAsJsonAsync($"/api/tickets/{id}/status", body);
            TempData["SuccessMessage"] = "PV annulé avec succès.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible d'annuler le PV {Id}.", id);
            TempData["ErrorMessage"] = "Impossible d'annuler le PV.";
        }

        return RedirectToPage(new
        {
            AgentFilter,
            StatusFilter,
            PlateSearch,
            DateFrom,
            DateTo
        });
    }

    // ── Labels & badge helpers ────────────────────────────────────────────────

    public static string GetInfractionLabel(InfractionType type) => type switch
    {
        InfractionType.StationnementInterdit      => "Stationnement interdit",
        InfractionType.StationnementGenant        => "Stationnement gênant",
        InfractionType.StationnementDangereux     => "Stationnement dangereux",
        InfractionType.StationnementHandicape     => "Stat. handicapé",
        InfractionType.VitesseExcessive           => "Excès de vitesse",
        InfractionType.FeuRouge                   => "Feu rouge",
        InfractionType.NonRespectPriorite         => "Non-respect priorité",
        InfractionType.PortableAuVolant           => "Portable au volant",
        InfractionType.CeintureSecurity           => "Ceinture sécurité",
        InfractionType.DefautAssurance            => "Défaut assurance",
        InfractionType.DefautControleTechnique    => "Défaut contrôle technique",
        InfractionType.NuisanceSonore             => "Nuisance sonore",
        InfractionType.DegradationEspacePublic    => "Dégradation espace public",
        InfractionType.Autre                      => "Autre",
        _                                         => type.ToString()
    };

    public static string GetStatusBadgeClass(TicketStatus status) => status switch
    {
        TicketStatus.Issued    => "bg-primary",
        TicketStatus.Paid      => "bg-success",
        TicketStatus.Contested => "bg-warning text-dark",
        TicketStatus.Cancelled => "bg-secondary",
        _                      => "bg-light text-dark"
    };

    public static string GetStatusLabel(TicketStatus status) => status switch
    {
        TicketStatus.Issued    => "Émis",
        TicketStatus.Paid      => "Payé",
        TicketStatus.Contested => "Contesté",
        TicketStatus.Cancelled => "Annulé",
        _                      => status.ToString()
    };

    public class AgentItem
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string BadgeNumber { get; set; } = string.Empty;
    }
}
