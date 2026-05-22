using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PrediCop.BackOffice.Pages.Public.Signup;

[AllowAnonymous]
public class CancelModel : PageModel
{
    public void OnGet() { }
}
