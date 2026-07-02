using System.Threading.Channels;
using PrediCop.Core.Entities;
using PrediCop.Core.Interfaces;

namespace PrediCop.Infrastructure.Services;

/// <summary>
/// File d'attente en mémoire (bornée) des logs techniques. L'écriture DB réelle est
/// effectuée par <see cref="FlowLogBackgroundService"/> pour ne jamais bloquer le thread appelant
/// (requête HTTP ou Hub SignalR). En cas de saturation, les plus anciens sont abandonnés.
/// </summary>
public sealed class FlowLogService : IFlowLogService
{
    private readonly Channel<FlowLog> _channel = Channel.CreateBounded<FlowLog>(
        new BoundedChannelOptions(10_000) { FullMode = BoundedChannelFullMode.DropOldest });

    public ChannelReader<FlowLog> Reader => _channel.Reader;

    public void Log(string source, string level, string category, string message,
        Guid? tenantId = null, Guid? userId = null, string? context = null)
    {
        _channel.Writer.TryWrite(new FlowLog
        {
            Timestamp = DateTime.UtcNow,
            Source = Truncate(source, 20) ?? "Server",
            Level = Truncate(level, 20) ?? "Information",
            Category = Truncate(category, 100) ?? string.Empty,
            Message = Truncate(message, 4000) ?? string.Empty,
            TenantId = tenantId,
            UserId = userId,
            Context = Truncate(context, 1000)
        });
    }

    private static string? Truncate(string? value, int max) =>
        value is null ? null : value.Length <= max ? value : value[..max];
}
