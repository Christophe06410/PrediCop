using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrediCop.Core.DTOs;
using PrediCop.Core.Entities;
using PrediCop.Infrastructure.Data;

namespace PrediCop.Api.Controllers;

[ApiController]
[Route("api/mobile-logs")]
public class MobileLogsController(AppDbContext db, ILogger<MobileLogsController> logger) : ControllerBase
{
    /// <summary>
    /// Reçoit un rapport d'erreur grave depuis l'application mobile.
    /// Accepte les appels anonymes (erreurs avant login) et authentifiés.
    /// </summary>
    [HttpPost("error")]
    [AllowAnonymous]
    public async Task<IActionResult> ReportError([FromBody] MobileErrorLogRequest request, CancellationToken ct)
    {
        Guid? tenantId = null;
        Guid? userId = null;

        if (User.Identity?.IsAuthenticated == true)
        {
            if (Guid.TryParse(User.FindFirstValue("tenantId"), out var tid)) tenantId = tid;
            if (Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid)) userId = uid;
        }

        var entry = new MobileErrorLog
        {
            TenantId      = tenantId,
            UserId        = userId,
            AppVersion    = request.AppVersion,
            Platform      = request.Platform,
            DeviceModel   = request.DeviceModel,
            Category      = request.Category,
            Message       = request.Message,
            StackTrace    = request.StackTrace,
            Endpoint      = request.Endpoint,
            HttpStatusCode = request.HttpStatusCode,
        };

        db.MobileErrorLogs.Add(entry);
        await db.SaveChangesAsync(ct);

        logger.LogWarning(
            "[MobileError] {Platform} v{Version} | {Category} | {Message} | TenantId={TenantId}",
            request.Platform, request.AppVersion, request.Category, request.Message, tenantId);

        return NoContent();
    }
}
