using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.Core.DTOs;
using PrediCop.Core.Enums;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Headers;

namespace PrediCop.BackOffice.Pages.Admin.Planning;

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
    public List<VehicleItem> Vehicles { get; set; } = [];
    public int PendingLeavesCount { get; set; }
    public List<LeaveEntitlementItem> Entitlements { get; set; } = [];
    public CsvImportResult? LastImportResult { get; set; }

    /// <summary>Occupation des véhicules pour chaque jour de la semaine affichée. Clé = "yyyy-MM-dd".</summary>
    public Dictionary<string, List<VehicleOccupancyItem>> WeekOccupancy { get; set; } = [];

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

        // Load vehicles
        try
        {
            var vehicles = await client.GetFromJsonAsync<List<VehicleItem>>("/api/vehicles", JsonOpts, ct);
            Vehicles = vehicles ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de charger les véhicules.");
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

        // Load entitlements
        try
        {
            var entitlements = await client.GetFromJsonAsync<List<LeaveEntitlementItem>>("/api/hr/entitlements", JsonOpts, ct);
            Entitlements = entitlements ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de charger les droits à congés.");
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

        // Load vehicle occupancy for each day of the displayed week
        try
        {
            if (DateOnly.TryParse(WeekStart, out var weekStartDate))
            {
                var occupancyTasks = Enumerable.Range(0, 7)
                    .Select(i => weekStartDate.AddDays(i))
                    .Select(async day =>
                    {
                        var dayStr = day.ToString("yyyy-MM-dd");
                        try
                        {
                            var occ = await client.GetFromJsonAsync<List<VehicleOccupancyItem>>(
                                $"/api/hr/vehicles/occupancy?date={Uri.EscapeDataString(dayStr)}", JsonOpts, ct);
                            return (dayStr, occ ?? []);
                        }
                        catch
                        {
                            return (dayStr, new List<VehicleOccupancyItem>());
                        }
                    });

                var results = await Task.WhenAll(occupancyTasks);
                WeekOccupancy = results.ToDictionary(r => r.Item1, r => r.Item2);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de charger l'occupation des véhicules.");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostUpsertShiftAsync(
        Guid agentId, string date, string shiftStart, string shiftEnd,
        Guid? vehicleId, bool isPublished, string? notes, CancellationToken ct)
    {
        if (!DateOnly.TryParse(date, out var parsedDate) ||
            !TimeOnly.TryParse(shiftStart, out var parsedStart) ||
            !TimeOnly.TryParse(shiftEnd, out var parsedEnd))
        {
            TempData["ErrorMessage"] = "Dates ou heures invalides.";
            return RedirectToPage(new { ActiveTab = "planning", WeekStart, AgentFilter });
        }

        try
        {
            var client = _httpClientFactory.CreateClient("PrediCopApi");
            var request = new UpsertShiftRequest(agentId, vehicleId, parsedDate, parsedStart, parsedEnd, isPublished, notes);
            var response = await client.PostAsJsonAsync("/api/hr/schedules", request, ct);
            if (!response.IsSuccessStatusCode)
                TempData["ErrorMessage"] = "Impossible d'enregistrer le créneau.";
            else
                TempData["SuccessMessage"] = "Créneau enregistré.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erreur lors de l'upsert du créneau.");
            TempData["ErrorMessage"] = "Une erreur est survenue.";
        }

        return RedirectToPage(new { ActiveTab = "planning", WeekStart = GetWeekMonday(date), AgentFilter });
    }

    public async Task<IActionResult> OnPostDeleteShiftAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("PrediCopApi");
            var response = await client.DeleteAsync($"/api/hr/schedules/{id}", ct);
            if (!response.IsSuccessStatusCode)
                TempData["ErrorMessage"] = "Impossible de supprimer le créneau.";
            else
                TempData["SuccessMessage"] = "Créneau supprimé.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erreur lors de la suppression du créneau {Id}.", id);
            TempData["ErrorMessage"] = "Une erreur est survenue.";
        }

        return RedirectToPage(new { ActiveTab = "planning", WeekStart, AgentFilter });
    }

    public async Task<IActionResult> OnPostCreateLeaveAsync(
        Guid agentId, string leaveType, string startDate, string endDate, string? notes, CancellationToken ct)
    {
        if (!Enum.TryParse<LeaveType>(leaveType, out var parsedType) ||
            !DateOnly.TryParse(startDate, out var parsedStart) ||
            !DateOnly.TryParse(endDate, out var parsedEnd))
        {
            TempData["ErrorMessage"] = "Données invalides.";
            return RedirectToPage(new { ActiveTab = "conges", AgentFilter, WeekStart });
        }

        try
        {
            var client = _httpClientFactory.CreateClient("PrediCopApi");
            var request = new CreateLeaveRequest(agentId, parsedType, parsedStart, parsedEnd, notes);
            var response = await client.PostAsJsonAsync("/api/hr/leaves", request, ct);
            if (!response.IsSuccessStatusCode)
                TempData["ErrorMessage"] = "Impossible de créer la demande de congé.";
            else
                TempData["SuccessMessage"] = "Demande de congé créée.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erreur lors de la création du congé.");
            TempData["ErrorMessage"] = "Une erreur est survenue.";
        }

        return RedirectToPage(new { ActiveTab = "conges", AgentFilter, WeekStart });
    }

    public async Task<IActionResult> OnPostCreateEntitlementAsync(
        Guid agentId, string leaveType, decimal totalDays, string validFrom, string validTo, CancellationToken ct)
    {
        if (!Enum.TryParse<LeaveType>(leaveType, out var parsedType) ||
            !DateOnly.TryParse(validFrom, out var parsedFrom) ||
            !DateOnly.TryParse(validTo, out var parsedTo))
        {
            TempData["ErrorMessage"] = "Données invalides.";
            return RedirectToPage(new { ActiveTab = "conges", AgentFilter, WeekStart });
        }

        try
        {
            var client = _httpClientFactory.CreateClient("PrediCopApi");
            var request = new CreateLeaveEntitlementRequest(agentId, parsedType, totalDays, parsedFrom, parsedTo);
            var response = await client.PostAsJsonAsync("/api/hr/entitlements", request, ct);
            if (!response.IsSuccessStatusCode)
                TempData["ErrorMessage"] = "Impossible de créer le droit.";
            else
                TempData["SuccessMessage"] = "Droit à congés attribué.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erreur lors de la création du droit.");
            TempData["ErrorMessage"] = "Une erreur est survenue.";
        }

        return RedirectToPage(new { ActiveTab = "conges", AgentFilter, WeekStart });
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

    public async Task<IActionResult> OnPostImportCsvAsync(IFormFile? csvFile, CancellationToken ct)
    {
        if (csvFile is null || csvFile.Length == 0)
        {
            TempData["ErrorMessage"] = "Veuillez sélectionner un fichier CSV.";
            return RedirectToPage(new { ActiveTab = "planning", WeekStart });
        }

        try
        {
            var client = _httpClientFactory.CreateClient("PrediCopApi");

            using var content = new MultipartFormDataContent();
            var fileContent = new StreamContent(csvFile.OpenReadStream());
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
            content.Add(fileContent, "file", csvFile.FileName);

            var response = await client.PostAsync("/api/hr/schedules/import-csv", content, ct);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<CsvImportResult>(JsonOpts, ct);
                if (result is not null)
                {
                    var msg = $"{result.Imported} créneau(x) importé(s), {result.Skipped} ligne(s) ignorée(s).";
                    if (result.Errors.Count > 0)
                        msg += $" {result.Errors.Count} erreur(s) — voir les détails ci-dessous.";

                    TempData["SuccessMessage"] = msg;
                    TempData["ImportErrors"] = JsonSerializer.Serialize(result.Errors);
                }
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                TempData["ErrorMessage"] = $"Erreur lors de l'import : {response.StatusCode}. {body}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erreur lors de l'import CSV planning.");
            TempData["ErrorMessage"] = "Une erreur est survenue lors de l'import.";
        }

        return RedirectToPage(new { ActiveTab = "planning", WeekStart });
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

    /// <summary>Retourne les droits d'un agent pour un type donné (peut y avoir plusieurs périodes).</summary>
    public IEnumerable<LeaveEntitlementItem> GetEntitlements(Guid agentId, LeaveType type)
        => Entitlements.Where(e => e.AgentId == agentId && e.Type == type);

    /// <summary>Retourne le solde restant d'un agent pour un type (somme de toutes les périodes actives).</summary>
    public string GetBalanceLabel(Guid agentId, LeaveType type)
    {
        if (type == LeaveType.Maladie) return "Illimité";
        var items = GetEntitlements(agentId, type).ToList();
        if (!items.Any()) return "—";
        var remaining = items.Sum(e => e.RemainingDays);
        var total = items.Sum(e => e.TotalDays);
        return $"{remaining:0.#}/{total:0.#} j";
    }

    private static string GetCurrentWeekMonday()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var daysToMonday = ((int)today.DayOfWeek - 1 + 7) % 7;
        return today.AddDays(-daysToMonday).ToString("yyyy-MM-dd");
    }

    private static string GetWeekMonday(string dateStr)
    {
        if (!DateOnly.TryParse(dateStr, out var date)) return GetCurrentWeekMonday();
        var offset = ((int)date.DayOfWeek - 1 + 7) % 7;
        return date.AddDays(-offset).ToString("yyyy-MM-dd");
    }

    public class AgentItem
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string BadgeNumber { get; set; } = string.Empty;
        public string FullName => $"{FirstName} {LastName}";
    }

    public class VehicleItem
    {
        public Guid Id { get; set; }
        public string CallSign { get; set; } = string.Empty;
        public string LicensePlate { get; set; } = string.Empty;
        public int Capacity { get; set; } = 2;
    }

    public class VehicleOccupancyItem
    {
        public Guid VehicleId { get; set; }
        public string CallSign { get; set; } = string.Empty;
        public int Capacity { get; set; }
        public int Count { get; set; }
        public List<string> AgentNames { get; set; } = [];
    }

    public class LeaveEntitlementItem
    {
        public Guid Id { get; set; }
        public Guid AgentId { get; set; }
        public string AgentFullName { get; set; } = string.Empty;
        public LeaveType Type { get; set; }
        public decimal TotalDays { get; set; }
        public decimal UsedDays { get; set; }
        public decimal RemainingDays { get; set; }
        public DateOnly ValidFrom { get; set; }
        public DateOnly ValidTo { get; set; }
    }
}
