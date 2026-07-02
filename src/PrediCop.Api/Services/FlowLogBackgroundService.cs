using PrediCop.Infrastructure.Data;
using PrediCop.Infrastructure.Services;

namespace PrediCop.Api.Services;

/// <summary>
/// Draine la file <see cref="FlowLogService"/> et persiste les logs techniques par petits lots.
/// Toute exception est avalée : le logging ne doit jamais faire tomber l'application.
/// </summary>
public sealed class FlowLogBackgroundService(
    FlowLogService queue,
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var first in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                db.FlowLogs.Add(first);
                var batch = 1;
                while (batch < 200 && queue.Reader.TryRead(out var more))
                {
                    db.FlowLogs.Add(more);
                    batch++;
                }

                await db.SaveChangesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // Ne jamais propager : un échec d'écriture de log ne doit rien casser.
            }
        }
    }
}
