using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrediCop.Core.DTOs;
using PrediCop.Core.Entities;
using PrediCop.Core.Enums;
using PrediCop.Infrastructure.Data;

namespace PrediCop.Api.Controllers;

[ApiController]
[Route("api/hr")]
[Authorize(Roles = "Admin,Manager")]
public class HrController(AppDbContext db) : ControllerBase
{
    private Guid TenantId => (Guid)HttpContext.Items["TenantId"]!;

    private async Task<Tenant?> GetTenantAsync(CancellationToken ct)
        => await db.Tenants.FindAsync([TenantId], ct);

    private async Task<bool> IsModuleEnabledAsync(CancellationToken ct)
    {
        var tenant = await GetTenantAsync(ct);
        return tenant?.ModulePlanningEnabled ?? false;
    }

    // -------- Agent Profiles --------

    [HttpGet("profiles")]
    public async Task<ActionResult<List<AgentProfileResponse>>> GetProfiles(CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var profiles = await db.Set<AgentProfile>()
            .Include(p => p.Agent)
            .Where(p => p.TenantId == TenantId)
            .ToListAsync(ct);

        return Ok(profiles.Select(MapProfileToResponse).ToList());
    }

    [HttpGet("profiles/{agentId:guid}")]
    public async Task<ActionResult<AgentProfileResponse>> GetProfile(Guid agentId, CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var profile = await db.Set<AgentProfile>()
            .Include(p => p.Agent)
            .FirstOrDefaultAsync(p => p.AgentId == agentId && p.TenantId == TenantId, ct);

        if (profile is null)
            return Problem(title: "Profil agent non trouvé", statusCode: 404);

        return Ok(MapProfileToResponse(profile));
    }

    [HttpPost("profiles/{agentId:guid}")]
    public async Task<ActionResult<AgentProfileResponse>> UpsertProfile(
        Guid agentId,
        [FromBody] UpsertAgentProfileRequest request,
        CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var agentExists = await db.Users.AnyAsync(u => u.Id == agentId && u.TenantId == TenantId, ct);
        if (!agentExists)
            return Problem(title: "Agent non trouvé", statusCode: 404);

        var profile = await db.Set<AgentProfile>()
            .Include(p => p.Agent)
            .FirstOrDefaultAsync(p => p.AgentId == agentId && p.TenantId == TenantId, ct);

        if (profile is null)
        {
            profile = new AgentProfile
            {
                TenantId = TenantId,
                AgentId = agentId
            };
            db.Set<AgentProfile>().Add(profile);
        }

        profile.EmergencyContact1Name = request.EmergencyContact1Name;
        profile.EmergencyContact1Phone = request.EmergencyContact1Phone;
        profile.EmergencyContact1Relationship = request.EmergencyContact1Relationship;
        profile.EmergencyContact2Name = request.EmergencyContact2Name;
        profile.EmergencyContact2Phone = request.EmergencyContact2Phone;
        profile.Notes = request.Notes;
        profile.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        if (profile.Agent is null)
            await db.Entry(profile).Reference(p => p.Agent).LoadAsync(ct);

        return Ok(MapProfileToResponse(profile));
    }

    // -------- Leaves --------

    [HttpGet("leaves")]
    public async Task<ActionResult<List<LeaveResponse>>> GetLeaves(
        [FromQuery] Guid? agentId,
        [FromQuery] LeaveStatus? status,
        [FromQuery] DateOnly? dateFrom,
        [FromQuery] DateOnly? dateTo,
        CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var query = db.Set<Leave>()
            .Include(l => l.Agent)
            .Where(l => l.TenantId == TenantId);

        if (agentId.HasValue)
            query = query.Where(l => l.AgentId == agentId.Value);

        if (status.HasValue)
            query = query.Where(l => l.Status == status.Value);

        if (dateFrom.HasValue)
            query = query.Where(l => l.EndDate >= dateFrom.Value);

        if (dateTo.HasValue)
            query = query.Where(l => l.StartDate <= dateTo.Value);

        var leaves = await query
            .OrderByDescending(l => l.RequestedAt)
            .ToListAsync(ct);

        // Load approvers separately to avoid complex join
        var approverIds = leaves
            .Where(l => l.ApprovedById.HasValue)
            .Select(l => l.ApprovedById!.Value)
            .Distinct()
            .ToList();

        var approvers = approverIds.Count > 0
            ? await db.Users
                .Where(u => approverIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.FullName, ct)
            : new Dictionary<Guid, string>();

        return Ok(leaves.Select(l => MapLeaveToResponse(l, approvers)).ToList());
    }

    [HttpPost("leaves")]
    public async Task<ActionResult<LeaveResponse>> CreateLeave(
        [FromBody] CreateLeaveRequest request,
        CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var agentExists = await db.Users.AnyAsync(u => u.Id == request.AgentId && u.TenantId == TenantId, ct);
        if (!agentExists)
            return Problem(title: "Agent non trouvé", statusCode: 404);

        var leave = new Leave
        {
            TenantId = TenantId,
            AgentId = request.AgentId,
            Type = request.Type,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Notes = request.Notes,
            Status = LeaveStatus.Pending,
            RequestedAt = DateTime.UtcNow
        };

        db.Set<Leave>().Add(leave);
        await db.SaveChangesAsync(ct);

        await db.Entry(leave).Reference(l => l.Agent).LoadAsync(ct);

        return Ok(MapLeaveToResponse(leave, new Dictionary<Guid, string>()));
    }

    [HttpPost("leaves/{id:guid}/approve")]
    public async Task<ActionResult<LeaveResponse>> ApproveLeave(
        Guid id,
        [FromBody] ApproveLeaveRequest request,
        CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var leave = await db.Set<Leave>()
            .Include(l => l.Agent)
            .FirstOrDefaultAsync(l => l.Id == id && l.TenantId == TenantId, ct);

        if (leave is null)
            return Problem(title: "Congé non trouvé", statusCode: 404);

        var userId = Guid.Parse(User.FindFirst("sub")?.Value ?? Guid.Empty.ToString());

        leave.Status = LeaveStatus.Approved;
        leave.ApprovedById = userId;
        leave.ApprovedAt = DateTime.UtcNow;
        if (request.Notes is not null) leave.Notes = request.Notes;
        leave.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        var approverName = string.Empty;
        if (userId != Guid.Empty)
        {
            var approver = await db.Users.FindAsync([userId], ct);
            approverName = approver?.FullName ?? string.Empty;
        }

        var approvers = userId != Guid.Empty && !string.IsNullOrEmpty(approverName)
            ? new Dictionary<Guid, string> { { userId, approverName } }
            : new Dictionary<Guid, string>();

        return Ok(MapLeaveToResponse(leave, approvers));
    }

    [HttpPost("leaves/{id:guid}/reject")]
    public async Task<ActionResult<LeaveResponse>> RejectLeave(
        Guid id,
        [FromBody] RejectLeaveRequest request,
        CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var leave = await db.Set<Leave>()
            .Include(l => l.Agent)
            .FirstOrDefaultAsync(l => l.Id == id && l.TenantId == TenantId, ct);

        if (leave is null)
            return Problem(title: "Congé non trouvé", statusCode: 404);

        leave.Status = LeaveStatus.Rejected;
        leave.RejectionReason = request.RejectionReason;
        leave.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(MapLeaveToResponse(leave, new Dictionary<Guid, string>()));
    }

    [HttpDelete("leaves/{id:guid}")]
    public async Task<IActionResult> DeleteLeave(Guid id, CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var leave = await db.Set<Leave>()
            .FirstOrDefaultAsync(l => l.Id == id && l.TenantId == TenantId, ct);

        if (leave is null)
            return Problem(title: "Congé non trouvé", statusCode: 404);

        leave.IsDeleted = true;
        leave.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    // -------- Vehicles Occupancy --------

    [HttpGet("vehicles/occupancy")]
    public async Task<ActionResult<List<VehicleOccupancyResponse>>> GetVehicleOccupancy(
        [FromQuery] string? date,
        CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        DateOnly targetDate;
        if (!DateOnly.TryParse(date, out targetDate))
            targetDate = DateOnly.FromDateTime(DateTime.UtcNow);

        // Charger tous les véhicules du tenant
        var vehicles = await db.PatrolVehicles
            .Where(v => v.TenantId == TenantId)
            .OrderBy(v => v.CallSign)
            .ToListAsync(ct);

        if (vehicles.Count == 0)
            return Ok(new List<VehicleOccupancyResponse>());

        var vehicleIds = vehicles.Select(v => v.Id).ToList();

        // Charger les créneaux pour cette date, groupés par VehicleId
        var shifts = await db.Set<ShiftSchedule>()
            .Include(s => s.Agent)
            .Where(s => s.TenantId == TenantId
                     && s.Date == targetDate
                     && s.VehicleId.HasValue
                     && vehicleIds.Contains(s.VehicleId!.Value))
            .ToListAsync(ct);

        var shiftsByVehicle = shifts
            .GroupBy(s => s.VehicleId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = vehicles.Select(v =>
        {
            shiftsByVehicle.TryGetValue(v.Id, out var vehicleShifts);
            var agents = vehicleShifts?.Select(s => s.Agent?.FullName ?? "Agent inconnu").ToList()
                         ?? [];
            return new VehicleOccupancyResponse(
                v.Id,
                v.CallSign,
                v.Capacity,
                agents.Count,
                agents);
        }).ToList();

        return Ok(result);
    }

    // -------- Schedules --------

    [HttpGet("schedules")]
    public async Task<ActionResult<List<ShiftScheduleResponse>>> GetSchedules(
        [FromQuery] string? weekStart,
        CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        DateOnly startDate;
        if (!DateOnly.TryParse(weekStart, out startDate))
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var daysToMonday = ((int)today.DayOfWeek - 1 + 7) % 7;
            startDate = today.AddDays(-daysToMonday);
        }

        var endDate = startDate.AddDays(6);

        var schedules = await db.Set<ShiftSchedule>()
            .Include(s => s.Agent)
            .Include(s => s.Vehicle)
            .Where(s => s.TenantId == TenantId && s.Date >= startDate && s.Date <= endDate)
            .OrderBy(s => s.Date)
            .ThenBy(s => s.ShiftStart)
            .ToListAsync(ct);

        return Ok(schedules.Select(MapScheduleToResponse).ToList());
    }

    [HttpPost("schedules")]
    public async Task<ActionResult<ShiftScheduleResponse>> UpsertSchedule(
        [FromBody] UpsertShiftRequest request,
        CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var agentExists = await db.Users.AnyAsync(u => u.Id == request.AgentId && u.TenantId == TenantId, ct);
        if (!agentExists)
            return Problem(title: "Agent non trouvé", statusCode: 404);

        // Check if a schedule already exists for this agent on this date
        var existing = await db.Set<ShiftSchedule>()
            .Include(s => s.Agent)
            .Include(s => s.Vehicle)
            .FirstOrDefaultAsync(s => s.AgentId == request.AgentId
                && s.Date == request.Date
                && s.TenantId == TenantId, ct);

        if (existing is not null)
        {
            existing.VehicleId = request.VehicleId;
            existing.ShiftStart = request.ShiftStart;
            existing.ShiftEnd = request.ShiftEnd;
            existing.IsPublished = request.IsPublished;
            existing.Notes = request.Notes;
            existing.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);

            if (existing.Vehicle is null && existing.VehicleId.HasValue)
                await db.Entry(existing).Reference(s => s.Vehicle).LoadAsync(ct);

            return Ok(MapScheduleToResponse(existing));
        }
        else
        {
            var schedule = new ShiftSchedule
            {
                TenantId = TenantId,
                AgentId = request.AgentId,
                VehicleId = request.VehicleId,
                Date = request.Date,
                ShiftStart = request.ShiftStart,
                ShiftEnd = request.ShiftEnd,
                IsPublished = request.IsPublished,
                Notes = request.Notes
            };

            db.Set<ShiftSchedule>().Add(schedule);
            await db.SaveChangesAsync(ct);

            await db.Entry(schedule).Reference(s => s.Agent).LoadAsync(ct);
            if (schedule.VehicleId.HasValue)
                await db.Entry(schedule).Reference(s => s.Vehicle).LoadAsync(ct);

            return Ok(MapScheduleToResponse(schedule));
        }
    }

    /// <summary>
    /// Import créneaux depuis un fichier CSV.
    /// Format attendu (séparateur virgule ou point-virgule) :
    ///   Matricule,Nom,Prénom,Date,HeureDebut,HeureFin[,Véhicule][,Notes][,Publié]
    /// La colonne Matricule (BadgeNumber) est utilisée en priorité pour identifier l'agent.
    /// Si le matricule n'est pas trouvé, on tente un match Nom+Prénom (case-insensitive, trim).
    /// Si 0 ou 2+ agents correspondent → ligne en erreur.
    /// </summary>
    [HttpPost("schedules/import-csv")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<CsvImportResult>> ImportSchedulesCsv(
        IFormFile file,
        CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        if (file is null || file.Length == 0)
            return BadRequest("Aucun fichier fourni.");

        // Charger tous les agents du tenant en mémoire pour matcher efficacement
        var agents = await db.Users
            .Where(u => u.TenantId == TenantId && u.IsActive)
            .ToListAsync(ct);

        var errors = new List<CsvImportError>();
        int imported = 0;
        int skipped = 0;

        using var reader = new System.IO.StreamReader(file.OpenReadStream(), System.Text.Encoding.UTF8);
        int lineNumber = 0;
        string? line;

        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            lineNumber++;

            // Ignorer les lignes vides et l'en-tête
            if (string.IsNullOrWhiteSpace(line)) { skipped++; continue; }

            // Détecter le séparateur : virgule ou point-virgule
            var sep = line.Contains(';') ? ';' : ',';
            var cols = line.Split(sep);

            // Ignorer si ressemble à un en-tête (première colonne non numérique et non un matricule connu)
            if (lineNumber == 1)
            {
                var firstCol = cols[0].Trim().ToUpperInvariant();
                if (firstCol is "MATRICULE" or "BADGENUMBER" or "N°" or "NUMERO" or "N" or "ID")
                { skipped++; continue; }
            }

            if (cols.Length < 5)
            {
                errors.Add(new CsvImportError(lineNumber, line,
                    "Ligne malformée : au moins 5 colonnes requises (Matricule, Nom, Prénom, Date, HeureDebut, HeureFin)."));
                continue;
            }

            var badge  = cols[0].Trim();
            var nom    = cols[1].Trim();
            var prenom = cols[2].Trim();
            var dateStr  = cols[3].Trim();
            var startStr = cols[4].Trim();
            var endStr   = cols.Length > 5 ? cols[5].Trim() : string.Empty;

            // Résolution de l'agent
            User? agent = null;

            // 1) Par matricule
            if (!string.IsNullOrEmpty(badge))
            {
                agent = agents.FirstOrDefault(a =>
                    string.Equals(a.BadgeNumber, badge, StringComparison.OrdinalIgnoreCase));
            }

            // 2) Fallback : Nom + Prénom
            if (agent is null)
            {
                var candidates = agents
                    .Where(a =>
                        string.Equals(a.LastName.Trim(), nom, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(a.FirstName.Trim(), prenom, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (candidates.Count == 0)
                {
                    errors.Add(new CsvImportError(lineNumber, line,
                        $"Agent introuvable par matricule ni par nom/prénom ({nom} {prenom})."));
                    continue;
                }

                if (candidates.Count > 1)
                {
                    errors.Add(new CsvImportError(lineNumber, line,
                        $"Homonymes détectés pour {nom} {prenom} ({candidates.Count} agents). Utilisez le matricule."));
                    continue;
                }

                agent = candidates[0];
            }

            // Validation date / heures
            if (!DateOnly.TryParse(dateStr, out var date))
            {
                errors.Add(new CsvImportError(lineNumber, line, $"Date invalide : \"{dateStr}\"."));
                continue;
            }

            if (!TimeOnly.TryParse(startStr, out var shiftStart))
            {
                errors.Add(new CsvImportError(lineNumber, line, $"Heure de début invalide : \"{startStr}\"."));
                continue;
            }

            if (string.IsNullOrEmpty(endStr) || !TimeOnly.TryParse(endStr, out var shiftEnd))
            {
                errors.Add(new CsvImportError(lineNumber, line, $"Heure de fin invalide : \"{endStr}\"."));
                continue;
            }

            // Colonnes optionnelles
            var vehicleCallSign = cols.Length > 6 ? cols[6].Trim() : null;
            var notes = cols.Length > 7 ? cols[7].Trim() : null;
            var isPublished = cols.Length > 8 && cols[8].Trim() is "1" or "true" or "oui" or "yes";

            // Résolution véhicule (optionnel)
            Guid? vehicleId = null;
            if (!string.IsNullOrEmpty(vehicleCallSign))
            {
                var vehicle = await db.PatrolVehicles
                    .Where(v => v.TenantId == TenantId &&
                                EF.Functions.Like(v.CallSign, vehicleCallSign))
                    .FirstOrDefaultAsync(ct);
                vehicleId = vehicle?.Id;
            }

            // Upsert créneau
            var existing = await db.Set<ShiftSchedule>()
                .FirstOrDefaultAsync(s =>
                    s.AgentId == agent.Id &&
                    s.Date == date &&
                    s.TenantId == TenantId, ct);

            if (existing is not null)
            {
                existing.VehicleId  = vehicleId;
                existing.ShiftStart = shiftStart;
                existing.ShiftEnd   = shiftEnd;
                existing.IsPublished = isPublished;
                existing.Notes      = string.IsNullOrEmpty(notes) ? existing.Notes : notes;
                existing.UpdatedAt  = DateTime.UtcNow;
            }
            else
            {
                db.Set<ShiftSchedule>().Add(new ShiftSchedule
                {
                    TenantId    = TenantId,
                    AgentId     = agent.Id,
                    VehicleId   = vehicleId,
                    Date        = date,
                    ShiftStart  = shiftStart,
                    ShiftEnd    = shiftEnd,
                    IsPublished = isPublished,
                    Notes       = string.IsNullOrEmpty(notes) ? null : notes
                });
            }

            imported++;
        }

        if (imported > 0)
            await db.SaveChangesAsync(ct);

        return Ok(new CsvImportResult(imported, skipped, errors));
    }

    [HttpDelete("schedules/{id:guid}")]
    public async Task<IActionResult> DeleteSchedule(Guid id, CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var schedule = await db.Set<ShiftSchedule>()
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == TenantId, ct);

        if (schedule is null)
            return Problem(title: "Créneau non trouvé", statusCode: 404);

        schedule.IsDeleted = true;
        schedule.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    // -------- Entitlements --------

    [HttpGet("entitlements")]
    public async Task<ActionResult<List<LeaveEntitlementResponse>>> GetEntitlements(
        [FromQuery] Guid? agentId,
        CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var query = db.LeaveEntitlements
            .Include(e => e.Agent)
            .Where(e => e.TenantId == TenantId);

        if (agentId.HasValue)
            query = query.Where(e => e.AgentId == agentId.Value);

        var entitlements = await query
            .OrderBy(e => e.Agent.LastName)
            .ThenBy(e => e.Type)
            .ToListAsync(ct);

        // Charger les congés approuvés pour calculer UsedDays
        var agentIds = entitlements.Select(e => e.AgentId).Distinct().ToList();
        var approvedLeaves = await db.Leaves
            .Where(l => l.TenantId == TenantId
                     && l.Status == LeaveStatus.Approved
                     && agentIds.Contains(l.AgentId))
            .ToListAsync(ct);

        return Ok(entitlements.Select(e => MapEntitlementToResponse(e, approvedLeaves)).ToList());
    }

    [HttpPost("entitlements")]
    public async Task<ActionResult<LeaveEntitlementResponse>> CreateEntitlement(
        [FromBody] CreateLeaveEntitlementRequest request,
        CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var agent = await db.Users.FirstOrDefaultAsync(u => u.Id == request.AgentId && u.TenantId == TenantId, ct);
        if (agent is null)
            return Problem(title: "Agent non trouvé", statusCode: 404);

        var entitlement = new LeaveEntitlement
        {
            TenantId = TenantId,
            AgentId = request.AgentId,
            Agent = agent,
            Type = request.Type,
            TotalDays = request.TotalDays,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo
        };

        db.LeaveEntitlements.Add(entitlement);
        await db.SaveChangesAsync(ct);

        var approvedLeaves = await db.Leaves
            .Where(l => l.TenantId == TenantId
                     && l.Status == LeaveStatus.Approved
                     && l.AgentId == entitlement.AgentId)
            .ToListAsync(ct);

        return Ok(MapEntitlementToResponse(entitlement, approvedLeaves));
    }

    [HttpPut("entitlements/{id:guid}")]
    public async Task<ActionResult<LeaveEntitlementResponse>> UpdateEntitlement(
        Guid id,
        [FromBody] UpdateLeaveEntitlementRequest request,
        CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var entitlement = await db.LeaveEntitlements
            .Include(e => e.Agent)
            .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == TenantId, ct);

        if (entitlement is null)
            return Problem(title: "Droit non trouvé", statusCode: 404);

        entitlement.TotalDays = request.TotalDays;
        entitlement.ValidFrom = request.ValidFrom;
        entitlement.ValidTo = request.ValidTo;
        entitlement.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        var approvedLeaves = await db.Leaves
            .Where(l => l.TenantId == TenantId
                     && l.Status == LeaveStatus.Approved
                     && l.AgentId == entitlement.AgentId)
            .ToListAsync(ct);

        return Ok(MapEntitlementToResponse(entitlement, approvedLeaves));
    }

    [HttpDelete("entitlements/{id:guid}")]
    public async Task<IActionResult> DeleteEntitlement(Guid id, CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var entitlement = await db.LeaveEntitlements
            .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == TenantId, ct);

        if (entitlement is null)
            return Problem(title: "Droit non trouvé", statusCode: 404);

        entitlement.IsDeleted = true;
        entitlement.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    // -------- Mappers --------

    private static AgentProfileResponse MapProfileToResponse(AgentProfile p) => new(
        p.Id,
        p.AgentId,
        p.Agent?.FullName ?? string.Empty,
        p.Agent?.BadgeNumber ?? string.Empty,
        p.EmergencyContact1Name,
        p.EmergencyContact1Phone,
        p.EmergencyContact1Relationship,
        p.EmergencyContact2Name,
        p.EmergencyContact2Phone,
        p.Notes);

    private static LeaveResponse MapLeaveToResponse(Leave l, Dictionary<Guid, string> approvers) => new(
        l.Id,
        l.AgentId,
        l.Agent?.FullName ?? string.Empty,
        l.Agent?.BadgeNumber ?? string.Empty,
        l.Type,
        l.StartDate,
        l.EndDate,
        l.Status,
        l.RequestedAt,
        l.ApprovedAt,
        l.ApprovedById.HasValue && approvers.TryGetValue(l.ApprovedById.Value, out var name) ? name : null,
        l.Notes,
        l.RejectionReason);

    private static ShiftScheduleResponse MapScheduleToResponse(ShiftSchedule s) => new(
        s.Id,
        s.AgentId,
        s.Agent?.FullName ?? string.Empty,
        s.Agent?.BadgeNumber ?? string.Empty,
        s.VehicleId,
        s.Vehicle?.CallSign,
        s.Date,
        s.ShiftStart,
        s.ShiftEnd,
        s.IsPublished,
        s.Notes);

    private static LeaveEntitlementResponse MapEntitlementToResponse(
        LeaveEntitlement e,
        List<Leave> allApprovedLeaves)
    {
        var usedDays = allApprovedLeaves
            .Where(l => l.AgentId == e.AgentId
                     && l.Type == e.Type
                     && l.StartDate >= e.ValidFrom
                     && l.EndDate <= e.ValidTo)
            .Sum(l => (decimal)(l.EndDate.DayNumber - l.StartDate.DayNumber + 1));

        var remaining = Math.Max(0, e.TotalDays - usedDays);

        return new LeaveEntitlementResponse(
            e.Id,
            e.AgentId,
            e.Agent?.FullName ?? string.Empty,
            e.Type,
            e.TotalDays,
            usedDays,
            remaining,
            e.ValidFrom,
            e.ValidTo);
    }
}
