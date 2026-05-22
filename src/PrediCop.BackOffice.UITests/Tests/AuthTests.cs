using Microsoft.Playwright;
using PrediCop.BackOffice.UITests.Infrastructure;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace PrediCop.BackOffice.UITests.Tests;

[Collection("Playwright")]
public class AuthTests : PlaywrightBaseTest
{
    public AuthTests(PlaywrightFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Login_WithValidCredentials_RedirectsToDashboard()
    {
        await LoginAsync();

        Assert.False(IsOnLoginPage(), $"Expected redirect away from login, but still on: {Page.Url}");
        await Expect(Page.Locator("nav.navbar")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ShowsError()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.FillAsync("[name='Email']", TestEmail);
        await Page.FillAsync("[name='Password']", "WrongPassword999!");

        var cityInput = Page.Locator("[name='CitySlug']");
        var tagName = await cityInput.EvaluateAsync<string>("el => el.tagName.toLowerCase()");
        if (tagName == "select")
            await cityInput.SelectOptionAsync(new SelectOptionValue { Value = TestCitySlug });
        else
            await cityInput.FillAsync(TestCitySlug);

        await Page.ClickAsync("button[type='submit']");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 15000 });

        await Expect(Page.Locator(".alert-danger")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task AccessProtectedPage_WhenNotAuthenticated_RedirectsToLogin()
    {
        await Page.GotoAsync($"{BaseUrl}/Dashboard");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 15000 });

        Assert.Contains("/Account/Login", Page.Url);
    }

    [Fact]
    public async Task Logout_AfterLogin_RedirectsToLogin()
    {
        await LoginAsync();
        Assert.False(IsOnLoginPage(), "Login should have succeeded before testing logout.");

        // Trouver et cliquer le bouton de déconnexion (formulaire POST /Account/Logout)
        var logoutButton = Page.Locator("form[action*='Logout'] button[type='submit']");
        var logoutButtonCount = await logoutButton.CountAsync();

        if (logoutButtonCount > 0)
        {
            await logoutButton.First.ClickAsync();
        }
        else
        {
            // Fallback : soumettre le formulaire de logout via JavaScript
            await Page.EvaluateAsync(@"
                const form = document.querySelector('form[action*=""Logout""]');
                if (form) form.submit();
            ");
        }

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 15000 });

        Assert.Contains("/Account/Login", Page.Url);
    }
}
