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
    public async Task AdminPlanning_PageLoads()
    {
        await AssertAdminPageLoads("/Admin/Planning");
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

    [Fact]
    public async Task Map_PageLoads()
    {
        await LoginAsync();
        await Page.GotoAsync($"{BaseUrl}/Map");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 15000 });

        var hasUnhandledError = await Page.Locator("text=An unhandled exception").IsVisibleAsync();
        Assert.False(hasUnhandledError, "La page /Map affiche une erreur serveur non gérée.");

        // La carte Leaflet doit être présente
        var hasMap = await Page.Locator("#live-map").IsVisibleAsync();
        Assert.True(hasMap, "Le div #live-map est absent de la page Carte.");
    }

    [Fact]
    public async Task AdminUsers_Edit_ContainsPatrolRoles()
    {
        await LoginAsync();
        await Page.GotoAsync($"{BaseUrl}/Admin/Users/Edit");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 15000 });

        var hasUnhandledError = await Page.Locator("text=An unhandled exception").IsVisibleAsync();
        Assert.False(hasUnhandledError, "La page /Admin/Users/Edit affiche une erreur serveur.");

        // Les rôles PatrolLeader et PatrolAgent doivent être dans le select
        var patrolLeaderOption = Page.Locator("option[value='PatrolLeader'], select option:has-text('PatrolLeader')");
        var patrolAgentOption  = Page.Locator("option[value='PatrolAgent'],  select option:has-text('PatrolAgent')");
        Assert.True(await patrolLeaderOption.CountAsync() > 0, "L'option 'PatrolLeader' est absente du formulaire.");
        Assert.True(await patrolAgentOption.CountAsync() > 0,  "L'option 'PatrolAgent' est absente du formulaire.");
    }
}
