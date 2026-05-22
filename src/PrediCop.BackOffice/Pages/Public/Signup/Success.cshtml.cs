using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PrediCop.BackOffice.Pages.Public.Signup;

[AllowAnonymous]
public class SuccessModel : PageModel
{
    public void OnGet(string? session_id) { }
}
