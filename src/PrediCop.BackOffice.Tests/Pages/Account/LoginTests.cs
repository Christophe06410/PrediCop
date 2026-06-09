using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Moq;
using PrediCop.BackOffice.Pages.Account;
using PrediCop.BackOffice.Tests.Helpers;
using PrediCop.Core.DTOs;
using PrediCop.Core.Enums;
using RichardSzalay.MockHttp;
using Xunit;

namespace PrediCop.BackOffice.Tests.Pages.Account;

public class LoginTests
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static LoginModel CreateModel(IHttpClientFactory factory)
    {
        var model = new LoginModel(factory, MockHttpHelper.NullLogger<LoginModel>())
            .WithPageContext();

        // Session stub
        var session = new Mock<ISession>();
        var sessionData = new Dictionary<string, byte[]>();
        session.Setup(s => s.Set(It.IsAny<string>(), It.IsAny<byte[]>()))
               .Callback<string, byte[]>((k, v) => sessionData[k] = v);
        session.Setup(s => s.TryGetValue(It.IsAny<string>(), out It.Ref<byte[]?>.IsAny))
               .Returns((string k, out byte[]? v) =>
               {
                   var found = sessionData.TryGetValue(k, out var val);
                   v = val;
                   return found;
               });

        // Auth service stub (needed by SignInAsync on OnPostAsync success path)
        var authService = new Mock<IAuthenticationService>();
        authService
            .Setup(a => a.SignInAsync(It.IsAny<HttpContext>(), It.IsAny<string>(),
                It.IsAny<System.Security.Claims.ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>()))
            .Returns(Task.CompletedTask);

        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(sp => sp.GetService(typeof(IAuthenticationService)))
                       .Returns(authService.Object);

        model.PageContext.HttpContext.RequestServices = serviceProvider.Object;
        model.PageContext.HttpContext.Session = session.Object;

        return model;
    }

    // ── OnGetAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task OnGetAsync_TenantsApiReturnsData_PopulatesTenantsList()
    {
        var (handler, factory) = MockHttpHelper.Create();
        var tenants = new List<TenantSummaryDto>
        {
            new() { Id = Guid.NewGuid(), Name = "Toulouse", Slug = "toulouse" },
            new() { Id = Guid.NewGuid(), Name = "Lyon",     Slug = "lyon"     },
        };
        handler.When("https://api.test/api/auth/tenants")
               .Respond("application/json", JsonSerializer.Serialize(tenants, JsonOpts));

        var model = CreateModel(factory);
        await model.OnGetAsync(returnUrl: null);

        Assert.Equal(2, model.Tenants.Count);
        Assert.Equal("Toulouse", model.Tenants[0].Name);
    }

    [Fact]
    public async Task OnGetAsync_TenantsApiFails_TenantsListIsEmpty()
    {
        var (handler, factory) = MockHttpHelper.Create();
        handler.When("*").Respond(HttpStatusCode.InternalServerError);

        var model = CreateModel(factory);
        var ex = await Record.ExceptionAsync(() => model.OnGetAsync(returnUrl: null));

        Assert.Null(ex);
        Assert.Empty(model.Tenants);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsPage()
    {
        var (handler, factory) = MockHttpHelper.Create();
        handler.When("https://api.test/api/auth/tenants")
               .Respond("application/json", "[]");

        var model = CreateModel(factory);
        var result = await model.OnGetAsync(returnUrl: null);

        Assert.IsType<PageResult>(result);
    }

    // ── OnPostAsync — validation ─────────────────────────────────────────────

    [Fact]
    public async Task OnPostAsync_MissingEmail_ReturnsPageWithError()
    {
        var (handler, factory) = MockHttpHelper.Create();
        handler.When("*").Respond("application/json", "[]");

        var model = CreateModel(factory);
        model.Email    = "";
        model.Password = "pass";
        model.CitySlug = "toulouse";

        var result = await model.OnPostAsync(returnUrl: null);

        Assert.IsType<PageResult>(result);
        Assert.NotNull(model.ErrorMessage);
    }

    [Fact]
    public async Task OnPostAsync_MissingCitySlug_ReturnsPageWithError()
    {
        var (handler, factory) = MockHttpHelper.Create();
        handler.When("*").Respond("application/json", "[]");

        var model = CreateModel(factory);
        model.Email    = "agent@pm.fr";
        model.Password = "pass";
        model.CitySlug = "";

        var result = await model.OnPostAsync(returnUrl: null);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Veuillez sélectionner votre ville.", model.ErrorMessage);
    }

    // ── OnPostAsync — API failures ────────────────────────────────────────────

    [Fact]
    public async Task OnPostAsync_ApiReturnsUnauthorized_ReturnsPageWithError()
    {
        var (handler, factory) = MockHttpHelper.Create();
        handler.When("https://api.test/api/auth/tenants").Respond("application/json", "[]");
        handler.When("https://api.test/api/auth/login").Respond(HttpStatusCode.Unauthorized);

        var model = CreateModel(factory);
        model.Email    = "agent@pm.fr";
        model.Password = "wrong";
        model.CitySlug = "toulouse";

        var result = await model.OnPostAsync(returnUrl: null);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Identifiants invalides ou ville incorrecte.", model.ErrorMessage);
    }

    [Fact]
    public async Task OnPostAsync_ApiUnreachable_ReturnsPageWithError()
    {
        var (handler, factory) = MockHttpHelper.Create();
        handler.When("https://api.test/api/auth/tenants").Respond("application/json", "[]");
        handler.When("https://api.test/api/auth/login")
               .Throw(new HttpRequestException("Connection refused"));

        var model = CreateModel(factory);
        model.Email    = "agent@pm.fr";
        model.Password = "pass";
        model.CitySlug = "toulouse";

        var result = await model.OnPostAsync(returnUrl: null);

        Assert.IsType<PageResult>(result);
        Assert.NotNull(model.ErrorMessage);
    }

    // ── OnPostAsync — success ────────────────────────────────────────────────

    [Fact]
    public async Task OnPostAsync_ValidCredentials_RedirectsToHome()
    {
        var (handler, factory) = MockHttpHelper.Create();
        handler.When("https://api.test/api/auth/tenants").Respond("application/json", "[]");

        var loginResponse = new LoginResponse
        {
            AccessToken = "eyJhbGciOiJIUzI1NiJ9.test.sig",
            User = new UserResponse
            {
                Id         = Guid.NewGuid(),
                FullName   = "Jean Dupont",
                Email      = "agent@pm.fr",
                BadgeNumber = "PM-001",
                Role       = UserRole.Officer,
                TenantId   = Guid.NewGuid(),
                TenantName = "Toulouse",
                TenantSlug = "toulouse",
                IsActive   = true,
            }
        };
        handler.When("https://api.test/api/auth/login")
               .Respond("application/json", JsonSerializer.Serialize(loginResponse, JsonOpts));

        var model = CreateModel(factory);
        model.Email    = "agent@pm.fr";
        model.Password = "Officer123!";
        model.CitySlug = "toulouse";

        var result = await model.OnPostAsync(returnUrl: null);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Home/Index", redirect.PageName);
    }

    [Fact]
    public async Task OnPostAsync_ValidCredentials_WithReturnUrl_RedirectsToReturnUrl()
    {
        var (handler, factory) = MockHttpHelper.Create();
        handler.When("https://api.test/api/auth/tenants").Respond("application/json", "[]");

        var loginResponse = new LoginResponse
        {
            AccessToken = "eyJhbGciOiJIUzI1NiJ9.test.sig",
            User = new UserResponse
            {
                Id         = Guid.NewGuid(),
                FullName   = "Jean Dupont",
                Email      = "agent@pm.fr",
                BadgeNumber = "PM-001",
                Role       = UserRole.Officer,
                TenantId   = Guid.NewGuid(),
                TenantName = "Toulouse",
                TenantSlug = "toulouse",
                IsActive   = true,
            }
        };
        handler.When("https://api.test/api/auth/login")
               .Respond("application/json", JsonSerializer.Serialize(loginResponse, JsonOpts));

        var model = CreateModel(factory);
        model.Email    = "agent@pm.fr";
        model.Password = "Officer123!";
        model.CitySlug = "toulouse";

        var result = await model.OnPostAsync(returnUrl: "/Missions");

        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/Missions", redirect.Url);
    }

    // ── OnPostAsync — 2FA ────────────────────────────────────────────────────

    [Fact]
    public async Task OnPostAsync_RequiresTwoFactor_RedirectsToTwoFactorPage()
    {
        var (handler, factory) = MockHttpHelper.Create();
        handler.When("https://api.test/api/auth/tenants").Respond("application/json", "[]");

        var loginResponse = new LoginResponse
        {
            RequiresTwoFactor = true,
            TempToken = "temp-token-abc"
        };
        handler.When("https://api.test/api/auth/login")
               .Respond("application/json", JsonSerializer.Serialize(loginResponse, JsonOpts));

        var model = CreateModel(factory);
        model.Email    = "agent@pm.fr";
        model.Password = "pass";
        model.CitySlug = "toulouse";

        var result = await model.OnPostAsync(returnUrl: null);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Account/TwoFactor", redirect.PageName);
    }
}
