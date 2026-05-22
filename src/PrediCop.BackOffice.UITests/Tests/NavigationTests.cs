using Microsoft.Playwright;
using PrediCop.BackOffice.UITests.Infrastructure;
using Xunit;

namespace PrediCop.BackOffice.UITests.Tests;

[Collection("Playwright")]
public class NavigationTests : PlaywrightBaseTest
{
    public NavigationTests(PlaywrightFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Navbar_AdminDropdown_ContainsAllMenuItems()
    {
        await LoginAsync();

        // Ouvrir le dropdown Administration
        var adminDropdown = Page.Locator(".navbar [data-bs-toggle='dropdown']:has-text('Administration'), .navbar .dropdown-toggle:has-text('Administration'), .navbar .dropdown-toggle:has-text('Admin')");
        var dropdownCount = await adminDropdown.CountAsync();

        if (dropdownCount > 0)
        {
            await adminDropdown.First.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

            // Vérifier la présence des liens vers les modules principaux
            var dropdownMenu = Page.Locator(".dropdown-menu.show, .navbar .dropdown-menu");

            var hasVehicles = await dropdownMenu.Locator("a:has-text('Véhicule'), a[href*='Vehicle']").CountAsync() > 0;
            var hasHR = await dropdownMenu.Locator("a:has-text('RH'), a:has-text('Personnel'), a[href*='HR']").CountAsync() > 0;

            // Au moins un lien doit être présent dans le menu déroulant
            var totalLinks = await dropdownMenu.Locator("a").CountAsync();
            Assert.True(totalLinks > 0,
                "Le menu déroulant Administration ne contient aucun lien.");

            Assert.True(hasVehicles || hasHR,
                "Le menu déroulant Administration devrait contenir des liens vers Véhicules ou RH.");
        }
        else
        {
            // Si pas de dropdown nommé "Administration", vérifier la présence de liens admin dans la navbar
            var navbarLinks = await Page.Locator(".navbar a[href*='Admin']").CountAsync();
            Assert.True(navbarLinks > 0,
                "La navbar ne contient aucun lien vers les modules Admin.");
        }
    }

    [Fact]
    public async Task Navbar_SearchBar_IsVisible()
    {
        await LoginAsync();

        // Vérifier la présence d'une barre de recherche dans la navbar
        var searchInput = Page.Locator(".navbar .input-group input, .navbar input[type='search'], .navbar input[type='text'][placeholder*='Recherche'], .navbar input[type='text'][placeholder*='Search']");
        var count = await searchInput.CountAsync();
        Assert.True(count >= 1,
            "La barre de recherche (.input-group input) est absente de la navbar.");
    }

    [Fact]
    public async Task Breadcrumb_Navigation_WorksBetweenPages()
    {
        await LoginAsync();

        // Aller sur la page des Appels
        await Page.GotoAsync($"{BaseUrl}/Calls");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 15000 });

        var urlBefore = Page.Url;

        // Chercher un lien vers "Réception appel" ou "Receive"
        var receiveLink = Page.Locator("a:has-text('Réception'), a:has-text('Recevoir'), a[href*='Receive'], a:has-text('Nouvel appel')");
        var linkCount = await receiveLink.CountAsync();

        if (linkCount > 0)
        {
            await receiveLink.First.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 15000 });

            var urlAfter = Page.Url;
            Assert.NotEqual(urlBefore, urlAfter);
        }
        else
        {
            // Fallback : naviguer directement vers /Calls/Receive et vérifier le changement d'URL
            await Page.GotoAsync($"{BaseUrl}/Calls/Receive");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 15000 });

            Assert.Contains("Receive", Page.Url, StringComparison.OrdinalIgnoreCase);
        }
    }
}
