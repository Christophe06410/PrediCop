using PrediCop.Infrastructure.Data;

namespace PrediCop.Api.Middleware;

/// <summary>
/// Middleware qui injecte le contexte utilisateur dans AppDbContext
/// pour la génération des AuditLog lors de chaque requête authentifiée.
/// Doit être placé après UseAuthentication() et UseTenantMiddleware().
/// </summary>
public class AuditContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            Guid? userId = null;
            var userIdClaim = context.User.FindFirst("userId");
            if (userIdClaim is not null && Guid.TryParse(userIdClaim.Value, out var parsedUserId))
                userId = parsedUserId;

            var userName = context.User.Identity.Name ?? string.Empty;

            Guid? tenantId = null;
            if (context.Items.TryGetValue("TenantId", out var tenantObj) && tenantObj is Guid tid)
                tenantId = tid;

            db.SetAuditContext(userId, userName, tenantId);
        }

        await next(context);
    }
}

public static class AuditContextMiddlewareExtensions
{
    public static IApplicationBuilder UseAuditContextMiddleware(this IApplicationBuilder app)
        => app.UseMiddleware<AuditContextMiddleware>();
}
