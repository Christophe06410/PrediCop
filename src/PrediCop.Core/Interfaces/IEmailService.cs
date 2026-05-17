namespace PrediCop.Core.Interfaces;

public interface IEmailService
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
    Task SendToManagersAsync(Guid tenantId, string subject, string htmlBody, CancellationToken ct = default);
}
