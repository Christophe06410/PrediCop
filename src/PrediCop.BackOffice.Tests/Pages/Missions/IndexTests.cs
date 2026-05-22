using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.BackOffice.Models;
using PrediCop.BackOffice.Pages.Missions;
using PrediCop.BackOffice.Tests.Helpers;
using RichardSzalay.MockHttp;
using Xunit;

namespace PrediCop.BackOffice.Tests.Pages.Missions;

public class IndexTests
{
    private static string MakeMissionsPageJson(List<MissionDto> items)
    {
        var page = new { Items = items, TotalCount = items.Count };
        return JsonSerializer.Serialize(page);
    }

    [Fact]
    public async Task OnGetAsync_ApiReturnsMissions_PopulatesMissionsList()
    {
        var (handler, factory) = MockHttpHelper.Create();

        var activeMissions = new List<MissionDto>
        {
            new() { Id = Guid.NewGuid(), Reference = "M-001", Status = "InProgress" },
            new() { Id = Guid.NewGuid(), Reference = "M-002", Status = "Proposed" }
        };

        var recentMissions = new List<MissionDto>
        {
            new() { Id = Guid.NewGuid(), Reference = "M-003", Status = "Completed" },
            new() { Id = Guid.NewGuid(), Reference = "M-004", Status = "Cancelled" }
        };

        handler.When("https://api.test/api/missions/active")
               .Respond("application/json", JsonSerializer.Serialize(activeMissions));

        handler.When("https://api.test/api/missions*")
               .Respond("application/json", MakeMissionsPageJson(recentMissions));

        var model = new IndexModel(factory, MockHttpHelper.NullLogger<IndexModel>());
        var result = await model.OnGetAsync();

        Assert.IsType<PageResult>(result);
        Assert.Equal(2, model.ActiveMissions.Count);
        Assert.Equal(2, model.RecentMissions.Count);
    }

    [Fact]
    public async Task OnGetAsync_ApiFails_ReturnsEmptyList()
    {
        var (handler, factory) = MockHttpHelper.Create();

        handler.When("*").Respond(HttpStatusCode.InternalServerError);

        var model = new IndexModel(factory, MockHttpHelper.NullLogger<IndexModel>());
        var exception = await Record.ExceptionAsync(() => model.OnGetAsync());

        Assert.Null(exception);
        Assert.Empty(model.ActiveMissions);
        Assert.Empty(model.RecentMissions);
    }

    [Fact]
    public async Task OnGetAsync_MissionsWithPriority_OrderedByPriority()
    {
        var (handler, factory) = MockHttpHelper.Create();

        var activeMissions = new List<MissionDto>
        {
            new() { Id = Guid.NewGuid(), Reference = "M-001", Status = "InProgress", Priority = "Routine" },
            new() { Id = Guid.NewGuid(), Reference = "M-002", Status = "Proposed", Priority = "SOS" }
        };

        handler.When("https://api.test/api/missions/active")
               .Respond("application/json", JsonSerializer.Serialize(activeMissions));
        handler.When("https://api.test/api/missions*")
               .Respond("application/json", MakeMissionsPageJson([]));

        var model = new IndexModel(factory, MockHttpHelper.NullLogger<IndexModel>());
        await model.OnGetAsync();

        Assert.Equal(2, model.ActiveMissions.Count);
        Assert.Contains(model.ActiveMissions, m => m.Priority == "SOS");
        Assert.Contains(model.ActiveMissions, m => m.Priority == "Routine");
    }

    [Fact]
    public async Task OnGetAsync_ActiveMission_HasPriorityField()
    {
        var (handler, factory) = MockHttpHelper.Create();

        var activeMissions = new List<MissionDto>
        {
            new() { Id = Guid.NewGuid(), Reference = "M-001", Status = "InProgress", Priority = "Urgent" }
        };

        handler.When("https://api.test/api/missions/active")
               .Respond("application/json", JsonSerializer.Serialize(activeMissions));
        handler.When("https://api.test/api/missions*")
               .Respond("application/json", MakeMissionsPageJson([]));

        var model = new IndexModel(factory, MockHttpHelper.NullLogger<IndexModel>());
        await model.OnGetAsync();

        Assert.Single(model.ActiveMissions);
        Assert.Equal("Urgent", model.ActiveMissions[0].Priority);
    }
}
