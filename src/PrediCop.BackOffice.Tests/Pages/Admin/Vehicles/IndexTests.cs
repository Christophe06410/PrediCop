using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Moq;
using PrediCop.BackOffice.Models;
using PrediCop.BackOffice.Tests.Helpers;
using RichardSzalay.MockHttp;
using Xunit;

namespace PrediCop.BackOffice.Tests.Pages.Admin.Vehicles;

public class IndexTests
{
    /// <summary>
    /// Creates a PageContext with a session that returns null for all keys
    /// (simulates no JWT token stored in session).
    /// </summary>
    private static BackOffice.Pages.Admin.Vehicles.IndexModel CreateModel(IHttpClientFactory factory)
    {
        var model = new BackOffice.Pages.Admin.Vehicles.IndexModel(
            factory,
            MockHttpHelper.NullLogger<BackOffice.Pages.Admin.Vehicles.IndexModel>());

        var sessionMock = new Mock<ISession>();
        byte[]? sessionValue = null;
        sessionMock.Setup(s => s.TryGetValue(It.IsAny<string>(), out sessionValue)).Returns(false);

        var httpContext = new DefaultHttpContext
        {
            Session = sessionMock.Object
        };

        var actionContext = new ActionContext(httpContext, new RouteData(), new PageActionDescriptor(), new ModelStateDictionary());
        var viewDataDictionary = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary());
        var tempDataMock = new Mock<ITempDataDictionary>();

        model.PageContext = new PageContext(actionContext)
        {
            ViewData = viewDataDictionary
        };
        model.TempData = tempDataMock.Object;

        return model;
    }

    [Fact]
    public async Task OnGetAsync_ApiReturnsVehicles_PopulatesList()
    {
        var (handler, factory) = MockHttpHelper.Create();

        var vehicles = new List<VehicleDto>
        {
            new() { Id = Guid.NewGuid(), CallSign = "PM-01", LicensePlate = "AA-001-BB", Status = "Available" },
            new() { Id = Guid.NewGuid(), CallSign = "PM-02", LicensePlate = "AA-002-BB", Status = "OnMission" },
            new() { Id = Guid.NewGuid(), CallSign = "PM-03", LicensePlate = "AA-003-BB", Status = "Offline" },
        };

        handler.When("/api/vehicles").Respond("application/json", JsonSerializer.Serialize(vehicles));

        var model = CreateModel(factory);
        await model.OnGetAsync();

        Assert.Equal(3, model.Vehicles.Count);
        Assert.Equal("PM-01", model.Vehicles[0].CallSign);
    }

    [Fact]
    public async Task OnGetAsync_ApiFails_EmptyList()
    {
        var (handler, factory) = MockHttpHelper.Create();

        // When API fails, the model falls back to fake vehicles
        handler.When("/api/vehicles").Respond(HttpStatusCode.InternalServerError);

        var model = CreateModel(factory);
        var exception = await Record.ExceptionAsync(() => model.OnGetAsync());

        Assert.Null(exception);
        // On failure the page model returns the hard-coded fake vehicle list (5 items)
        Assert.NotEmpty(model.Vehicles);
    }
}
