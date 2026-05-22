using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrediCop.Core.DTOs;
using PrediCop.Core.Entities;
using PrediCop.Core.Enums;
using PrediCop.Infrastructure.Data;
using System.Text;
using System.Text.Json;

namespace PrediCop.Api.Controllers;

[ApiController]
[Route("api/tickets")]
[Authorize]
public class TicketsController(AppDbContext db) : ControllerBase
{
    private Guid TenantId => (Guid)HttpContext.Items["TenantId"]!;

    private async Task<bool> IsModuleEnabledAsync(CancellationToken ct)
    {
        var tenant = await db.Tenants.FindAsync([TenantId], ct);
        return tenant?.ModuleVerbalisationEnabled ?? false;
    }

    private static readonly Dictionary<DayOfWeek, string> FrenchDays = new()
    {
        { DayOfWeek.Monday,    "Lundi"    },
        { DayOfWeek.Tuesday,   "Mardi"    },
        { DayOfWeek.Wednesday, "Mercredi" },
        { DayOfWeek.Thursday,  "Jeudi"    },
        { DayOfWeek.Friday,    "Vendredi" },
        { DayOfWeek.Saturday,  "Samedi"   },
        { DayOfWeek.Sunday,    "Dimanche" }
    };

    // ── GET /api/tickets ──────────────────────────────────────────────────────

    [HttpGet]
    public async Task<ActionResult<List<ElectronicTicketResponse>>> GetTickets(
        [FromQuery] Guid? agentId,
        [FromQuery] TicketStatus? status,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] string? plate,
        CancellationToken ct = default)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var query = db.Set<ElectronicTicket>()
            .Include(t => t.IssuedBy)
            .Include(t => t.Mission)
            .Where(t => t.TenantId == TenantId);

        if (agentId.HasValue)
            query = query.Where(t => t.IssuedById == agentId.Value);

        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);

        if (dateFrom.HasValue)
            query = query.Where(t => t.IssuedAt >= dateFrom.Value.ToUniversalTime());

        if (dateTo.HasValue)
            query = query.Where(t => t.IssuedAt <= dateTo.Value.ToUniversalTime().AddDays(1).AddTicks(-1));

        if (!string.IsNullOrWhiteSpace(plate))
            query = query.Where(t => t.PlateNumber.Contains(plate.Trim().ToUpper()));

        var tickets = await query
            .OrderByDescending(t => t.IssuedAt)
            .ToListAsync(ct);

        return Ok(tickets.Select(MapToResponse).ToList());
    }

    // ── GET /api/tickets/{id} ─────────────────────────────────────────────────

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ElectronicTicketResponse>> GetTicket(Guid id, CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var ticket = await db.Set<ElectronicTicket>()
            .Include(t => t.IssuedBy)
            .Include(t => t.Mission)
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == TenantId, ct);

        if (ticket is null)
            return Problem(title: "PV non trouvé", statusCode: 404);

        return Ok(MapToResponse(ticket));
    }

    // ── POST /api/tickets ─────────────────────────────────────────────────────

    [HttpPost]
    public async Task<ActionResult<ElectronicTicketResponse>> CreateTicket(
        [FromBody] CreateTicketRequest request,
        CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        // Vérifier que l'agent appartient au tenant
        var agent = await db.Users
            .FirstOrDefaultAsync(u => u.Id == request.IssuedById && u.TenantId == TenantId, ct);

        if (agent is null)
            return Problem(title: "Agent introuvable ou n'appartient pas au tenant", statusCode: 422);

        // Générer TicketNumber : PV-{yyyy}-{count:D5}
        var year = DateTime.UtcNow.Year;
        var yearStart = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var countThisYear = await db.Set<ElectronicTicket>()
            .IgnoreQueryFilters()
            .CountAsync(t => t.TenantId == TenantId && t.CreatedAt >= yearStart, ct);

        var ticketNumber = $"PV-{year}-{countThisYear + 1:D5}";

        // Vérifier que la mission appartient au tenant (si fournie)
        Mission? mission = null;
        if (request.MissionId.HasValue)
        {
            mission = await db.Set<Mission>()
                .FirstOrDefaultAsync(m => m.Id == request.MissionId.Value && m.TenantId == TenantId, ct);
            if (mission is null)
                return Problem(title: "Mission introuvable ou n'appartient pas au tenant", statusCode: 422);
        }

        var ticket = new ElectronicTicket
        {
            TenantId = TenantId,
            TicketNumber = ticketNumber,
            IssuedAt = DateTime.UtcNow,
            IssuedById = request.IssuedById,
            IssuedAtAddress = request.IssuedAtAddress,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            PlateNumber = request.PlateNumber.Trim().ToUpper(),
            VehicleMake = request.VehicleMake,
            VehicleModel = request.VehicleModel,
            VehicleColor = request.VehicleColor,
            InfractionType = request.InfractionType,
            ArticleCode = request.ArticleCode,
            FineAmount = request.FineAmount,
            Notes = request.Notes,
            Status = TicketStatus.Issued,
            MissionId = request.MissionId
        };

        db.Set<ElectronicTicket>().Add(ticket);
        await db.SaveChangesAsync(ct);

        ticket.IssuedBy = agent;
        ticket.Mission = mission;
        return CreatedAtAction(nameof(GetTicket), new { id = ticket.Id }, MapToResponse(ticket));
    }

    // ── PUT /api/tickets/{id}/status ──────────────────────────────────────────

    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult<ElectronicTicketResponse>> UpdateStatus(
        Guid id,
        [FromBody] UpdateTicketStatusRequest request,
        CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var ticket = await db.Set<ElectronicTicket>()
            .Include(t => t.IssuedBy)
            .Include(t => t.Mission)
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == TenantId, ct);

        if (ticket is null)
            return Problem(title: "PV non trouvé", statusCode: 404);

        ticket.Status = request.Status;

        if (request.Status == TicketStatus.Cancelled && request.Notes is not null)
            ticket.Notes = string.IsNullOrWhiteSpace(ticket.Notes)
                ? request.Notes
                : $"{ticket.Notes}\n[Annulation] {request.Notes}";

        ticket.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(MapToResponse(ticket));
    }

    // ── DELETE /api/tickets/{id} ──────────────────────────────────────────────

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteTicket(Guid id, CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var ticket = await db.Set<ElectronicTicket>()
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == TenantId, ct);

        if (ticket is null)
            return Problem(title: "PV non trouvé", statusCode: 404);

        if (ticket.Status == TicketStatus.Paid)
            return Problem(title: "Impossible de supprimer un PV payé", statusCode: 422);

        ticket.IsDeleted = true;
        ticket.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    // ── GET /api/tickets/stats ────────────────────────────────────────────────

    [HttpGet("stats")]
    public async Task<ActionResult<TicketStatsResponse>> GetStats(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] Guid? agentId,
        CancellationToken ct = default)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var query = db.Set<ElectronicTicket>()
            .Include(t => t.IssuedBy)
            .Where(t => t.TenantId == TenantId);

        if (dateFrom.HasValue)
            query = query.Where(t => t.IssuedAt >= dateFrom.Value.ToUniversalTime());

        if (dateTo.HasValue)
            query = query.Where(t => t.IssuedAt <= dateTo.Value.ToUniversalTime().AddDays(1).AddTicks(-1));

        if (agentId.HasValue)
            query = query.Where(t => t.IssuedById == agentId.Value);

        var tickets = await query.ToListAsync(ct);

        var totalIssued    = tickets.Count(t => t.Status == TicketStatus.Issued);
        var totalPaid      = tickets.Count(t => t.Status == TicketStatus.Paid);
        var totalContested = tickets.Count(t => t.Status == TicketStatus.Contested);
        var totalCancelled = tickets.Count(t => t.Status == TicketStatus.Cancelled);

        var totalFine = tickets
            .Where(t => t.Status == TicketStatus.Issued || t.Status == TicketStatus.Paid)
            .Sum(t => t.FineAmount);

        var byInfraction = tickets
            .GroupBy(t => t.InfractionType.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var byAgent = tickets
            .GroupBy(t => t.IssuedBy?.FullName ?? "Inconnu")
            .ToDictionary(g => g.Key, g => g.Count());

        var byDayOfWeek = tickets
            .GroupBy(t => t.IssuedAt.DayOfWeek)
            .ToDictionary(
                g => FrenchDays[g.Key],
                g => g.Count());

        return Ok(new TicketStatsResponse(
            totalIssued,
            totalPaid,
            totalContested,
            totalCancelled,
            totalFine,
            byInfraction,
            byAgent,
            byDayOfWeek
        ));
    }

    // ── GET /api/tickets/export ───────────────────────────────────────────────

    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] string format = "csv",
        CancellationToken ct = default)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var query = db.Set<ElectronicTicket>()
            .Include(t => t.IssuedBy)
            .Where(t => t.TenantId == TenantId);

        if (dateFrom.HasValue)
            query = query.Where(t => t.IssuedAt >= dateFrom.Value.ToUniversalTime());

        if (dateTo.HasValue)
            query = query.Where(t => t.IssuedAt <= dateTo.Value.ToUniversalTime().AddDays(1).AddTicks(-1));

        var tickets = await query.OrderByDescending(t => t.IssuedAt).ToListAsync(ct);

        // Marquer les PV non annulés comme exportés vers l'ANTAI
        var now = DateTime.UtcNow;
        foreach (var t in tickets.Where(t => t.Status != TicketStatus.Cancelled))
        {
            t.ExportedToAntai = true;
            t.ExportedAt = now;
        }
        await db.SaveChangesAsync(ct);

        var fromStr = dateFrom?.ToString("yyyy-MM-dd") ?? "debut";
        var toStr   = dateTo?.ToString("yyyy-MM-dd")   ?? "fin";

        if (format.Equals("xlsx", StringComparison.OrdinalIgnoreCase))
        {
            using var workbook  = new XLWorkbook();
            var ws = workbook.Worksheets.Add("PV");

            // Header
            var headers = new[]
            {
                "Numéro PV", "Date", "Agent", "Badge", "Adresse",
                "Plaque", "Marque", "Modèle", "Couleur",
                "Infraction", "Article", "Montant (€)", "Statut"
            };

            for (var c = 0; c < headers.Length; c++)
            {
                var cell = ws.Cell(1, c + 1);
                cell.Value = headers[c];
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1A2035");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Font.Bold = true;
            }

            // Rows
            for (var r = 0; r < tickets.Count; r++)
            {
                var t = tickets[r];
                var row = r + 2;
                ws.Cell(row, 1).Value  = t.TicketNumber;
                ws.Cell(row, 2).Value  = t.IssuedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
                ws.Cell(row, 3).Value  = t.IssuedBy?.FullName ?? "";
                ws.Cell(row, 4).Value  = t.IssuedBy?.BadgeNumber ?? "";
                ws.Cell(row, 5).Value  = t.IssuedAtAddress;
                ws.Cell(row, 6).Value  = t.PlateNumber;
                ws.Cell(row, 7).Value  = t.VehicleMake ?? "";
                ws.Cell(row, 8).Value  = t.VehicleModel ?? "";
                ws.Cell(row, 9).Value  = t.VehicleColor ?? "";
                ws.Cell(row, 10).Value = t.InfractionType.ToString();
                ws.Cell(row, 11).Value = t.ArticleCode ?? "";
                ws.Cell(row, 12).Value = (double)t.FineAmount;
                ws.Cell(row, 13).Value = t.Status.ToString();
            }

            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            ms.Position = 0;

            var fileName = $"pv-export-{fromStr}-{toStr}.xlsx";
            Response.Headers["Content-Disposition"] = $"attachment; filename=\"{fileName}\"";
            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }
        else
        {
            // CSV
            var sb = new StringBuilder();
            sb.AppendLine("NuméroPV;Date;Agent;Badge;Adresse;Plaque;Marque;Modèle;Couleur;Infraction;Article;Montant;Statut");

            foreach (var t in tickets)
            {
                sb.AppendLine(string.Join(";",
                    Escape(t.TicketNumber),
                    t.IssuedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                    Escape(t.IssuedBy?.FullName ?? ""),
                    Escape(t.IssuedBy?.BadgeNumber ?? ""),
                    Escape(t.IssuedAtAddress),
                    Escape(t.PlateNumber),
                    Escape(t.VehicleMake ?? ""),
                    Escape(t.VehicleModel ?? ""),
                    Escape(t.VehicleColor ?? ""),
                    t.InfractionType.ToString(),
                    Escape(t.ArticleCode ?? ""),
                    t.FineAmount.ToString("F2"),
                    t.Status.ToString()
                ));
            }

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            var fileName = $"pv-export-{fromStr}-{toStr}.csv";
            Response.Headers["Content-Disposition"] = $"attachment; filename=\"{fileName}\"";
            return File(bytes, "text/csv; charset=utf-8", fileName);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string Escape(string value)
        => value.Contains(';') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;

    private static ElectronicTicketResponse MapToResponse(ElectronicTicket t) => new(
        t.Id,
        t.TicketNumber,
        t.IssuedAt,
        t.IssuedById,
        t.IssuedBy?.FullName ?? string.Empty,
        t.IssuedBy?.BadgeNumber ?? string.Empty,
        t.IssuedAtAddress,
        t.Latitude,
        t.Longitude,
        t.PlateNumber,
        t.VehicleMake,
        t.VehicleModel,
        t.VehicleColor,
        t.InfractionType,
        t.ArticleCode,
        t.FineAmount,
        t.Notes,
        t.Status,
        t.IsSigned,
        t.SignedAt,
        t.ExportedToAntai,
        t.ExportedAt,
        t.CreatedAt,
        t.MissionId,
        t.Mission?.Reference
    );
}
