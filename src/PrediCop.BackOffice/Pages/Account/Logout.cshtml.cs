using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PrediCop.BackOffice.Pages.Account;

public class LogoutModel : PageModel
{
    public async Task<IActionResult> OnGetAsync() => await SignOutAsync();

    public async Task<IActionResult> OnPostAsync() => await SignOutAsync();

    private async Task<IActionResult> SignOutAsync()
    {
        HttpContext.Session.Clear();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Account/Login");
    }
}
