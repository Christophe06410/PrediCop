using Microsoft.EntityFrameworkCore;
using PrediCop.Core.DTOs;
using PrediCop.Core.Entities;
using PrediCop.Core.Enums;
using PrediCop.Core.Interfaces;
using PrediCop.Infrastructure.Data;

namespace PrediCop.Infrastructure.Services;

public class ShiftReportService(AppDbContext db) : IShiftReportService
{
    public async Task<ShiftReportResponse> GenerateAsync(
        CreateShiftReportRequest request,
        Guid tenantId,
        CancellationToken ct)
    {
        // Vérifie que le véhicule appartient au tenant
        var vehicle = await db.PatrolVehicles
            .Include(v => v.Officers.Where(o => o.IsActive))
                .ThenInclude(o => o.User)
            .FirstOrDefaultAsync(v => v.Id == request.VehicleId && v.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException($"Véhicule {request.VehicleId} introuvable.");

        // Officiers présents sur le véhicule pendant la vacation
        var officers = await db.VehicleOfficers
            .Include(o => o.User)
            .Where(o =>
                o.VehicleId == request.VehicleId &&
                o.AssignedAt <= request.ShiftEnd &&
                (o.UnassignedAt == null || o.UnassignedAt >= request.ShiftStart))
            .ToListAsync(ct);

        var officerNames = officers.Select(o => o.User.FullName).ToList();
        var officerIds   = officers.Select(o => o.UserId).ToList();

        // Missions du véhicule pendant la vacation (via MissionAssignments acceptés/en cours/terminés)
        var missionIds = await db.MissionAssignments
            .Where(a =>
                a.VehicleId == request.VehicleId &&
                a.ProposedAt >= request.ShiftStart &&
                a.ProposedAt <= request.ShiftEnd)
            .Select(a => a.MissionId)
            .Distinct()
            .ToListAsync(ct);

        var missions = await db.Missions
            .Where(m => missionIds.Contains(m.Id) && m.TenantId == tenantId)
            .ToListAsync(ct);

        var missionCount = missions.Count;
        var completedMissionCount = missions.Count(m => m.Status == MissionStatus.Completed);
        var refusedMissionCount = await db.MissionAssignments
            .Where(a =>
                a.VehicleId == request.VehicleId &&
                a.ProposedAt >= request.ShiftStart &&
                a.ProposedAt <= request.ShiftEnd &&
                a.Status == MissionStatus.Refused)
            .CountAsync(ct);

        // PatrolRecords du véhicule pendant la vacation
        var patrolRecordCount = await db.PatrolRecords
            .Where(pr =>
                pr.VehicleId == request.VehicleId &&
                pr.TenantId == tenantId &&
                pr.PatrolledAt >= request.ShiftStart &&
                pr.PatrolledAt <= request.ShiftEnd)
            .CountAsync(ct);

        var estimatedKm = patrolRecordCount * 0.5;

        // TrackingDocuments créés pendant la vacation pour les missions de ce véhicule
        var documentCount = await db.TrackingDocuments
            .Where(td =>
                td.TenantId == tenantId &&
                missionIds.Contains(td.MissionId) &&
                td.CreatedAt >= request.ShiftStart &&
                td.CreatedAt <= request.ShiftEnd)
            .CountAsync(ct);

        var report = new ShiftReport
        {
            TenantId = tenantId,
            VehicleId = request.VehicleId,
            ShiftStart = request.ShiftStart,
            ShiftEnd = request.ShiftEnd,
            OfficerNames = string.Join(", ", officerNames),
            MissionCount = missionCount,
            CompletedMissionCount = completedMissionCount,
            RefusedMissionCount = refusedMissionCount,
            PatrolRecordCount = patrolRecordCount,
            EstimatedKm = estimatedKm,
            DocumentCount = documentCount,
            Notes = request.Notes,
            IsSigned = false
        };

        db.ShiftReports.Add(report);
        await db.SaveChangesAsync(ct);

        return MapToResponse(report, vehicle.CallSign, officerIds);
    }

    public async Task<ShiftReportResponse?> GetAsync(Guid id, Guid tenantId, CancellationToken ct)
    {
        var report = await db.ShiftReports
            .Include(r => r.Vehicle)
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId, ct);

        if (report is null) return null;

        var authorizedSignerIds = await db.VehicleOfficers
            .Where(o =>
                o.VehicleId == report.VehicleId &&
                o.AssignedAt <= report.ShiftEnd &&
                (o.UnassignedAt == null || o.UnassignedAt >= report.ShiftStart))
            .Select(o => o.UserId)
            .ToListAsync(ct);

        return MapToResponse(report, report.Vehicle.CallSign, authorizedSignerIds);
    }

    public async Task<(List<ShiftReportResponse> Items, int Total)> GetListAsync(
        Guid tenantId,
        Guid? vehicleId,
        DateTime? dateFrom,
        DateTime? dateTo,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        var query = db.ShiftReports
            .Include(r => r.Vehicle)
            .Where(r => r.TenantId == tenantId);

        if (vehicleId.HasValue)
            query = query.Where(r => r.VehicleId == vehicleId.Value);

        if (dateFrom.HasValue)
            query = query.Where(r => r.ShiftStart >= dateFrom.Value.ToUniversalTime());

        if (dateTo.HasValue)
            query = query.Where(r => r.ShiftStart <= dateTo.Value.ToUniversalTime().AddDays(1).AddTicks(-1));

        var total = await query.CountAsync(ct);

        var reports = await query
            .OrderByDescending(r => r.ShiftStart)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (reports.Select(r => MapToResponse(r, r.Vehicle.CallSign)).ToList(), total);
    }

    public async Task SignAsync(Guid id, Guid tenantId, Guid signerUserId, CancellationToken ct)
    {
        var report = await db.ShiftReports
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException($"Rapport {id} introuvable.");

        if (report.IsSigned)
            throw new InvalidOperationException("Ce rapport est déjà signé.");

        // Vérifier que le signataire était bien affecté à ce véhicule pendant la vacation
        var isAuthorized = await db.VehicleOfficers
            .AnyAsync(o =>
                o.VehicleId == report.VehicleId &&
                o.UserId == signerUserId &&
                o.AssignedAt <= report.ShiftEnd &&
                (o.UnassignedAt == null || o.UnassignedAt >= report.ShiftStart), ct);

        if (!isAuthorized)
            throw new InvalidOperationException(
                "Vous n'êtes pas autorisé à signer ce rapport. Seuls les agents ayant participé à cette vacation peuvent signer.");

        var signerName = await db.Users
            .Where(u => u.Id == signerUserId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(ct) ?? "Inconnu";

        report.IsSigned = true;
        report.SignedAt = DateTime.UtcNow;
        report.SignedByUserId = signerUserId;
        report.SignedByName = signerName;
        await db.SaveChangesAsync(ct);
    }

    public async Task<ShiftReportResponse> GenerateForAgentAsync(
        Guid agentId,
        CreateMyShiftReportRequest request,
        Guid tenantId,
        CancellationToken ct)
    {
        var assignment = await db.VehicleOfficers
            .Include(vo => vo.Vehicle)
            .Where(vo =>
                vo.UserId == agentId &&
                vo.Vehicle.TenantId == tenantId &&
                vo.AssignedAt <= request.ShiftEnd &&
                (vo.UnassignedAt == null || vo.UnassignedAt >= request.ShiftStart))
            .OrderByDescending(vo => vo.AssignedAt)
            .FirstOrDefaultAsync(ct);

        if (assignment is null)
            throw new InvalidOperationException(
                "Aucune affectation à un véhicule trouvée pour cette plage horaire. Vérifiez que vous avez bien activé votre patrouille.");

        return await GenerateAsync(
            new CreateShiftReportRequest(assignment.VehicleId, request.ShiftStart, request.ShiftEnd, request.Notes),
            tenantId, ct);
    }

    private static ShiftReportResponse MapToResponse(
        ShiftReport r, string vehicleCallSign, List<Guid>? authorizedSignerIds = null) => new(
        r.Id,
        r.VehicleId,
        vehicleCallSign,
        r.ShiftStart,
        r.ShiftEnd,
        r.OfficerNames,
        r.MissionCount,
        r.CompletedMissionCount,
        r.RefusedMissionCount,
        r.PatrolRecordCount,
        r.EstimatedKm,
        r.DocumentCount,
        r.Notes,
        r.IsSigned,
        r.SignedAt,
        r.CreatedAt,
        r.SignedByName,
        authorizedSignerIds ?? []
    );
}
