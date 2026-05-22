using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.BackOffice.Services;
using System.Net.Http.Json;
using System.Text.Json;

namespace PrediCop.BackOffice.Pages.Missions;

[Authorize]
public class ShiftReportExportModel(
    IHttpClientFactory httpClientFactory,
    ILogger<ShiftReportExportModel> logger) : PageModel
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("PrediCopApi");
        ShiftReportDto? dto;
        try
        {
            dto = await client.GetFromJsonAsync<ShiftReportDto>($"/api/shift-reports/{id}", JsonOpts, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Impossible de charger le rapport {Id} pour l'export PDF", id);
            return NotFound();
        }

        if (dto is null) return NotFound();

        var data = new ShiftReportPdfData
        {
            VehicleCallSign = dto.VehicleCallSign,
            ShiftStart = dto.ShiftStart,
            ShiftEnd = dto.ShiftEnd,
            OfficerNames = dto.OfficerNames,
            MissionCount = dto.MissionCount,
            CompletedMissionCount = dto.CompletedMissionCount,
            RefusedMissionCount = dto.RefusedMissionCount,
            PatrolRecordCount = dto.PatrolRecordCount,
            EstimatedKm = dto.EstimatedKm,
            DocumentCount = dto.DocumentCount,
            Notes = dto.Notes,
            IsSigned = dto.IsSigned,
            SignedAt = dto.SignedAt
        };

        var pdfBytes = ShiftReportPdfGenerator.Generate(data, DateTime.UtcNow);
        var filename = $"vacation_{SanitizeFilename(dto.VehicleCallSign)}_{dto.ShiftStart:yyyyMMdd}.pdf";
        return File(pdfBytes, "application/pdf", filename);
    }

    private static string SanitizeFilename(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    private class ShiftReportDto
    {
        public Guid Id { get; set; }
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
        public string? Notes { get; set; }
        public bool IsSigned { get; set; }
        public DateTime? SignedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
