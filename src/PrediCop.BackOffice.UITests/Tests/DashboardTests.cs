using Microsoft.Playwright;
using PrediCop.BackOffice.UITests.Infrastructure;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace PrediCop.BackOffice.UITests.Tests;

[Collection("Playwright")]
public class DashboardTests : PlaywrightBaseTest
{
    public DashboardTests(PlaywrightFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Dashboard_AfterLogin_ShowsKpiCards()
    {
        await LoginAsync();
        await Page.GotoAsync($"{BaseUrl}/Dashboard");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 15000 });

        var kpiCards = Page.Locator(".kpi-card");
        var count = await kpiCards.CountAsync();
        Assert.True(count >= 1, $"Expected at least 1 .kpi-card element on the Dashboard, found {count}.");
    }

    [Fact]
    public async Task Dashboard_PageTitle_ContainsDashboard()
    {
        await LoginAsync();
        await Page.GotoAsync($"{BaseUrl}/Dashboard");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 15000 });

        var title = await Page.TitleAsync();
        Assert.Contains("Dashboard", title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Dashboard_Navbar_IsVisible()
    {
        await LoginAsync();
        await Page.GotoAsync($"{BaseUrl}/Dashboard");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 15000 });

        await Expect(Page.Locator("nav.navbar")).ToBeVisibleAsync();
    }
}
