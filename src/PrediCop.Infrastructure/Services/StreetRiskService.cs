using Microsoft.EntityFrameworkCore;
using PrediCop.Core.Entities;
using PrediCop.Core.Enums;
using PrediCop.Core.Interfaces;
using PrediCop.Infrastructure.Data;

namespace PrediCop.Infrastructure.Services;

public class StreetRiskService(AppDbContext context) : IStreetRiskService
{
    private const int MaxTemporalBonus = 80;

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
            .Where(re => IsEventActive(re, now))
            .Sum(re => re.RiskPoints);

        var reference = street.LastPatrolledAt ?? street.CreatedAt;
        var hoursSincePatrol = (now - reference).TotalHours;

        var rate = IsNightTime(now.Hour, street.NightStartHour, street.NightEndHour)
            ? street.NightRiskGrowthRatePerWeek
            : street.RiskGrowthRatePerWeek;

        var temporalBonus = Math.Min((int)(hoursSincePatrol * rate / 168.0), MaxTemporalBonus);

        return Math.Min(street.BaseRiskScore + activeEventsScore + temporalBonus, 100);
    }

    public static bool IsEventActive(StreetRiskEvent re, DateTime now)
    {
        if (re.RecurrenceType == RecurrenceType.None)
            return re.ExpiresAt > now;

        if (now < re.EventDate) return false;
        if (re.RecurrenceEndDate.HasValue && now > re.RecurrenceEndDate.Value) return false;

        var duration = re.ExpiresAt - re.EventDate;

        return re.RecurrenceType switch
        {
            RecurrenceType.Daily   => IsInFixedCycle(now, re.EventDate, duration, TimeSpan.FromDays(1)),
            RecurrenceType.Weekly  => IsInFixedCycle(now, re.EventDate, duration, TimeSpan.FromDays(7)),
            RecurrenceType.Monthly => IsInCalendarCycle(now, re.EventDate, duration, 1),
            RecurrenceType.Yearly  => IsInCalendarCycle(now, re.EventDate, duration, 12),
            _                      => re.ExpiresAt > now
        };
    }

    // Checks fixed-period cycles (daily, weekly) using modular arithmetic.
    private static bool IsInFixedCycle(DateTime now, DateTime start, TimeSpan duration, TimeSpan period)
    {
        var elapsed = now - start;
        var posInCycle = TimeSpan.FromTicks(elapsed.Ticks % period.Ticks);
        return posInCycle < duration;
    }

    // Checks calendar cycles (monthly, yearly) where the period length varies.
    private static bool IsInCalendarCycle(DateTime now, DateTime start, TimeSpan duration, int monthsPerCycle)
    {
        // Estimate the cycle index from elapsed months, then check ±1 to handle edge cases.
        int totalMonths = (now.Year - start.Year) * 12 + (now.Month - start.Month);
        int cycle = Math.Max(0, totalMonths / monthsPerCycle);
        for (int i = Math.Max(0, cycle - 1); i <= cycle + 1; i++)
        {
            var cycleStart = start.AddMonths(i * monthsPerCycle);
            if (cycleStart <= now && now < cycleStart + duration)
                return true;
        }
        return false;
    }

    // Returns true if the given UTC hour falls within the night window [nightStart, nightEnd[.
    // Handles wrap-around midnight (e.g. 22→6).
    private static bool IsNightTime(int hour, int nightStart, int nightEnd)
    {
        if (nightStart == nightEnd) return false;
        return nightStart < nightEnd
            ? hour >= nightStart && hour < nightEnd
            : hour >= nightStart || hour < nightEnd;
    }
}
