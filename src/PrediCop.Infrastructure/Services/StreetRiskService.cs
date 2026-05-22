using Microsoft.EntityFrameworkCore;
using PrediCop.Core.Entities;
using PrediCop.Core.Interfaces;
using PrediCop.Infrastructure.Data;

namespace PrediCop.Infrastructure.Services;

public class StreetRiskService(AppDbContext context) : IStreetRiskService
{
    private const int MaxTemporalBonus = 50;

    public async Task<int> CalculateCurrentRiskScoreAsync(Guid streetId, CancellationToken ct = default)
    {
        var street = await context.Streets
            .Include(s => s.RiskEvents)
            .FirstOrDefaultAsync(s => s.Id == streetId, ct)
            ?? throw new InvalidOperationException($"Street {streetId} not found.");

        return ComputeScore(street);
    }

    public async Task RecalculateAllStreetRisksAsync(Guid tenantId, CancellationToken ct = default)
    {
        var streets = await context.Streets
            .Include(s => s.RiskEvents)
            .Where(s => s.TenantId == tenantId)
            .ToListAsync(ct);

        foreach (var street in streets)
            street.CurrentRiskScore = ComputeScore(street);

        await context.SaveChangesAsync(ct);
    }

    public async Task RecordPatrolAsync(Guid streetId, Guid vehicleId, CancellationToken ct = default)
    {
        var street = await context.Streets
            .Include(s => s.RiskEvents)
            .FirstOrDefaultAsync(s => s.Id == streetId, ct)
            ?? throw new InvalidOperationException($"Street {streetId} not found.");

        var now = DateTime.UtcNow;

        var record = new PatrolRecord
        {
            StreetId = streetId,
            VehicleId = vehicleId,
            TenantId = street.TenantId,
            PatrolledAt = now,
            RiskScoreAtPatrol = street.CurrentRiskScore
        };

        street.LastPatrolledAt = now;
        street.CurrentRiskScore = street.BaseRiskScore;

        context.PatrolRecords.Add(record);
        await context.SaveChangesAsync(ct);
    }

    public async Task<IEnumerable<Street>> GetStreetsOrderedByPriorityAsync(Guid tenantId, int count = 10, CancellationToken ct = default)
    {
        return await context.Streets
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.CurrentRiskScore)
            .Take(count)
            .ToListAsync(ct);
    }

    private static int ComputeScore(Street street)
    {
        var now = DateTime.UtcNow;

        var activeEventsScore = street.RiskEvents
            .Where(re => re.ExpiresAt > now)
            .Sum(re => re.RiskPoints);

        var reference = street.LastPatrolledAt ?? street.CreatedAt;
        var hoursSincePatrol = (now - reference).TotalHours;
        var temporalBonus = Math.Min((int)(hoursSincePatrol * street.RiskGrowthRatePerHour), MaxTemporalBonus);

        return Math.Min(street.BaseRiskScore + activeEventsScore + temporalBonus, 100);
    }
}
