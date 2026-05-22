using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrediCop.BackOffice.Pages.Public.Signup;

[AllowAnonymous]
public class IndexModel(IHttpClientFactory httpClientFactory, ILogger<IndexModel> logger) : PageModel
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly Dictionary<string, (string Label, int Price)> KnownModules = new()
    {
        ["rh"]             = ("Module RH", 39),
        ["verbalisation"]  = ("Verbalisation électronique", 49),
        ["fourriere"]      = ("Module Fourrière", 29),
        ["fleet"]          = ("Gestion de flotte", 29),
        ["logistics"]      = ("Module Logistique", 25),
        ["geofencing"]     = ("Géofencing", 19),
    };

    [BindProperty]
    public SignupInput Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public List<(string Label, int Price)> SelectedModuleLabels { get; set; } = [];

    public void OnGet(string? plan, string? period, string? modules)
    {
        Input.Plan    = plan    ?? "Essential";
        Input.Period  = period  ?? "Monthly";
        Input.Modules = modules ?? "";

        BuildModuleLabels();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        BuildModuleLabels();

        if (!ModelState.IsValid) return Page();

        if (Input.Password != Input.ConfirmPassword)
        {
            ModelState.AddModelError("Input.ConfirmPassword", "Les mots de passe ne correspondent pas.");
            return Page();
        }

        var modules = ParseModuleKeys(Input.Modules);

        var payload = new
        {
            tenantName    = Input.TenantName,
            slug          = Input.Slug,
            adminEmail    = Input.Email,
            adminPassword = Input.Password,
            plan          = Input.Plan,
            period        = Input.Period,
            modules       = string.Join(",", modules)
        };

        try
        {
            var client = httpClientFactory.CreateClient("PrediCopApiAnon");
            var response = await client.PostAsJsonAsync("/api/stripe/checkout-session", payload, JsonOpts);

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                logger.LogWarning("Checkout session error {Status}: {Body}", (int)response.StatusCode, err);
                ErrorMessage = response.StatusCode == System.Net.HttpStatusCode.Conflict
                    ? "Ce slug ou cet email est déjà utilisé."
                    : "Une erreur est survenue. Veuillez réessayer.";
                return Page();
            }

            var result = await response.Content.ReadFromJsonAsync<CheckoutResult>(JsonOpts);
            if (result?.Url is null)
            {
                ErrorMessage = "Réponse inattendue du serveur.";
                return Page();
            }

            return Redirect(result.Url);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating checkout session");
            ErrorMessage = "Impossible de joindre le serveur. Vérifiez que l'API est démarrée.";
            return Page();
        }
    }

    private void BuildModuleLabels()
    {
        SelectedModuleLabels = ParseModuleKeys(Input.Modules)
            .Where(k => KnownModules.ContainsKey(k))
            .Select(k => KnownModules[k])
            .ToList();
    }

    private static List<string> ParseModuleKeys(string? modules)
        => string.IsNullOrWhiteSpace(modules)
            ? []
            : modules.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     .Select(k => k.ToLowerInvariant())
                     .Where(k => new[] { "rh", "verbalisation", "fourriere", "fleet", "logistics", "geofencing" }.Contains(k))
                     .ToList();

    private record CheckoutResult(string? Url);
}

public class SignupInput
{
    [Required(ErrorMessage = "Le nom de la commune est obligatoire.")]
    [Display(Name = "Commune / Organisation")]
    public string TenantName { get; set; } = "";

    [Required(ErrorMessage = "Le slug est obligatoire.")]
    [RegularExpression("^[a-z0-9-]+$", ErrorMessage = "Lettres minuscules, chiffres et tirets uniquement.")]
    [Display(Name = "Identifiant unique (slug)")]
    public string Slug { get; set; } = "";

    [Required(ErrorMessage = "L'email est obligatoire.")]
    [EmailAddress(ErrorMessage = "Email invalide.")]
    [Display(Name = "Email administrateur")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Le mot de passe est obligatoire.")]
    [MinLength(8, ErrorMessage = "Au moins 8 caractères.")]
    [Display(Name = "Mot de passe")]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "La confirmation est obligatoire.")]
    [Display(Name = "Confirmer le mot de passe")]
    public string ConfirmPassword { get; set; } = "";

    public string Plan    { get; set; } = "Essential";
    public string Period  { get; set; } = "Monthly";
    public string Modules { get; set; } = "";
}
