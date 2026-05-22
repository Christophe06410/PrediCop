using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.BackOffice.Tests.Helpers;
using PrediCop.Core.DTOs;
using RichardSzalay.MockHttp;
using Xunit;

namespace PrediCop.BackOffice.Tests.Pages.Admin.Tickets;

public class StatsTests
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private static TicketStatsResponse MakeStats() =>
        new(42, 18, 5, 3, 1470m,
            new Dictionary<string, int> { { "StationnementInterdit", 20 }, { "FeuRouge", 22 } },
            new Dictionary<string, int> { { "Agent Test", 42 } },
            new Dictionary<string, int> { { "Monday", 10 }, { "Friday", 32 } });

    [Fact]
    public async Task OnGetAsync_ApiReturnsStats_PopulatesStats()
    {
        var (handler, factory) = MockHttpHelper.Create();

        var stats = MakeStats();
        handler.When("/api/tickets/stats*").Respond("application/json", JsonSerializer.Serialize(stats, JsonOpts));

        var model = new BackOffice.Pages.Admin.Tickets.StatsModel(factory, MockHttpHelper.NullLogger<BackOffice.Pages.Admin.Tickets.StatsModel>());
        await model.OnGetAsync(CancellationToken.None);

        Assert.NotNull(model.Stats);
        Assert.Equal(42, model.Stats!.TotalIssued);
        Assert.Equal(18, model.Stats.TotalPaid);
        Assert.Equal(5, model.Stats.TotalContested);
        Assert.Equal(3, model.Stats.TotalCancelled);
    }

    [Fact]
    public async Task OnGetAsync_ApiFails_StatsRemainsNull()
    {
        var (handler, factory) = MockHttpHelper.Create();

        handler.When("*").Respond(HttpStatusCode.InternalServerError);

        var model = new BackOffice.Pages.Admin.Tickets.StatsModel(factory, MockHttpHelper.NullLogger<BackOffice.Pages.Admin.Tickets.StatsModel>());
        var exception = await Record.ExceptionAsync(() => model.OnGetAsync(CancellationToken.None));

        Assert.Null(exception);
        Assert.Null(model.Stats);
    }
}
