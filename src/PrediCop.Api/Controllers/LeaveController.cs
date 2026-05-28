using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrediCop.Core.DTOs;
using PrediCop.Core.Entities;
using PrediCop.Core.Enums;
using PrediCop.Infrastructure.Data;

namespace PrediCop.Api.Controllers;

[ApiController]
[Route("api/leave")]
[Authorize]
public class LeaveController(AppDbContext db) : ControllerBase
{
    private Guid TenantId => (Guid)HttpContext.Items["TenantId"]!;

    private Guid CurrentUserId =>
        Guid.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : Guid.Empty;

    private async Task<bool> IsModuleEnabledAsync(CancellationToken ct)
    {
        var tenant = await db.Tenants.FindAsync([TenantId], ct);
        return tenant?.ModulePlanningEnabled ?? false;
    }

    // GET /api/leave/balance — solde par type pour l'agent connecté
    [HttpGet("balance")]
    public async Task<ActionResult<List<AgentLeaveBalanceResponse>>> GetBalance(CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var agentId = CurrentUserId;
        if (agentId == Guid.Empty)
            return Problem(title: "Identité non reconnue", statusCode: 401);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Droits valides à la date d'aujourd'hui (ou à cheval)
        var entitlements = await db.LeaveEntitlements
            .Where(e => e.TenantId == TenantId
                     && e.AgentId == agentId
                     && e.ValidTo >= today)
            .OrderBy(e => e.Type)
            .ToListAsync(ct);

        var approvedLeaves = await db.Leaves
            .Where(l => l.TenantId == TenantId
                     && l.AgentId == agentId
                     && l.Status == LeaveStatus.Approved)
            .ToListAsync(ct);

        var result = new List<AgentLeaveBalanceResponse>();

        // Ajouter les types avec droit défini
        foreach (var e in entitlements)
        {
            var usedDays = approvedLeaves
                .Where(l => l.Type == e.Type
                         && l.StartDate >= e.ValidFrom
                         && l.EndDate <= e.ValidTo)
                .Sum(l => (decimal)(l.EndDate.DayNumber - l.StartDate.DayNumber + 1));

            var remaining = Math.Max(0, e.TotalDays - usedDays);

            result.Add(new AgentLeaveBalanceResponse(
                e.Type,
                e.TotalDays,
                usedDays,
                remaining,
                e.ValidFrom,
                e.ValidTo));
        }

        return Ok(result);
    }

    // GET /api/leave/requests — ses propres demandes
    [HttpGet("requests")]
    public async Task<ActionResult<List<LeaveResponse>>> GetMyRequests(CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var agentId = CurrentUserId;
        if (agentId == Guid.Empty)
            return Problem(title: "Identité non reconnue", statusCode: 401);

        var leaves = await db.Leaves
            .Include(l => l.Agent)
            .Where(l => l.TenantId == TenantId && l.AgentId == agentId)
            .OrderByDescending(l => l.RequestedAt)
            .ToListAsync(ct);

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

    // POST /api/leave/requests — créer une demande pour soi-même
    [HttpPost("requests")]
    public async Task<ActionResult<LeaveResponse>> CreateRequest(
        [FromBody] CreateLeaveRequest request,
        CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var agentId = CurrentUserId;
        if (agentId == Guid.Empty)
            return Problem(title: "Identité non reconnue", statusCode: 401);

        var agentExists = await db.Users.AnyAsync(u => u.Id == agentId && u.TenantId == TenantId, ct);
        if (!agentExists)
            return Problem(title: "Agent non trouvé", statusCode: 404);

        var leave = new Leave
        {
            TenantId = TenantId,
            AgentId = agentId,  // forcé sur l'agent connecté
            Type = request.Type,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Notes = request.Notes,
            Status = LeaveStatus.Pending,
            RequestedAt = DateTime.UtcNow
        };

        db.Leaves.Add(leave);
        await db.SaveChangesAsync(ct);

        await db.Entry(leave).Reference(l => l.Agent).LoadAsync(ct);

        return Ok(MapLeaveToResponse(leave, new Dictionary<Guid, string>()));
    }

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
}
