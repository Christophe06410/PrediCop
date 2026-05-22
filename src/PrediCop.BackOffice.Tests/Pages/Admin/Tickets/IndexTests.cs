using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using PrediCop.BackOffice.Tests.Helpers;
using PrediCop.Core.DTOs;
using PrediCop.Core.Enums;
using RichardSzalay.MockHttp;
using Xunit;

namespace PrediCop.BackOffice.Tests.Pages.Admin.Tickets;

public class IndexTests
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static ITempDataDictionary CreateTempData()
    {
        var mock = new Mock<ITempDataDictionary>();
        mock.SetupSet(t => t[It.IsAny<string>()] = It.IsAny<object>());
        return mock.Object;
    }

    private static ElectronicTicketResponse MakeTicket(string number, Guid? missionId = null, string? missionReference = null) =>
        new(
            Guid.NewGuid(), number, DateTime.UtcNow,
            Guid.NewGuid(), "Agent Test", "PM-001",
            "12 rue de la Paix", null, null,
            "AB-123-CD", "Renault", "Clio", "Blanc",
            InfractionType.StationnementInterdit, null, 35m, null,
            TicketStatus.Issued, false, null, false, null, DateTime.UtcNow,
            missionId, missionReference
        );

    private static TicketStatsResponse MakeStats(int totalIssued) =>
        new(totalIssued, 10, 3, 2, (decimal)(totalIssued * 35),
            new Dictionary<string, int>(), new Dictionary<string, int>(), new Dictionary<string, int>());

    [Fact]
    public async Task OnGetAsync_ApiReturnsTickets_PopulatesList()
    {
        var (handler, factory) = MockHttpHelper.Create();

        var tickets = Enumerable.Range(1, 5)
            .Select(i => MakeTicket($"PV-{i:D4}"))
            .ToList();

        handler.When("/api/users").Respond("application/json", "[]");
        handler.When("/api/tickets/stats*").Respond("application/json", JsonSerializer.Serialize(MakeStats(5), JsonOpts));
        handler.When("/api/tickets*").Respond("application/json", JsonSerializer.Serialize(tickets, JsonOpts));

        var model = new BackOffice.Pages.Admin.Tickets.IndexModel(factory, MockHttpHelper.NullLogger<BackOffice.Pages.Admin.Tickets.IndexModel>());
        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal(5, model.Tickets.Count);
    }

    [Fact]
    public async Task OnGetAsync_ApiFails_EmptyLists()
    {
        var (handler, factory) = MockHttpHelper.Create();

        handler.When("*").Respond(HttpStatusCode.InternalServerError);

        var model = new BackOffice.Pages.Admin.Tickets.IndexModel(factory, MockHttpHelper.NullLogger<BackOffice.Pages.Admin.Tickets.IndexModel>());
        var exception = await Record.ExceptionAsync(() => model.OnGetAsync(CancellationToken.None));

        Assert.Null(exception);
        Assert.Empty(model.Tickets);
        Assert.Empty(model.Agents);
        Assert.Null(model.Stats);
    }

    [Fact]
    public async Task OnGetAsync_Stats_AreLoaded()
    {
        var (handler, factory) = MockHttpHelper.Create();

        var stats = MakeStats(50);

        handler.When("/api/users").Respond("application/json", "[]");
        handler.When("/api/tickets/stats*").Respond("application/json", JsonSerializer.Serialize(stats, JsonOpts));
        handler.When("/api/tickets*").Respond("application/json", "[]");

        var model = new BackOffice.Pages.Admin.Tickets.IndexModel(factory, MockHttpHelper.NullLogger<BackOffice.Pages.Admin.Tickets.IndexModel>());
        await model.OnGetAsync(CancellationToken.None);

        Assert.NotNull(model.Stats);
        Assert.Equal(50, model.Stats!.TotalIssued);
    }

    [Fact]
    public async Task OnPostCancelAsync_Success_Redirects()
    {
        var (handler, factory) = MockHttpHelper.Create();
        var ticketId = Guid.NewGuid();

        handler.When(HttpMethod.Put, $"/api/tickets/{ticketId}/status")
               .Respond(HttpStatusCode.OK);

        var model = new BackOffice.Pages.Admin.Tickets.IndexModel(factory, MockHttpHelper.NullLogger<BackOffice.Pages.Admin.Tickets.IndexModel>())
        {
            TempData = CreateTempData()
        };

        var result = await model.OnPostCancelAsync(ticketId, "Erreur de saisie");

        Assert.IsType<RedirectToPageResult>(result);
    }

    [Fact]
    public async Task OnGetAsync_TicketWithMissionId_HasMissionReference()
    {
        var (handler, factory) = MockHttpHelper.Create();

        var missionId = Guid.NewGuid();
        var ticket = MakeTicket("PV-0001", missionId, "MSN-001");
        var tickets = new List<ElectronicTicketResponse> { ticket };

        handler.When("/api/users").Respond("application/json", "[]");
        handler.When("/api/tickets/stats*").Respond("application/json", JsonSerializer.Serialize(MakeStats(1), JsonOpts));
        handler.When("/api/tickets*").Respond("application/json", JsonSerializer.Serialize(tickets, JsonOpts));

        var model = new BackOffice.Pages.Admin.Tickets.IndexModel(factory, MockHttpHelper.NullLogger<BackOffice.Pages.Admin.Tickets.IndexModel>());
        await model.OnGetAsync(CancellationToken.None);

        Assert.Single(model.Tickets);
        Assert.NotNull(model.Tickets[0].MissionId);
        Assert.Equal(missionId, model.Tickets[0].MissionId);
        Assert.Equal("MSN-001", model.Tickets[0].MissionReference);
    }
}
