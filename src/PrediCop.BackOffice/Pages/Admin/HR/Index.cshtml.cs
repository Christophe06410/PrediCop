using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.Core.DTOs;
using PrediCop.Core.Enums;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrediCop.BackOffice.Pages.Admin.HR;

[Authorize(Roles = "Admin,Manager")]
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

    public List<LeaveResponse> Leaves { get; set; } = [];
    public List<ShiftScheduleResponse> Schedules { get; set; } = [];
    public List<AgentItem> Agents { get; set; } = [];
    public int PendingLeavesCount { get; set; }

    [BindProperty(SupportsGet = true)]
    public string WeekStart { get; set; } = GetCurrentWeekMonday();

    [BindProperty(SupportsGet = true)]
    public Guid? AgentFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string ActiveTab { get; set; } = "planning";

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("PrediCopApi");

        // Load agents
        try
        {
            var users = await client.GetFromJsonAsync<List<AgentItem>>("/api/users", JsonOpts, ct);
            Agents = users ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de charger la liste des agents.");
        }

        // Load leaves
        try
        {
            var leavesUrl = "/api/hr/leaves";
            if (AgentFilter.HasValue)
                leavesUrl += $"?agentId={AgentFilter.Value}";

            var leaves = await client.GetFromJsonAsync<List<LeaveResponse>>(leavesUrl, JsonOpts, ct);
            Leaves = leaves ?? [];
            PendingLeavesCount = Leaves.Count(l => l.Status == LeaveStatus.Pending);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de charger les congés.");
        }

        // Load schedules for the week
        try
        {
            var weekParam = Uri.EscapeDataString(WeekStart);
            var schedules = await client.GetFromJsonAsync<List<ShiftScheduleResponse>>(
                $"/api/hr/schedules?weekStart={weekParam}", JsonOpts, ct);
            Schedules = schedules ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de charger le planning.");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostApproveLeaveAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("PrediCopApi");
            var response = await client.PostAsJsonAsync($"/api/hr/leaves/{id}/approve", new { }, ct);
            if (!response.IsSuccessStatusCode)
                TempData["ErrorMessage"] = "Impossible d'approuver le congé.";
            else
                TempData["SuccessMessage"] = "Congé approuvé avec succès.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erreur lors de l'approbation du congé {Id}.", id);
            TempData["ErrorMessage"] = "Une erreur est survenue.";
        }

        return RedirectToPage(new { ActiveTab = "conges", AgentFilter, WeekStart });
    }

    public async Task<IActionResult> OnPostRejectLeaveAsync(Guid id, string rejectionReason, CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("PrediCopApi");
            var response = await client.PostAsJsonAsync(
                $"/api/hr/leaves/{id}/reject",
                new { rejectionReason },
                ct);
            if (!response.IsSuccessStatusCode)
                TempData["ErrorMessage"] = "Impossible de refuser le congé.";
            else
                TempData["SuccessMessage"] = "Congé refusé.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erreur lors du refus du congé {Id}.", id);
            TempData["ErrorMessage"] = "Une erreur est survenue.";
        }

        return RedirectToPage(new { ActiveTab = "conges", AgentFilter, WeekStart });
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

    private static string GetCurrentWeekMonday()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var daysToMonday = ((int)today.DayOfWeek - 1 + 7) % 7;
        return today.AddDays(-daysToMonday).ToString("yyyy-MM-dd");
    }

    public class AgentItem
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string BadgeNumber { get; set; } = string.Empty;
        public string FullName => $"{FirstName} {LastName}";
    }
}
