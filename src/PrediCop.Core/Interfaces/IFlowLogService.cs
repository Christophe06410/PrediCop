namespace PrediCop.Core.Interfaces;

/// <summary>
/// Écriture non bloquante de logs techniques dans la table FlowLogs (diagnostic des flux).
/// L'appel est "fire and forget" : il n'échoue jamais et n'attend pas l'écriture DB.
/// </summary>
public interface IFlowLogService
{
    void Log(string source, string level, string category, string message,
        Guid? tenantId = null, Guid? userId = null, string? context = null);
}
