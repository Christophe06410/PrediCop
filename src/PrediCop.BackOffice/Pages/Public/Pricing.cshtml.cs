using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PrediCop.BackOffice.Pages.Public;

[AllowAnonymous]
public class PricingModel : PageModel
{
    public void OnGet() { }
}
