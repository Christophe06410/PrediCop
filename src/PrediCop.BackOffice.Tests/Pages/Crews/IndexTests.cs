using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.BackOffice.Pages.Crews;
using PrediCop.BackOffice.Tests.Helpers;
using RichardSzalay.MockHttp;
using Xunit;

namespace PrediCop.BackOffice.Tests.Pages.Crews;

public class IndexTests
{
    [Fact]
    public async Task OnGetAsync_ApiReturnsCrews_PopulatesCrewsList()
    {
        var (handler, factory) = MockHttpHelper.Create();

        var crews = new List<CrewSheetEntryDto>
        {
            new()
            {
                VehicleId = Guid.NewGuid(),
                CallSign = "PM-01",
                LicensePlate = "AB-123-CD",
                Status = "Available",
                Officers = new List<CrewMemberDto>
                {
                    new() { UserId = Guid.NewGuid(), FullName = "Agent Durand", BadgeNumber = "PM-001" }
                },
                CurrentMission = null
            },
            new()
            {
                VehicleId = Guid.NewGuid(),
                CallSign = "PM-02",
                LicensePlate = "EF-456-GH",
                Status = "OnMission",
                Officers = new List<CrewMemberDto>
                {
                    new() { UserId = Guid.NewGuid(), FullName = "Agent Martin", BadgeNumber = "PM-002" }
                },
                CurrentMission = new ActiveMissionDto
                {
                    MissionId = Guid.NewGuid(),
                    Reference = "M-2026-001",
                    Priority = "Urgent",
                    TargetAddress = "5 rue du Commerce"
                }
            }
        };

        handler.When("https://api.test/api/vehicles/crew-sheet")
               .Respond("application/json", JsonSerializer.Serialize(crews));

        var model = new IndexModel(factory, MockHttpHelper.NullLogger<IndexModel>());
        var result = await model.OnGetAsync();

        Assert.IsType<PageResult>(result);
        Assert.Equal(2, model.Crews.Count);
        Assert.Equal("PM-01", model.Crews[0].CallSign);
        Assert.Equal("PM-02", model.Crews[1].CallSign);
        Assert.NotNull(model.Crews[1].CurrentMission);
        Assert.Equal("M-2026-001", model.Crews[1].CurrentMission!.Reference);
    }

    [Fact]
    public async Task OnGetAsync_EmptyList_ReturnsPageWithoutError()
    {
        var (handler, factory) = MockHttpHelper.Create();

        handler.When("https://api.test/api/vehicles/crew-sheet")
               .Respond("application/json", "[]");

        var model = new IndexModel(factory, MockHttpHelper.NullLogger<IndexModel>());
        var result = await model.OnGetAsync();

        Assert.IsType<PageResult>(result);
        Assert.Empty(model.Crews);
        Assert.Null(model.ErrorMessage);
    }

    [Fact]
    public async Task OnGetAsync_ApiFails_ReturnsEmptyList()
    {
        var (handler, factory) = MockHttpHelper.Create();

        handler.When("*").Respond(HttpStatusCode.InternalServerError);

        var model = new IndexModel(factory, MockHttpHelper.NullLogger<IndexModel>());
        var exception = await Record.ExceptionAsync(() => model.OnGetAsync());

        Assert.Null(exception);
        Assert.Empty(model.Crews);
        Assert.NotNull(model.ErrorMessage);
    }
}
