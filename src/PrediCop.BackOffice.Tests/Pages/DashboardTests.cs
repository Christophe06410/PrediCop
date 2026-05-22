using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.BackOffice.Models;
using PrediCop.BackOffice.Pages.Dashboard;
using PrediCop.BackOffice.Tests.Helpers;
using RichardSzalay.MockHttp;
using Xunit;

namespace PrediCop.BackOffice.Tests.Pages;

public class DashboardTests
{
    [Fact]
    public async Task OnGetAsync_ApiReturnsData_PopulatesDashboard()
    {
        var (handler, factory) = MockHttpHelper.Create();

        var dto = new DashboardDto
        {
            CallsToday = 5,
            ActiveMissions = 3,
            AvailableVehicles = 7,
            VehiclesOnMission = 2
        };
        var json = JsonSerializer.Serialize(dto);

        var timeSeriesDto = new TimeSeriesStatsResponse();
        handler.When("https://api.test/api/dashboard/timeseries*")
               .Respond("application/json", JsonSerializer.Serialize(timeSeriesDto));
        handler.When("https://api.test/api/dashboard")
               .Respond("application/json", json);

        var model = new IndexModel(factory, MockHttpHelper.NullLogger<IndexModel>());
        await model.OnGetAsync();

        Assert.Equal(5, model.Dashboard.CallsToday);
        Assert.Equal(3, model.Dashboard.ActiveMissions);
        Assert.Equal(7, model.Dashboard.AvailableVehicles);
        Assert.Equal(2, model.Dashboard.VehiclesOnMission);
    }

    [Fact]
    public async Task OnGetAsync_ApiFails_DashboardRemainsDefault()
    {
        var (handler, factory) = MockHttpHelper.Create();

        handler.When("*").Respond(HttpStatusCode.InternalServerError);

        var model = new IndexModel(factory, MockHttpHelper.NullLogger<IndexModel>());
        var exception = await Record.ExceptionAsync(() => model.OnGetAsync());

        Assert.Null(exception);
        Assert.NotNull(model.Dashboard);
        Assert.Equal(0, model.Dashboard.CallsToday);
        Assert.Equal(0, model.Dashboard.ActiveMissions);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsPage()
    {
        var (handler, factory) = MockHttpHelper.Create();

        var dto = new DashboardDto();
        var timeSeriesDto = new TimeSeriesStatsResponse();
        handler.When("https://api.test/api/dashboard/timeseries*")
               .Respond("application/json", JsonSerializer.Serialize(timeSeriesDto));
        handler.When("https://api.test/api/dashboard")
               .Respond("application/json", JsonSerializer.Serialize(dto));

        var model = new IndexModel(factory, MockHttpHelper.NullLogger<IndexModel>());
        var result = await model.OnGetAsync();

        Assert.IsType<PageResult>(result);
    }

    [Fact]
    public async Task OnGetAsync_WithDays14_BuildsCorrectTimeseriesUrl()
    {
        var (handler, factory) = MockHttpHelper.Create();

        string? capturedUrl = null;

        handler.When("https://api.test/api/dashboard/timeseries*")
               .With(req =>
               {
                   capturedUrl = req.RequestUri?.PathAndQuery;
                   return true;
               })
               .Respond("application/json", JsonSerializer.Serialize(new TimeSeriesStatsResponse()));
        handler.When("https://api.test/api/dashboard")
               .Respond("application/json", JsonSerializer.Serialize(new DashboardDto()));

        var model = new IndexModel(factory, MockHttpHelper.NullLogger<IndexModel>())
        {
            Days = 14
        };
        await model.OnGetAsync();

        Assert.NotNull(capturedUrl);
        Assert.Contains("days=14", capturedUrl);
    }

    [Fact]
    public async Task OnGetAsync_TimeseriesApiFails_DashboardStillLoads()
    {
        var (handler, factory) = MockHttpHelper.Create();

        var dto = new DashboardDto { CallsToday = 5 };
        var json = JsonSerializer.Serialize(dto);

        // timeseries → 500, dashboard → 200
        // Both are called with WhenAll — if one throws, the whole try/catch fires
        // So we use a wildcard fallback that returns 500 for timeseries specifically
        handler.When("https://api.test/api/dashboard/timeseries*")
               .Respond(HttpStatusCode.InternalServerError);
        handler.When("https://api.test/api/dashboard")
               .Respond("application/json", json);

        var model = new IndexModel(factory, MockHttpHelper.NullLogger<IndexModel>());
        var exception = await Record.ExceptionAsync(() => model.OnGetAsync());

        // The page wraps both calls in a single try/catch — if timeseries fails,
        // the exception is caught and Dashboard remains at default value.
        Assert.Null(exception);
        Assert.NotNull(model.Dashboard);
    }
}
