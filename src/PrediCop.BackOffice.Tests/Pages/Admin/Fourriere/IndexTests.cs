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

namespace PrediCop.BackOffice.Tests.Pages.Admin.Fourriere;

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

    private static ImpoundedVehicleResponse MakeVehicle(string plate) =>
        new(
            Guid.NewGuid(), plate, "Peugeot", "208", "Blanc", VehicleCategory.VoitureParticuliere,
            ImpoundReason.StationnementGenant, DateTime.UtcNow.AddDays(-2),
            Guid.NewGuid(), "Agent Test", "PM-001",
            "12 rue de la Paix", "Fourrière centrale",
            null, null, null, null,
            ImpoundStatus.InStorage,
            null, null, null, null, null, DateTime.UtcNow.AddDays(-2)
        );

    [Fact]
    public async Task OnGetAsync_ApiReturnsVehicles_PopulatesList()
    {
        var (handler, factory) = MockHttpHelper.Create();

        var vehicles = Enumerable.Range(1, 4)
            .Select(i => MakeVehicle($"AB-{i:D3}-CD"))
            .ToList();

        var stats = new FourriereStatsResponse(4, 1, 0,
            new Dictionary<string, int> { { "StationnementGenant", 4 } },
            new Dictionary<string, int> { { "InStorage", 4 } });

        handler.When("/api/users").Respond("application/json", "[]");
        handler.When("/api/fourriere/stats").Respond("application/json", JsonSerializer.Serialize(stats, JsonOpts));
        handler.When("/api/fourriere*").Respond("application/json", JsonSerializer.Serialize(vehicles, JsonOpts));

        var model = new BackOffice.Pages.Admin.Fourriere.IndexModel(factory, MockHttpHelper.NullLogger<BackOffice.Pages.Admin.Fourriere.IndexModel>());
        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal(4, model.Vehicles.Count);
    }

    [Fact]
    public async Task OnGetAsync_ApiFails_EmptyLists()
    {
        var (handler, factory) = MockHttpHelper.Create();

        handler.When("*").Respond(HttpStatusCode.InternalServerError);

        // Fourriere.OnGetAsync writes to TempData in the catch block — must be provided
        var model = new BackOffice.Pages.Admin.Fourriere.IndexModel(factory, MockHttpHelper.NullLogger<BackOffice.Pages.Admin.Fourriere.IndexModel>())
        {
            TempData = CreateTempData()
        };
        var exception = await Record.ExceptionAsync(() => model.OnGetAsync(CancellationToken.None));

        Assert.Null(exception);
        Assert.Empty(model.Vehicles);
        Assert.Empty(model.Agents);
        Assert.Null(model.Stats);
    }

    [Fact]
    public async Task OnGetAsync_Stats_AreLoaded()
    {
        var (handler, factory) = MockHttpHelper.Create();

        var stats = new FourriereStatsResponse(10, 3, 1,
            new Dictionary<string, int> { { "Epave", 5 }, { "StationnementDangereux", 5 } },
            new Dictionary<string, int> { { "InStorage", 10 } });

        handler.When("/api/users").Respond("application/json", "[]");
        handler.When("/api/fourriere/stats").Respond("application/json", JsonSerializer.Serialize(stats, JsonOpts));
        handler.When("/api/fourriere*").Respond("application/json", "[]");

        var model = new BackOffice.Pages.Admin.Fourriere.IndexModel(factory, MockHttpHelper.NullLogger<BackOffice.Pages.Admin.Fourriere.IndexModel>());
        await model.OnGetAsync(CancellationToken.None);

        Assert.NotNull(model.Stats);
        Assert.Equal(10, model.Stats!.TotalInStorage);
    }

    [Fact]
    public async Task OnPostReleaseAsync_Success_Redirects()
    {
        var (handler, factory) = MockHttpHelper.Create();
        var vehicleId = Guid.NewGuid();

        handler.When(HttpMethod.Post, $"/api/fourriere/{vehicleId}/release")
               .Respond(HttpStatusCode.OK);

        var model = new BackOffice.Pages.Admin.Fourriere.IndexModel(factory, MockHttpHelper.NullLogger<BackOffice.Pages.Admin.Fourriere.IndexModel>())
        {
            TempData = CreateTempData()
        };

        var result = await model.OnPostReleaseAsync(vehicleId, "Jean Dupont", "CNI-123456", CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
    }

    [Fact]
    public async Task OnPostDestroyAsync_Success_Redirects()
    {
        var (handler, factory) = MockHttpHelper.Create();
        var vehicleId = Guid.NewGuid();

        handler.When(HttpMethod.Post, $"/api/fourriere/{vehicleId}/destroy")
               .Respond(HttpStatusCode.OK);

        var model = new BackOffice.Pages.Admin.Fourriere.IndexModel(factory, MockHttpHelper.NullLogger<BackOffice.Pages.Admin.Fourriere.IndexModel>())
        {
            TempData = CreateTempData()
        };

        var result = await model.OnPostDestroyAsync(vehicleId, CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
    }
}
