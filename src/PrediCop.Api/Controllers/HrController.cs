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
        return tenant?.ModuleRhEnabled ?? false;
    }

    // -------- Agent Profiles --------

    [HttpGet("profiles")]
    public async Task<ActionResult<List<AgentProfileResponse>>> GetProfiles(CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var tenant = await GetTenantAsync(ct);
        var bloodTypeEnabled = tenant?.AgentBloodTypeEnabled ?? false;

        var profiles = await db.Set<AgentProfile>()
            .Include(p => p.Agent)
            .Where(p => p.TenantId == TenantId)
            .ToListAsync(ct);

        return Ok(profiles.Select(p => MapProfileToResponse(p, bloodTypeEnabled)).ToList());
    }

    [HttpGet("profiles/{agentId:guid}")]
    public async Task<ActionResult<AgentProfileResponse>> GetProfile(Guid agentId, CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var tenant = await GetTenantAsync(ct);
        var bloodTypeEnabled = tenant?.AgentBloodTypeEnabled ?? false;

        var profile = await db.Set<AgentProfile>()
            .Include(p => p.Agent)
            .FirstOrDefaultAsync(p => p.AgentId == agentId && p.TenantId == TenantId, ct);

        if (profile is null)
            return Problem(title: "Profil agent non trouvé", statusCode: 404);

        return Ok(MapProfileToResponse(profile, bloodTypeEnabled));
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

        profile.BloodType = request.BloodType;
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

    // -------- Mappers --------

    private static AgentProfileResponse MapProfileToResponse(AgentProfile p, bool bloodTypeEnabled = true) => new(
        p.Id,
        p.AgentId,
        p.Agent?.FullName ?? string.Empty,
        p.Agent?.BadgeNumber ?? string.Empty,
        bloodTypeEnabled ? p.BloodType : null,
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
}
