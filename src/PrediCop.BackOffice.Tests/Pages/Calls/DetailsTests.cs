using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.BackOffice.Models;
using PrediCop.BackOffice.Pages.Calls;
using PrediCop.BackOffice.Tests.Helpers;
using RichardSzalay.MockHttp;
using Xunit;

namespace PrediCop.BackOffice.Tests.Pages.Calls;

public class DetailsTests
{
    private static CallDto MakeCall(Guid id, List<MissionDto>? missions = null) =>
        new()
        {
            Id = id,
            Reference = "MC-2026-001",
            Status = "MissionCreated",
            Priority = "Routine",
            CallerName = "Jean Dupont",
            CallerPhone = "0600000001",
            IncidentCategory = "Tapage",
            IncidentDescription = "Tapage nocturne",
            IncidentAddress = "1 rue de la Paix",
            OperatorName = "Opérateur Test",
            Missions = missions ?? []
        };

    [Fact]
    public async Task OnGetAsync_LoadsCallWithMissions_Correctly()
    {
        var (handler, factory) = MockHttpHelper.Create();
        var callId = Guid.NewGuid();

        var missions = new List<MissionDto>
        {
            new() { Id = Guid.NewGuid(), Reference = "M-001", Status = "Completed" }
        };
        var call = MakeCall(callId, missions);

        handler.When($"https://api.test/api/calls/{callId}")
               .Respond("application/json", JsonSerializer.Serialize(call));

        var model = new DetailsModel(factory, MockHttpHelper.NullLogger<DetailsModel>())
            .WithPageContext();
        var result = await model.OnGetAsync(callId);

        Assert.IsType<PageResult>(result);
        Assert.NotNull(model.Call);
        Assert.Equal("MC-2026-001", model.Call!.Reference);
        Assert.Single(model.Call.Missions);
    }

    [Fact]
    public async Task OnGetAsync_WithCompletedMissions_CanReopenIsTrue()
    {
        var (handler, factory) = MockHttpHelper.Create();
        var callId = Guid.NewGuid();

        var missions = new List<MissionDto>
        {
            new() { Id = Guid.NewGuid(), Reference = "M-001", Status = "Completed" },
            new() { Id = Guid.NewGuid(), Reference = "M-002", Status = "Cancelled" }
        };
        var call = MakeCall(callId, missions);

        handler.When($"https://api.test/api/calls/{callId}")
               .Respond("application/json", JsonSerializer.Serialize(call));

        var model = new DetailsModel(factory, MockHttpHelper.NullLogger<DetailsModel>())
            .WithPageContext();
        await model.OnGetAsync(callId);

        Assert.True(model.CanReopen);
        Assert.False(model.HasActiveMission);
    }

    [Fact]
    public async Task OnGetAsync_WithActiveMission_CanReopenIsFalse()
    {
        var (handler, factory) = MockHttpHelper.Create();
        var callId = Guid.NewGuid();

        var missions = new List<MissionDto>
        {
            new() { Id = Guid.NewGuid(), Reference = "M-001", Status = "Completed" },
            new() { Id = Guid.NewGuid(), Reference = "M-002", Status = "InProgress" }
        };
        var call = MakeCall(callId, missions);

        handler.When($"https://api.test/api/calls/{callId}")
               .Respond("application/json", JsonSerializer.Serialize(call));

        var model = new DetailsModel(factory, MockHttpHelper.NullLogger<DetailsModel>())
            .WithPageContext();
        await model.OnGetAsync(callId);

        Assert.False(model.CanReopen);
        Assert.True(model.HasActiveMission);
    }

    [Fact]
    public async Task OnPostReopenAsync_ApiSuccess_RedirectsToMissions()
    {
        var (handler, factory) = MockHttpHelper.Create();
        var callId = Guid.NewGuid();

        handler.Expect(HttpMethod.Post, $"https://api.test/api/calls/{callId}/create-mission")
               .Respond(HttpStatusCode.OK, "application/json", "{}");

        var model = new DetailsModel(factory, MockHttpHelper.NullLogger<DetailsModel>())
            .WithPageContext();
        var result = await model.OnPostReopenAsync(callId, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Missions/Index", redirect.PageName);
    }

    [Fact]
    public async Task OnPostReopenAsync_ApiFails_StaysOnPageWithError()
    {
        var (handler, factory) = MockHttpHelper.Create();
        var callId = Guid.NewGuid();

        handler.Expect(HttpMethod.Post, $"https://api.test/api/calls/{callId}/create-mission")
               .Respond(HttpStatusCode.BadRequest, "application/json", "{\"error\":\"active mission exists\"}");

        var model = new DetailsModel(factory, MockHttpHelper.NullLogger<DetailsModel>())
            .WithPageContext();
        var result = await model.OnPostReopenAsync(callId, CancellationToken.None);

        // On failure, redirects back to the same page (with id)
        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.NotNull(redirect.RouteValues);
        Assert.True(redirect.RouteValues!.ContainsKey("id"));
    }
}
