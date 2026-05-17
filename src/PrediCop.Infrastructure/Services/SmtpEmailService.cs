using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PrediCop.Core.Enums;
using PrediCop.Core.Interfaces;
using PrediCop.Infrastructure.Data;

namespace PrediCop.Infrastructure.Services;

public class SmtpEmailService(
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    ILogger<SmtpEmailService> logger) : IEmailService
{
    private readonly string? _smtpHost = configuration["EmailSettings:SmtpHost"];
    private readonly int _smtpPort = int.TryParse(configuration["EmailSettings:SmtpPort"], out var p) ? p : 587;
    private readonly string? _smtpUser = configuration["EmailSettings:SmtpUser"];
    private readonly string? _smtpPassword = configuration["EmailSettings:SmtpPassword"];
    private readonly string _fromAddress = configuration["EmailSettings:FromAddress"] ?? "noreply@predicop.fr";
    private readonly string _fromName = configuration["EmailSettings:FromName"] ?? "PrediCop";
    private readonly bool _enableSsl = !bool.TryParse(configuration["EmailSettings:EnableSsl"], out var ssl) || ssl;

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_smtpHost))
        {
            logger.LogWarning("EmailSettings:SmtpHost est vide — envoi email désactivé. Destinataire: {To}, Sujet: {Subject}", to, subject);
            return;
        }

        try
        {
            using var client = BuildSmtpClient();
            using var message = BuildMailMessage(to, subject, htmlBody);
            await client.SendMailAsync(message, ct);
            logger.LogInformation("Email envoyé à {To} — {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Échec de l'envoi email à {To} — {Subject}", to, subject);
        }
    }

    public async Task SendToManagersAsync(Guid tenantId, string subject, string htmlBody, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_smtpHost))
        {
            logger.LogWarning("EmailSettings:SmtpHost est vide — envoi email désactivé. TenantId: {TenantId}, Sujet: {Subject}", tenantId, subject);
            return;
        }

        try
        {
            List<string> managerEmails;

            // Crée un scope pour résoudre AppDbContext (Scoped) depuis ce Singleton
            await using (var scope = scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                managerEmails = await db.Users
                    .Where(u => u.TenantId == tenantId
                                && u.IsActive
                                && !u.IsDeleted
                                && (u.Role == UserRole.Manager || u.Role == UserRole.Admin))
                    .Select(u => u.Email)
                    .Where(e => !string.IsNullOrEmpty(e))
                    .ToListAsync(ct);
            }

            if (managerEmails.Count == 0)
            {
                logger.LogWarning("Aucun manager/admin actif trouvé pour le tenant {TenantId} — email non envoyé.", tenantId);
                return;
            }

            using var client = BuildSmtpClient();

            foreach (var email in managerEmails)
            {
                try
                {
                    using var message = BuildMailMessage(email, subject, htmlBody);
                    await client.SendMailAsync(message, ct);
                    logger.LogInformation("Email envoyé au manager {Email} — {Subject}", email, subject);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Échec de l'envoi email au manager {Email} — {Subject}", email, subject);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur dans SendToManagersAsync pour tenant {TenantId} — {Subject}", tenantId, subject);
        }
    }

    private SmtpClient BuildSmtpClient()
    {
        var client = new SmtpClient(_smtpHost, _smtpPort)
        {
            EnableSsl = _enableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false
        };

        if (!string.IsNullOrWhiteSpace(_smtpUser))
            client.Credentials = new NetworkCredential(_smtpUser, _smtpPassword);

        return client;
    }

    private MailMessage BuildMailMessage(string to, string subject, string htmlBody)
    {
        var message = new MailMessage
        {
            From = new MailAddress(_fromAddress, _fromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(to);
        return message;
    }
}
