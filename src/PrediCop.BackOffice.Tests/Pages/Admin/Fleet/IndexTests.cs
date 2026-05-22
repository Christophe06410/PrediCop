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

namespace PrediCop.BackOffice.Tests.Pages.Admin.Fleet;

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

    private static string SerializeVehicles()
    {
        var vehicles = new[]
        {
            new { Id = Guid.NewGuid(), CallSign = "PM-01", LicensePlate = "AA-001-BB" },
            new { Id = Guid.NewGuid(), CallSign = "PM-02", LicensePlate = "AA-002-BB" },
        };
        return JsonSerializer.Serialize(vehicles);
    }

    [Fact]
    public async Task OnGetAsync_ApiReturnsData_PopulatesAllLists()
    {
        var (handler, factory) = MockHttpHelper.Create();

        var summaries = new List<VehicleSummaryResponse>
        {
            new(Guid.NewGuid(), "PM-01", "AA-001-BB", 500, 10, null, null, false),
        };
        var maintenances = new List<VehicleMaintenanceResponse>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "PM-01", "AA-001-BB", MaintenanceType.Revision,
                DateTime.UtcNow.AddDays(10), null, null, "Révision annuelle", null, null, null, false, false, true),
        };
        var alerts = new List<FleetAlertResponse>
        {
            new(Guid.NewGuid(), "PM-01", "AA-001-BB", "Maintenance", "Révision due", DateTime.UtcNow.AddDays(5)),
        };

        handler.When("/api/vehicles").Respond("application/json", SerializeVehicles());
        handler.When("/api/fleet/summary").Respond("application/json", JsonSerializer.Serialize(summaries, JsonOpts));
        handler.When("/api/fleet/maintenance*").Respond("application/json", JsonSerializer.Serialize(maintenances, JsonOpts));
        handler.When("/api/fleet/alerts").Respond("application/json", JsonSerializer.Serialize(alerts, JsonOpts));

        var model = new BackOffice.Pages.Admin.Fleet.IndexModel(factory, MockHttpHelper.NullLogger<BackOffice.Pages.Admin.Fleet.IndexModel>());
        await model.OnGetAsync();

        Assert.NotEmpty(model.Vehicles);
        Assert.NotEmpty(model.VehicleSummaries);
        Assert.NotEmpty(model.UpcomingMaintenances);
        Assert.NotEmpty(model.Alerts);
    }

    [Fact]
    public async Task OnGetAsync_ApiFails_AllListsAreEmpty()
    {
        var (handler, factory) = MockHttpHelper.Create();

        handler.When("*").Respond(HttpStatusCode.InternalServerError);

        var model = new BackOffice.Pages.Admin.Fleet.IndexModel(factory, MockHttpHelper.NullLogger<BackOffice.Pages.Admin.Fleet.IndexModel>());
        var exception = await Record.ExceptionAsync(() => model.OnGetAsync());

        Assert.Null(exception);
        Assert.Empty(model.Vehicles);
        Assert.Empty(model.VehicleSummaries);
        Assert.Empty(model.UpcomingMaintenances);
        Assert.Empty(model.Alerts);
    }

    [Fact]
    public async Task OnGetAsync_HasAlerts_AlertsListPopulated()
    {
        var (handler, factory) = MockHttpHelper.Create();

        var alerts = new List<FleetAlertResponse>
        {
            new(Guid.NewGuid(), "PM-01", "AA-001-BB", "Maintenance", "Révision due", DateTime.UtcNow.AddDays(5)),
            new(Guid.NewGuid(), "PM-02", "AA-002-BB", "ControleTechnique", "CT expiré", DateTime.UtcNow.AddDays(2)),
        };

        handler.When("/api/vehicles").Respond("application/json", "[]");
        handler.When("/api/fleet/summary").Respond("application/json", "[]");
        handler.When("/api/fleet/maintenance*").Respond("application/json", "[]");
        handler.When("/api/fleet/alerts").Respond("application/json", JsonSerializer.Serialize(alerts, JsonOpts));

        var model = new BackOffice.Pages.Admin.Fleet.IndexModel(factory, MockHttpHelper.NullLogger<BackOffice.Pages.Admin.Fleet.IndexModel>());
        await model.OnGetAsync();

        Assert.Equal(2, model.Alerts.Count);
    }

    [Fact]
    public async Task OnPostCompleteMaintenanceAsync_Success_Redirects()
    {
        var (handler, factory) = MockHttpHelper.Create();
        var maintenanceId = Guid.NewGuid();

        handler.When(HttpMethod.Post, $"/api/fleet/maintenance/{maintenanceId}/complete")
               .Respond(HttpStatusCode.NoContent);

        var model = new BackOffice.Pages.Admin.Fleet.IndexModel(factory, MockHttpHelper.NullLogger<BackOffice.Pages.Admin.Fleet.IndexModel>())
        {
            TempData = CreateTempData()
        };

        var result = await model.OnPostCompleteMaintenanceAsync(maintenanceId);

        Assert.IsType<RedirectToPageResult>(result);
    }
}
