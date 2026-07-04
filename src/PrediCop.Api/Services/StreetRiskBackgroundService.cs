using Microsoft.EntityFrameworkCore;
using PrediCop.Core.Interfaces;
using PrediCop.Infrastructure.Data;

namespace PrediCop.Api.Services;

public class StreetRiskBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<StreetRiskBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for app to fully start before first run
        await Task.Delay(TimeSpan.FromSeconds(90), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                logger.LogInformation("Starting nightly street risk recomputation");
                using var scope = scopeFactory.CreateScope();
                var computeService = scope.ServiceProvider.GetRequiredService<StreetRiskComputeService>();
                await computeService.ComputeAllTenantsAsync(refreshDensity: true, stoppingToken);

                var streetRiskService = scope.ServiceProvider.GetRequiredService<IStreetRiskService>();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var tenantIds = await db.Streets
                    .Select(s => s.TenantId)
                    .Distinct()
                    .ToListAsync(stoppingToken);
                foreach (var tenantId in tenantIds)
                    await streetRiskService.RecalculateAllStreetRisksAsync(tenantId, stoppingToken);

                logger.LogInformation("Nightly street risk recomputation complete");
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during nightly street risk recomputation");
            }

            // Sleep until next midnight UTC
            var now = DateTime.UtcNow;
            var nextMidnight = now.Date.AddDays(1);
            await Task.Delay(nextMidnight - now, stoppingToken);
        }
    }
}
