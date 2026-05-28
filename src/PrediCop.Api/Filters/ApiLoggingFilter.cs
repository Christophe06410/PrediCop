using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PrediCop.Api.Filters;

/// <summary>
/// Filtre global qui logue l'entrée et la sortie de chaque endpoint REST.
/// - Entrée  : méthode HTTP, route, paramètres de route, query string, corps de la requête (POST/PATCH/PUT), userId + tenantId
/// - Sortie  : status code HTTP, durée d'exécution en ms
/// - Niveau  : Information (2xx), Warning (4xx), Error (5xx)
/// - Les paramètres sensibles (password, token, secret) sont masqués.
/// </summary>
public sealed class ApiLoggingFilter(ILogger<ApiLoggingFilter> logger) : IAsyncActionFilter
{
    // Noms de propriétés sensibles à masquer dans le corps de la requête.
    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "token", "secret", "refreshtoken", "accesstoken",
        "currentpassword", "newpassword", "confirmpassword"
    };

    // Méthodes HTTP dont on logue le corps.
    private static readonly HashSet<string> BodyMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "POST", "PUT", "PATCH"
    };

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        var sw = Stopwatch.StartNew();

        // ---- Identité ----
        var user = context.HttpContext.User;
        var userId   = user.FindFirst("sub")?.Value
                    ?? user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                    ?? "anonymous";
        var tenantId = user.FindFirst("tenantId")?.Value ?? "-";

        // ---- Route et query string ----
        var routeValues  = FormatRouteValues(context.ActionArguments, context.RouteData.Values);
        var queryString  = request.QueryString.HasValue ? request.QueryString.Value : string.Empty;

        // ---- Corps de la requête ----
        string? requestBody = null;
        if (BodyMethods.Contains(request.Method))
            requestBody = FormatActionArguments(context.ActionArguments);

        logger.LogInformation(
            "[API IN]  {Method} {Path}{QueryString} | route={Route} | user={UserId} tenant={TenantId}{BodyPart}",
            request.Method,
            request.Path,
            queryString,
            routeValues,
            userId,
            tenantId,
            requestBody is not null ? $" | body={requestBody}" : string.Empty);

        // ---- Exécution de l'action ----
        var executed = await next();

        sw.Stop();

        // ---- Status code ----
        int statusCode = executed.Result switch
        {
            Microsoft.AspNetCore.Mvc.ObjectResult   r => r.StatusCode ?? 200,
            Microsoft.AspNetCore.Mvc.StatusCodeResult r => r.StatusCode,
            Microsoft.AspNetCore.Mvc.EmptyResult      _ => 200,
            _                                          => context.HttpContext.Response.StatusCode
        };

        var durationMs = sw.ElapsedMilliseconds;

        if (executed.Exception is not null && !executed.ExceptionHandled)
        {
            logger.LogError(
                executed.Exception,
                "[API OUT] {Method} {Path} => {StatusCode} | {DurationMs}ms | user={UserId} tenant={TenantId}",
                request.Method, request.Path, 500, durationMs, userId, tenantId);
            return;
        }

        var logLevel = statusCode switch
        {
            >= 500 => LogLevel.Error,
            >= 400 => LogLevel.Warning,
            _      => LogLevel.Information
        };

        logger.Log(
            logLevel,
            "[API OUT] {Method} {Path} => {StatusCode} | {DurationMs}ms | user={UserId} tenant={TenantId}",
            request.Method, request.Path, statusCode, durationMs, userId, tenantId);
    }

    // ---- Helpers ----

    /// <summary>
    /// Formate les paramètres de route (issus de RouteData) sans dupliquer les valeurs déjà dans ActionArguments.
    /// </summary>
    private static string FormatRouteValues(
        IDictionary<string, object?> actionArguments,
        Microsoft.AspNetCore.Routing.RouteValueDictionary routeData)
    {
        var parts = new List<string>();
        foreach (var (key, value) in routeData)
        {
            // On ignore controller/action qui sont du bruit
            if (key is "controller" or "action")
                continue;

            var display = IsSensitive(key) ? "***" : value?.ToString() ?? string.Empty;
            parts.Add($"{key}={display}");
        }
        return parts.Count > 0 ? string.Join(", ", parts) : "(none)";
    }

    /// <summary>
    /// Sérialise les ActionArguments en JSON compact en masquant les clés sensibles.
    /// </summary>
    private static string FormatActionArguments(IDictionary<string, object?> args)
    {
        if (args.Count == 0)
            return "{}";

        // On ne garde que les arguments complexes (objets / collections), pas les scalaires de route
        var filtered = new Dictionary<string, object?>();
        foreach (var (key, value) in args)
        {
            if (value is null || value is string || value.GetType().IsPrimitive || value is Guid || value is DateTime)
                continue;

            filtered[key] = SanitizeValue(value);
        }

        if (filtered.Count == 0)
            return "{}";

        try
        {
            return JsonSerializer.Serialize(filtered, new JsonSerializerOptions
            {
                WriteIndented = false,
                MaxDepth = 5
            });
        }
        catch
        {
            return "(non sérialisable)";
        }
    }

    /// <summary>
    /// Parcourt récursivement un objet et remplace les valeurs sensibles par "***".
    /// Fonctionne avec les types anonymes, les DTOs et les dictionnaires.
    /// </summary>
    private static object? SanitizeValue(object? value)
    {
        if (value is null)
            return null;

        // Dictionnaire générique
        if (value is IDictionary<string, object?> dict)
        {
            var result = new Dictionary<string, object?>();
            foreach (var (k, v) in dict)
                result[k] = IsSensitive(k) ? "***" : SanitizeValue(v);
            return result;
        }

        // On réfléchit sur les propriétés publiques du type concret
        var type = value.GetType();
        if (type.IsPrimitive || type == typeof(string) || type == typeof(Guid) || type == typeof(DateTime))
            return value;

        var props = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (props.Length == 0)
            return value;

        var sanitized = new Dictionary<string, object?>(props.Length);
        foreach (var prop in props)
        {
            if (!prop.CanRead) continue;
            object? propValue;
            try { propValue = prop.GetValue(value); }
            catch { propValue = null; }

            sanitized[prop.Name] = IsSensitive(prop.Name) ? "***" : SanitizeValue(propValue);
        }
        return sanitized;
    }

    private static bool IsSensitive(string name) => SensitiveKeys.Contains(name);
}
