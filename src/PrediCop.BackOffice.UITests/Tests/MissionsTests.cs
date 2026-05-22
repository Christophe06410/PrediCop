using Microsoft.Playwright;
using PrediCop.BackOffice.UITests.Infrastructure;
using Xunit;

namespace PrediCop.BackOffice.UITests.Tests;

[Collection("Playwright")]
public class MissionsTests : PlaywrightBaseTest
{
    public MissionsTests(PlaywrightFixture fixture) : base(fixture) { }

    [Fact]
    public async Task MissionsList_PageLoads()
    {
        await LoginAsync();
        await Page.GotoAsync($"{BaseUrl}/Missions");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 15000 });

        var hasServerError = await Page.Locator("text=An unhandled exception").IsVisibleAsync();
        Assert.False(hasServerError, "La page /Missions affiche une erreur serveur non gérée.");

        // Vérifier que la page s'est bien chargée (titre ou contenu attendu)
        var title = await Page.TitleAsync();
        Assert.False(string.IsNullOrWhiteSpace(title), "La page /Missions n'a pas de titre.");
    }

    [Fact]
    public async Task MissionsMap_PageLoads_HasLeafletMap()
    {
        await LoginAsync();
        await Page.GotoAsync($"{BaseUrl}/Map");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 15000 });

        var hasServerError = await Page.Locator("text=An unhandled exception").IsVisibleAsync();
        Assert.False(hasServerError, "La page /Map affiche une erreur serveur non gérée.");

        var mapCount = await Page.Locator("#map").CountAsync();
        Assert.True(mapCount >= 1, "L'élément #map (carte Leaflet) est absent de la page /Map.");
    }
}
