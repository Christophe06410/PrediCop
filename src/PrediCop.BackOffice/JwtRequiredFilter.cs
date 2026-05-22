using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PrediCop.BackOffice;

public class JwtRequiredFilter(IHttpContextAccessor httpContextAccessor) : IAsyncPageFilter
{
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context)
        => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(
        PageHandlerExecutingContext context,
        PageHandlerExecutionDelegate next)
    {
        var httpContext = httpContextAccessor.HttpContext!;
        var page = context.ActionDescriptor.DisplayName ?? "";

        if (page.Contains("/Account/") || page.Contains("/Public/") || page.Contains("/Subscription/Suspended"))
        {
            await next();
            return;
        }

        var isAuthenticated = httpContext.User.Identity?.IsAuthenticated == true;
        var jwtToken = httpContext.Session.GetString("JwtToken");
        var hasJwt = !string.IsNullOrEmpty(jwtToken);

        if (isAuthenticated && !hasJwt)
        {
            context.Result = new RedirectToPageResult("/Account/Login",
                new { returnUrl = httpContext.Request.PathBase + httpContext.Request.Path + httpContext.Request.QueryString });
            return;
        }

        if (isAuthenticated && hasJwt)
        {
            var handler = new JwtSecurityTokenHandler();
            if (handler.CanReadToken(jwtToken))
            {
                var token = handler.ReadJwtToken(jwtToken);
                var subStatus = token.Claims.FirstOrDefault(c => c.Type == "subscriptionStatus")?.Value;
                if (subStatus is "PastDue" or "Cancelled")
                {
                    context.Result = new RedirectToPageResult("/Subscription/Suspended");
                    return;
                }
            }
        }

        await next();
    }
}
