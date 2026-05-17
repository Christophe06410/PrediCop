using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PrediCop.BackOffice.Pages.Account;

[AllowAnonymous]
public class AccessDeniedModel : PageModel;
