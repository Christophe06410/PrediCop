using Microsoft.Playwright;
using PrediCop.BackOffice.UITests.Infrastructure;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace PrediCop.BackOffice.UITests.Tests;

[Collection("Playwright")]
public class CallsTests : PlaywrightBaseTest
{
    public CallsTests(PlaywrightFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CallsList_PageLoads_ShowsTable()
    {
        await LoginAsync();
        await Page.GotoAsync($"{BaseUrl}/Calls");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 15000 });

        var hasServerError = await Page.Locator("text=An unhandled exception").IsVisibleAsync();
        Assert.False(hasServerError, "La page /Calls affiche une erreur serveur non gérée.");

        var hasTable = await Page.Locator("table").IsVisibleAsync();
        var hasEmptyMessage = await Page.Locator(".text-center.text-muted").IsVisibleAsync();

        Assert.True(hasTable || hasEmptyMessage,
            "La page /Calls devrait afficher soit un tableau, soit un message indiquant qu'il n'y a pas de données.");
    }

    [Fact]
    public async Task CallsReceive_PageLoads_ShowsForm()
    {
        await LoginAsync();
        await Page.GotoAsync($"{BaseUrl}/Calls/Receive");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 15000 });

        var hasServerError = await Page.Locator("text=An unhandled exception").IsVisibleAsync();
        Assert.False(hasServerError, "La page /Calls/Receive affiche une erreur serveur non gérée.");

        await Expect(Page.Locator("form[method='post']")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task CallsReceive_FormHasRequiredFields()
    {
        await LoginAsync();
        await Page.GotoAsync($"{BaseUrl}/Calls/Receive");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 15000 });

        var addressCount = await Page.Locator("[name='Address']").CountAsync();
        Assert.True(addressCount >= 1, "Le champ [name='Address'] est absent du formulaire de réception d'appel.");

        var descriptionCount = await Page.Locator("[name='Description']").CountAsync();
        Assert.True(descriptionCount >= 1, "Le champ [name='Description'] est absent du formulaire de réception d'appel.");
    }
}
