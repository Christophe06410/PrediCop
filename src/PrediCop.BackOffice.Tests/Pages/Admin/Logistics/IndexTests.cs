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

namespace PrediCop.BackOffice.Tests.Pages.Admin.Logistics;

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

    [Fact]
    public async Task OnGetAsync_ApiReturnsCatalog_PopulatesCatalog()
    {
        var (handler, factory) = MockHttpHelper.Create();

        var catalog = new List<EquipmentCatalogResponse>
        {
            new(Guid.NewGuid(), "Gilet pare-balles", EquipmentCategory.EquipementProtection, null, "unité", 60, "GPB-001", true),
            new(Guid.NewGuid(), "Radio portative", EquipmentCategory.Materiel, null, "unité", 48, "RAD-001", true),
            new(Guid.NewGuid(), "Uniforme été", EquipmentCategory.Uniforme, null, "tenue", 24, "UNI-ETE", true),
        };

        handler.When("/api/users").Respond("application/json", "[]");
        handler.When("/api/logistics/catalog*").Respond("application/json", JsonSerializer.Serialize(catalog, JsonOpts));
        handler.When("/api/logistics/issuances*").Respond("application/json", "[]");
        handler.When("/api/logistics/alerts").Respond("application/json", "[]");

        var model = new BackOffice.Pages.Admin.Logistics.IndexModel(factory, MockHttpHelper.NullLogger<BackOffice.Pages.Admin.Logistics.IndexModel>());
        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal(3, model.Catalog.Count);
    }

    [Fact]
    public async Task OnGetAsync_ApiFails_EmptyLists()
    {
        var (handler, factory) = MockHttpHelper.Create();

        handler.When("*").Respond(HttpStatusCode.InternalServerError);

        var model = new BackOffice.Pages.Admin.Logistics.IndexModel(factory, MockHttpHelper.NullLogger<BackOffice.Pages.Admin.Logistics.IndexModel>());
        var exception = await Record.ExceptionAsync(() => model.OnGetAsync(CancellationToken.None));

        Assert.Null(exception);
        Assert.Empty(model.Catalog);
        Assert.Empty(model.Issuances);
        Assert.Empty(model.Alerts);
        Assert.Empty(model.Agents);
    }

    [Fact]
    public async Task OnGetAsync_ExpiringCount_SetFromAlerts()
    {
        var (handler, factory) = MockHttpHelper.Create();

        var alerts = new List<LogisticsAlertResponse>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "Agent One", "Gilet pare-balles", DateTime.UtcNow.AddDays(10), false),
            new(Guid.NewGuid(), Guid.NewGuid(), "Agent Two", "Radio portative", DateTime.UtcNow.AddDays(5), false),
            new(Guid.NewGuid(), Guid.NewGuid(), "Agent Three", "Uniforme été", DateTime.UtcNow.AddDays(-1), true),
            new(Guid.NewGuid(), Guid.NewGuid(), "Agent Four", "Gilet pare-balles", DateTime.UtcNow.AddDays(20), false),
            new(Guid.NewGuid(), Guid.NewGuid(), "Agent Five", "Radio portative", DateTime.UtcNow.AddDays(30), false),
        };

        handler.When("/api/users").Respond("application/json", "[]");
        handler.When("/api/logistics/catalog*").Respond("application/json", "[]");
        handler.When("/api/logistics/issuances*").Respond("application/json", "[]");
        handler.When("/api/logistics/alerts").Respond("application/json", JsonSerializer.Serialize(alerts, JsonOpts));

        var model = new BackOffice.Pages.Admin.Logistics.IndexModel(factory, MockHttpHelper.NullLogger<BackOffice.Pages.Admin.Logistics.IndexModel>());
        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal(5, model.ExpiringCount);
    }

    [Fact]
    public async Task OnPostReturnAsync_Success_Redirects()
    {
        var (handler, factory) = MockHttpHelper.Create();
        var issuanceId = Guid.NewGuid();

        handler.When(HttpMethod.Post, $"/api/logistics/issuances/{issuanceId}/return")
               .Respond(HttpStatusCode.NoContent);

        var model = new BackOffice.Pages.Admin.Logistics.IndexModel(factory, MockHttpHelper.NullLogger<BackOffice.Pages.Admin.Logistics.IndexModel>())
        {
            TempData = CreateTempData()
        };

        var result = await model.OnPostReturnAsync(issuanceId, CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
    }
}
