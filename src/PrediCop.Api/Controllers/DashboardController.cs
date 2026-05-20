using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrediCop.Core.DTOs;
using PrediCop.Core.Enums;
using PrediCop.Infrastructure.Data;

namespace PrediCop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController(AppDbContext db) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirst("tenantId")!.Value);

    [HttpGet]
    public async Task<IActionResult> GetCombined(CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;

        var callsToday = await db.Calls
            .CountAsync(c => c.TenantId == TenantId && c.ReceivedAt.Date == today, ct);

        var activeMissions = await db.Missions
            .CountAsync(m => m.TenantId == TenantId
                && (m.Status == MissionStatus.Pending || m.Status == MissionStatus.Proposed
                    || m.Status == MissionStatus.Accepted || m.Status == MissionStatus.InProgress), ct);

        var availableVehicles = await db.PatrolVehicles
            .CountAsync(v => v.TenantId == TenantId && v.Status == VehicleStatus.Available, ct);

        var vehiclesOnMission = await db.PatrolVehicles
            .CountAsync(v => v.TenantId == TenantId && v.Status == VehicleStatus.Busy, ct);

        // Missions by hour (today)
        var todayMissions = await db.Missions
            .Where(m => m.TenantId == TenantId && m.CreatedAt.Date == today)
            .Select(m => m.CreatedAt.Hour)
            .ToListAsync(ct);

        var missionsByHour = Enumerable.Range(0, 24)
            .Select(h => new { Hour = h, Count = todayMissions.Count(x => x == h) })
            .ToList();

        // Top 5 vehicles (all time)
        var assignments = await db.MissionAssignments
            .Include(a => a.Vehicle)
            .Where(a => a.Vehicle.TenantId == TenantId)
            .ToListAsync(ct);

        var topVehicles = assignments
            .GroupBy(a => new { a.VehicleId, a.Vehicle.CallSign })
            .Select(g => new
            {
                g.Key.CallSign,
                AcceptedCount = g.Count(a => a.Status == MissionStatus.Accepted || a.Status == MissionStatus.Completed),
                RefusedCount  = g.Count(a => a.Status == MissionStatus.Refused),
                TotalCount    = g.Count()
            })
            .OrderByDescending(v => v.AcceptedCount)
            .Take(5)
            .ToList();

        // 10 dernières missions
        var recentMissions = await db.Missions
            .Where(m => m.TenantId == TenantId)
            .Include(m => m.Assignments).ThenInclude(a => a.Vehicle)
            .OrderByDescending(m => m.CreatedAt)
            .Take(10)
            .ToListAsync(ct);

        var recentDto = recentMissions.Select(m =>
        {
            var acceptedAssignment = m.Assignments
                .FirstOrDefault(a => a.Status == MissionStatus.Accepted || a.Status == MissionStatus.Completed
                                     || a.Status == MissionStatus.InProgress);
            return new
            {
                m.Id,
                m.Reference,
                Status = m.Status.ToString(),
                m.TargetAddress,
                AssignedVehicleCallSign = acceptedAssignment?.Vehicle?.CallSign,
                m.CreatedAt,
                m.CompletedAt
            };
        }).ToList();

        return Ok(new
        {
            CallsToday       = callsToday,
            ActiveMissions   = activeMissions,
            AvailableVehicles= availableVehicles,
            VehiclesOnMission= vehiclesOnMission,
            MissionsByHour   = missionsByHour,
            TopVehicles      = topVehicles,
            RecentMissions   = recentDto
        });
    }

    [HttpGet("stats")]
    public async Task<ActionResult<DashboardStats>> GetStats(CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;

        var totalCallsToday = await db.Calls
            .CountAsync(c => c.TenantId == TenantId && c.ReceivedAt.Date == today, ct);

        var openCalls = await db.Calls
            .CountAsync(c => c.TenantId == TenantId
                && (c.Status == CallStatus.Open || c.Status == CallStatus.InProgress), ct);

        var activeMissions = await db.Missions
            .CountAsync(m => m.TenantId == TenantId
                && (m.Status == MissionStatus.Pending
                    || m.Status == MissionStatus.Proposed
                    || m.Status == MissionStatus.Accepted
                    || m.Status == MissionStatus.InProgress), ct);

        var completedToday = await db.Missions
            .CountAsync(m => m.TenantId == TenantId
                && m.Status == MissionStatus.Completed
                && m.CompletedAt.HasValue
                && m.CompletedAt.Value.Date == today, ct);

        var availableVehicles = await db.PatrolVehicles
            .CountAsync(v => v.TenantId == TenantId && v.Status == VehicleStatus.Available, ct);

        var totalVehicles = await db.PatrolVehicles
            .CountAsync(v => v.TenantId == TenantId, ct);

        var highRiskStreets = await db.Streets
            .CountAsync(s => s.TenantId == TenantId && s.CurrentRiskScore >= 70, ct);

        // Average response time for accepted missions today
        var acceptedMissions = await db.Missions
            .Include(m => m.Assignments)
            .Where(m => m.TenantId == TenantId
                && m.AcceptedAt.HasValue
                && m.CreatedAt.Date == today)
            .ToListAsync(ct);

        double avgResponseTime = 0;
        if (acceptedMissions.Count > 0)
        {
            avgResponseTime = acceptedMissions
                .Select(m => (m.AcceptedAt!.Value - m.CreatedAt).TotalMinutes)
                .Average();
        }

        return Ok(new DashboardStats
        {
            TotalCallsToday = totalCallsToday,
            OpenCalls = openCalls,
            ActiveMissions = activeMissions,
            CompletedMissionsToday = completedToday,
            AvailableVehicles = availableVehicles,
            TotalVehicles = totalVehicles,
            HighRiskStreets = highRiskStreets,
            AverageMissionResponseTimeMinutes = Math.Round(avgResponseTime, 1)
        });
    }

    [HttpGet("vehicle-stats")]
    public async Task<ActionResult<List<VehicleStats>>> GetVehicleStats(CancellationToken ct)
    {
        var vehicles = await db.PatrolVehicles
            .Where(v => v.TenantId == TenantId)
            .ToListAsync(ct);

        var assignments = await db.MissionAssignments
            .Include(a => a.Vehicle)
            .Where(a => a.Vehicle.TenantId == TenantId)
            .ToListAsync(ct);

        var stats = vehicles.Select(v =>
        {
            var vehicleAssignments = assignments.Where(a => a.VehicleId == v.Id).ToList();
            return new VehicleStats
            {
                VehicleId = v.Id,
                CallSign = v.CallSign,
                TotalProposed = vehicleAssignments.Count,
                TotalAccepted = vehicleAssignments.Count(a =>
                    a.Status == MissionStatus.Accepted || a.Status == MissionStatus.Completed),
                TotalRefused = vehicleAssignments.Count(a => a.Status == MissionStatus.Refused),
                TotalCompleted = vehicleAssignments.Count(a => a.Status == MissionStatus.Completed)
            };
        }).ToList();

        return Ok(stats);
    }

    [HttpGet("export")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Export(
        [FromQuery] int year, [FromQuery] int month,
        [FromQuery] string format = "xlsx",
        CancellationToken ct = default)
    {
        if (year < 2020 || year > 2100 || month < 1 || month > 12)
            return Problem(title: "Année ou mois invalide", statusCode: 400);

        var from = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddMonths(1);
        var label = from.ToString("yyyy-MM");

        // ---- Missions ----
        var missions = await db.Missions
            .Where(m => m.TenantId == TenantId && m.CreatedAt >= from && m.CreatedAt < to)
            .Include(m => m.Assignments).ThenInclude(a => a.Vehicle)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);

        // ---- Véhicule stats ----
        var vehicles = await db.PatrolVehicles
            .Where(v => v.TenantId == TenantId)
            .ToListAsync(ct);

        var assignments = await db.MissionAssignments
            .Include(a => a.Vehicle)
            .Where(a => a.Vehicle.TenantId == TenantId
                && a.CreatedAt >= from && a.CreatedAt < to)
            .ToListAsync(ct);

        // ---- Appels ----
        var calls = await db.Calls
            .Where(c => c.TenantId == TenantId && c.ReceivedAt >= from && c.ReceivedAt < to)
            .Include(c => c.Operator)
            .OrderBy(c => c.ReceivedAt)
            .ToListAsync(ct);

        // ---- Documents ----
        var docs = await db.TrackingDocuments
            .Where(d => d.TenantId == TenantId && d.CreatedAt >= from && d.CreatedAt < to)
            .Include(d => d.Mission)
            .OrderBy(d => d.CreatedAt)
            .ToListAsync(ct);

        if (format.Equals("csv", StringComparison.OrdinalIgnoreCase))
        {
            var csv = BuildMissionsCsv(missions);
            return File(System.Text.Encoding.UTF8.GetBytes(csv),
                "text/csv; charset=utf-8",
                $"predicop-missions-{label}.csv");
        }

        using var wb = new XLWorkbook();
        AddMissionsSheet(wb, missions);
        AddVehiclesSheet(wb, vehicles, assignments);
        AddCallsSheet(wb, calls);
        AddDocumentsSheet(wb, docs);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"predicop-stats-{label}.xlsx");
    }

    private static string BuildMissionsCsv(List<PrediCop.Core.Entities.Mission> missions)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Référence;Statut;Adresse;Véhicule;Créée le;Acceptée le;Terminée le;Tps réponse (min)");
        foreach (var m in missions)
        {
            var vehicle = m.Assignments
                .Where(a => a.Status == MissionStatus.Accepted || a.Status == MissionStatus.Completed)
                .Select(a => a.Vehicle?.CallSign).FirstOrDefault() ?? "";
            var responseMin = m.AcceptedAt.HasValue
                ? Math.Round((m.AcceptedAt.Value - m.CreatedAt).TotalMinutes, 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : "";
            sb.AppendLine(string.Join(";", [
                m.Reference, m.Status.ToString(), $"\"{m.TargetAddress}\"",
                vehicle,
                m.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                m.AcceptedAt?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "",
                m.CompletedAt?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "",
                responseMin
            ]));
        }
        return sb.ToString();
    }

    private static void AddMissionsSheet(XLWorkbook wb, List<PrediCop.Core.Entities.Mission> missions)
    {
        var ws = wb.Worksheets.Add("Missions");
        string[] headers = ["Référence", "Statut", "Adresse", "Véhicule", "Créée le", "Acceptée le", "Terminée le", "Tps réponse (min)"];
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        StyleHeader(ws.Row(1));

        int row = 2;
        foreach (var m in missions)
        {
            var vehicle = m.Assignments
                .Where(a => a.Status == MissionStatus.Accepted || a.Status == MissionStatus.Completed)
                .Select(a => a.Vehicle?.CallSign).FirstOrDefault() ?? "";
            ws.Cell(row, 1).Value = m.Reference;
            ws.Cell(row, 2).Value = m.Status.ToString();
            ws.Cell(row, 3).Value = m.TargetAddress;
            ws.Cell(row, 4).Value = vehicle;
            ws.Cell(row, 5).Value = m.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
            ws.Cell(row, 6).Value = m.AcceptedAt?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "";
            ws.Cell(row, 7).Value = m.CompletedAt?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "";
            if (m.AcceptedAt.HasValue)
                ws.Cell(row, 8).Value = Math.Round((m.AcceptedAt.Value - m.CreatedAt).TotalMinutes, 1);
            row++;
        }

        ws.Columns().AdjustToContents();
    }

    private static void AddVehiclesSheet(XLWorkbook wb,
        List<PrediCop.Core.Entities.PatrolVehicle> vehicles,
        List<PrediCop.Core.Entities.MissionAssignment> assignments)
    {
        var ws = wb.Worksheets.Add("Véhicules");
        string[] headers = ["Indicatif", "Proposées", "Acceptées", "Refusées", "Terminées", "Taux acceptation %"];
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];
        StyleHeader(ws.Row(1));

        int row = 2;
        foreach (var v in vehicles)
        {
            var va = assignments.Where(a => a.VehicleId == v.Id).ToList();
            var accepted = va.Count(a => a.Status == MissionStatus.Accepted || a.Status == MissionStatus.Completed);
            var refused = va.Count(a => a.Status == MissionStatus.Refused);
            var completed = va.Count(a => a.Status == MissionStatus.Completed);
            var rate = va.Count == 0 ? 0.0 : Math.Round((double)accepted / va.Count * 100, 1);
            ws.Cell(row, 1).Value = v.CallSign;
            ws.Cell(row, 2).Value = va.Count;
            ws.Cell(row, 3).Value = accepted;
            ws.Cell(row, 4).Value = refused;
            ws.Cell(row, 5).Value = completed;
            ws.Cell(row, 6).Value = rate;
            row++;
        }
        ws.Columns().AdjustToContents();
    }

    private static void AddCallsSheet(XLWorkbook wb, List<PrediCop.Core.Entities.Call> calls)
    {
        var ws = wb.Worksheets.Add("Appels");
        string[] headers = ["Référence", "Reçu le", "Appelant", "Téléphone", "Adresse", "Catégorie", "Statut", "Opérateur"];
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];
        StyleHeader(ws.Row(1));

        int row = 2;
        foreach (var c in calls)
        {
            ws.Cell(row, 1).Value = c.Reference;
            ws.Cell(row, 2).Value = c.ReceivedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
            ws.Cell(row, 3).Value = c.CallerName;
            ws.Cell(row, 4).Value = c.CallerPhone;
            ws.Cell(row, 5).Value = c.IncidentAddress;
            ws.Cell(row, 6).Value = c.IncidentCategory;
            ws.Cell(row, 7).Value = c.Status.ToString();
            ws.Cell(row, 8).Value = c.Operator?.FullName ?? "";
            row++;
        }
        ws.Columns().AdjustToContents();
    }

    private static void AddDocumentsSheet(XLWorkbook wb, List<PrediCop.Core.Entities.TrackingDocument> docs)
    {
        var ws = wb.Worksheets.Add("Documents");
        string[] headers = ["Référence", "Type", "Statut", "Mission", "Titre", "Créé le"];
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];
        StyleHeader(ws.Row(1));

        int row = 2;
        foreach (var d in docs)
        {
            ws.Cell(row, 1).Value = d.Reference;
            ws.Cell(row, 2).Value = d.Type.ToString();
            ws.Cell(row, 3).Value = d.Status.ToString();
            ws.Cell(row, 4).Value = d.Mission?.Reference ?? "";
            ws.Cell(row, 5).Value = d.Title;
            ws.Cell(row, 6).Value = d.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
            row++;
        }
        ws.Columns().AdjustToContents();
    }

    private static void StyleHeader(IXLRow row)
    {
        row.Style.Font.Bold = true;
        row.Style.Fill.BackgroundColor = XLColor.FromHtml("#1a2035");
        row.Style.Font.FontColor = XLColor.White;
    }

    [HttpGet("missions-by-hour")]
    public async Task<ActionResult<List<MissionStats>>> GetMissionsByHour(CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;

        var missions = await db.Missions
            .Where(m => m.TenantId == TenantId && m.CreatedAt.Date == today)
            .Select(m => new { m.CreatedAt.Hour, m.Status })
            .ToListAsync(ct);

        var stats = Enumerable.Range(0, 24).Select(hour => new MissionStats
        {
            Hour = hour,
            MissionCount = missions.Count(m => m.Hour == hour),
            CompletedCount = missions.Count(m => m.Hour == hour && m.Status == MissionStatus.Completed)
        }).ToList();

        return Ok(stats);
    }
}
