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

        // Noms des officiers actifs sur le véhicule au moment de la vacation
        // On sélectionne ceux dont la période d'affectation chevauche la vacation
        var officerNames = await db.VehicleOfficers
            .Include(o => o.User)
            .Where(o =>
                o.VehicleId == request.VehicleId &&
                o.AssignedAt <= request.ShiftEnd &&
                (o.UnassignedAt == null || o.UnassignedAt >= request.ShiftStart))
            .Select(o => o.User.FullName)
            .ToListAsync(ct);

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

        return MapToResponse(report, vehicle.CallSign);
    }

    public async Task<ShiftReportResponse?> GetAsync(Guid id, Guid tenantId, CancellationToken ct)
    {
        var report = await db.ShiftReports
            .Include(r => r.Vehicle)
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId, ct);

        return report is null ? null : MapToResponse(report, report.Vehicle.CallSign);
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

    public async Task SignAsync(Guid id, Guid tenantId, CancellationToken ct)
    {
        var report = await db.ShiftReports
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException($"Rapport {id} introuvable.");

        if (report.IsSigned)
            throw new InvalidOperationException("Ce rapport est déjà signé.");

        report.IsSigned = true;
        report.SignedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private static ShiftReportResponse MapToResponse(ShiftReport r, string vehicleCallSign) => new(
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
        r.CreatedAt
    );
}
