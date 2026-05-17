using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PrediCop.BackOffice;

/// <summary>
/// Redirects to login when the cookie auth is valid but the JWT is missing from the session
/// (happens when the BackOffice or API restarts and in-memory session is cleared).
/// </summary>
public class JwtRequiredFilter(IHttpContextAccessor httpContextAccessor) : IAsyncPageFilter
{
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context)
        => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(
        PageHandlerExecutingContext context,
        PageHandlerExecutionDelegate next)
    {
        var httpContext = httpContextAccessor.HttpContext!;

        // Don't intercept Account pages (login, logout, etc.)
        var page = context.ActionDescriptor.DisplayName ?? "";
        if (page.Contains("/Account/"))
        {
            await next();
            return;
        }

        var isAuthenticated = httpContext.User.Identity?.IsAuthenticated == true;
        var hasJwt = !string.IsNullOrEmpty(httpContext.Session.GetString("JwtToken"));

        if (isAuthenticated && !hasJwt)
        {
            // Cookie is valid but JWT is gone — force a fresh login so the session gets a new token
            context.Result = new RedirectToPageResult("/Account/Login",
                new { returnUrl = httpContext.Request.Path });
            return;
        }

        await next();
    }
}
