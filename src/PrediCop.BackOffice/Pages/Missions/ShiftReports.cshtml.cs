using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Json;
using System.Text.Json;

namespace PrediCop.BackOffice.Pages.Missions;

[Authorize(Roles = "Admin,Manager")]
public class ShiftReportsModel(
    IHttpClientFactory httpClientFactory,
    ILogger<ShiftReportsModel> logger) : PageModel
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public List<ShiftReportSummary> Reports { get; set; } = [];
    public List<VehicleSummary> Vehicles { get; set; } = [];
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    [BindProperty(SupportsGet = true)]
    public Guid? VehicleFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string DateFrom { get; set; } = DateTime.Today.AddDays(-30).ToString("yyyy-MM-dd");

    [BindProperty(SupportsGet = true)]
    public string DateTo { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public async Task OnGetAsync(CancellationToken ct)
    {
        CurrentPage = Math.Max(1, PageNumber);

        var client = httpClientFactory.CreateClient("PrediCopApi");

        // Charger la liste des véhicules pour le filtre
        try
        {
            var vehicles = await client.GetFromJsonAsync<List<VehicleSummary>>("/api/vehicles", JsonOpts, ct);
            Vehicles = vehicles ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de charger la liste des véhicules.");
        }

        // Charger les rapports
        try
        {
            var url = $"/api/shift-reports?page={CurrentPage}&pageSize={PageSize}";
            if (VehicleFilter.HasValue)
                url += $"&vehicleId={VehicleFilter.Value}";
            if (!string.IsNullOrWhiteSpace(DateFrom))
                url += $"&dateFrom={Uri.EscapeDataString(DateFrom)}";
            if (!string.IsNullOrWhiteSpace(DateTo))
                url += $"&dateTo={Uri.EscapeDataString(DateTo)}";

            var response = await client.GetAsync(url, ct);
            if (response.IsSuccessStatusCode)
            {
                if (response.Headers.TryGetValues("X-Total-Count", out var totalHeader)
                    && int.TryParse(totalHeader.FirstOrDefault(), out var total))
                    TotalCount = total;

                var reports = await response.Content.ReadFromJsonAsync<List<ShiftReportSummary>>(JsonOpts, ct);
                Reports = reports ?? [];

                if (TotalCount == 0)
                    TotalCount = Reports.Count;
            }
            else
            {
                logger.LogWarning("Erreur chargement rapports {Status}", (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Impossible de charger les rapports de vacation.");
        }
    }

    public class ShiftReportSummary
    {
        public Guid Id { get; set; }
        public Guid VehicleId { get; set; }
        public string VehicleCallSign { get; set; } = string.Empty;
        public DateTime ShiftStart { get; set; }
        public DateTime ShiftEnd { get; set; }
        public string OfficerNames { get; set; } = string.Empty;
        public int MissionCount { get; set; }
        public int CompletedMissionCount { get; set; }
        public int RefusedMissionCount { get; set; }
        public int PatrolRecordCount { get; set; }
        public double EstimatedKm { get; set; }
        public int DocumentCount { get; set; }
        public bool IsSigned { get; set; }
        public DateTime? SignedAt { get; set; }
        public DateTime CreatedAt { get; set; }

        public TimeSpan Duration => ShiftEnd - ShiftStart;
    }

    public class VehicleSummary
    {
        public Guid Id { get; set; }
        public string CallSign { get; set; } = string.Empty;
        public string LicensePlate { get; set; } = string.Empty;
    }
}
