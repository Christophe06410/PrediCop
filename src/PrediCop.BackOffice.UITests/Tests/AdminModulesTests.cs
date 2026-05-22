using Microsoft.Playwright;
using PrediCop.BackOffice.UITests.Infrastructure;
using Xunit;

namespace PrediCop.BackOffice.UITests.Tests;

[Collection("Playwright")]
public class AdminModulesTests : PlaywrightBaseTest
{
    public AdminModulesTests(PlaywrightFixture fixture) : base(fixture) { }

    private async Task AssertAdminPageLoads(string path)
    {
        await LoginAsync();
        await Page.GotoAsync($"{BaseUrl}{path}");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 15000 });

        var hasUnhandledError = await Page.Locator("text=An unhandled exception").IsVisibleAsync();
        Assert.False(hasUnhandledError, $"La page {path} affiche une erreur serveur non gérée.");

        // Vérifier qu'un élément de structure est présent (h4 ou h1 ou h2)
        var hasHeading = await Page.Locator("h1, h2, h3, h4").CountAsync() > 0;
        Assert.True(hasHeading, $"La page {path} ne contient aucun titre (h1-h4).");
    }

    [Fact]
    public async Task AdminVehicles_PageLoads()
    {
        await AssertAdminPageLoads("/Admin/Vehicles");
    }

    [Fact]
    public async Task AdminQualifications_PageLoads()
    {
        await AssertAdminPageLoads("/Admin/Qualifications");
    }

    [Fact]
    public async Task AdminHR_PageLoads()
    {
        await AssertAdminPageLoads("/Admin/HR");
    }

    [Fact]
    public async Task AdminFleet_PageLoads()
    {
        await AssertAdminPageLoads("/Admin/Fleet");
    }

    [Fact]
    public async Task AdminLogistics_PageLoads()
    {
        await AssertAdminPageLoads("/Admin/Logistics");
    }

    [Fact]
    public async Task AdminFourriere_PageLoads()
    {
        await AssertAdminPageLoads("/Admin/Fourriere");
    }

    [Fact]
    public async Task AdminTickets_PageLoads()
    {
        await AssertAdminPageLoads("/Admin/Tickets");
    }

    [Fact]
    public async Task AdminTicketsStats_PageLoads()
    {
        await AssertAdminPageLoads("/Admin/Tickets/Stats");
    }

    [Fact]
    public async Task AdminAudit_PageLoads()
    {
        await AssertAdminPageLoads("/Admin/Audit");
    }
}
