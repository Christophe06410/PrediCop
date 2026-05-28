using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.Core.DTOs;
using PrediCop.Core.Enums;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrediCop.BackOffice.Pages.Planning;

[Authorize]
public class MesCongesModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MesCongesModel> _logger;

    public MesCongesModel(IHttpClientFactory httpClientFactory, ILogger<MesCongesModel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public List<AgentLeaveBalanceResponse> Balances { get; set; } = [];
    public List<LeaveResponse> Leaves { get; set; } = [];
    public string? ModuleDisabledMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("PrediCopApi");

        try
        {
            var balances = await client.GetFromJsonAsync<List<AgentLeaveBalanceResponse>>("/api/leave/balance", JsonOpts, ct);
            Balances = balances ?? [];
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            ModuleDisabledMessage = "Le module Planning n'est pas activé pour votre organisation.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de charger les soldes de congés.");
        }

        if (ModuleDisabledMessage is null)
        {
            try
            {
                var leaves = await client.GetFromJsonAsync<List<LeaveResponse>>("/api/leave/requests", JsonOpts, ct);
                Leaves = leaves ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Impossible de charger les demandes de congés.");
            }
        }

        return Page();
    }

    public async Task<IActionResult> OnPostCreateRequestAsync(
        string leaveType, string startDate, string endDate, string? notes, CancellationToken ct)
    {
        if (!Enum.TryParse<LeaveType>(leaveType, out var parsedType) ||
            !DateOnly.TryParse(startDate, out var parsedStart) ||
            !DateOnly.TryParse(endDate, out var parsedEnd))
        {
            TempData["ErrorMessage"] = "Données invalides.";
            return RedirectToPage();
        }

        try
        {
            var client = _httpClientFactory.CreateClient("PrediCopApi");
            // AgentId is ignored server-side for self-service (forced to the connected user)
            var request = new CreateLeaveRequest(Guid.Empty, parsedType, parsedStart, parsedEnd, notes);
            var response = await client.PostAsJsonAsync("/api/leave/requests", request, JsonOpts, ct);
            if (!response.IsSuccessStatusCode)
                TempData["ErrorMessage"] = "Impossible de soumettre la demande.";
            else
                TempData["SuccessMessage"] = "Demande de congé soumise. Elle sera traitée par votre responsable.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erreur lors de la création de la demande de congé.");
            TempData["ErrorMessage"] = "Une erreur est survenue.";
        }

        return RedirectToPage();
    }

    public string GetLeaveTypeLabel(LeaveType t) => t switch
    {
        LeaveType.CongesPayes => "Congés payés",
        LeaveType.RTT => "RTT",
        LeaveType.Maladie => "Maladie",
        LeaveType.Formation => "Formation",
        LeaveType.RecupHeure => "Récup. heures",
        LeaveType.Autre => "Autre",
        _ => t.ToString()
    };

    public string GetLeaveStatusBadgeClass(LeaveStatus s) => s switch
    {
        LeaveStatus.Pending => "bg-warning text-dark",
        LeaveStatus.Approved => "bg-success",
        LeaveStatus.Rejected => "bg-danger",
        _ => "bg-secondary"
    };
}
