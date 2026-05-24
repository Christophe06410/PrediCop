using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PrediCop.BackOffice.Pages.Map;
using PrediCop.BackOffice.Tests.Helpers;
using RichardSzalay.MockHttp;
using Xunit;

namespace PrediCop.BackOffice.Tests.Pages.Map;

public class IndexTests
{
    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    // ──────────────────────────────────────────────────────────────
    // OnGetVehiclesJson
    // ──────────────────────────────────────────────────────────────

    private static string BuildVehicleJson(string callSign, string status, double? lat, double? lng, string? indicatif = null, string? patrolType = null)
    {
        var id = Guid.NewGuid();
        var latStr    = lat.HasValue    ? lat.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "null";
        var lngStr    = lng.HasValue    ? lng.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "null";
        var indStr    = indicatif   != null ? $"\"{indicatif}\""   : "null";
        var typeStr   = patrolType  != null ? $"\"{patrolType}\""  : "null";
        return $"{{\"id\":\"{id}\",\"callSign\":\"{callSign}\",\"status\":\"{status}\"," +
               $"\"lastLatitude\":{latStr},\"lastLongitude\":{lngStr}," +
               $"\"lastPositionUpdate\":null,\"officerNames\":[]," +
               $"\"indicatif\":{indStr},\"patrolType\":{typeStr}}}";
    }

    [Fact]
    public async Task OnGetVehiclesJson_ReturnsOnlyPositionedVehicles()
    {
        var (handler, factory) = MockHttpHelper.Create();

        var vehiclesJson =
            $"[{BuildVehicleJson("PM-01", "Available", 43.6, 1.44)}," +
            $"{BuildVehicleJson("PM-02", "Offline",    null, null)}]";

        handler.When("/api/vehicles").Respond("application/json", vehiclesJson);
        handler.When("/api/missions/active").Respond("application/json", "[]");

        var model = new IndexModel(factory, MockHttpHelper.NullLogger<IndexModel>())
            .WithPageContext();

        var result = await model.OnGetVehiclesJsonAsync();

        var json = Assert.IsType<JsonResult>(result);
        var list = Assert.IsAssignableFrom<IEnumerable<object>>(json.Value);
        Assert.Single(list); // Only PM-01 has GPS coordinates
    }

    [Fact]
    public async Task OnGetVehiclesJson_VehicleWithIndicatif_IsIncluded()
    {
        var (handler, factory) = MockHttpHelper.Create();

        var vehiclesJson = $"[{BuildVehicleJson("VP-01", "Available", 43.6, 1.44, "Sierra 1", "Car")}]";

        handler.When("/api/vehicles").Respond("application/json", vehiclesJson);
        handler.When("/api/missions/active").Respond("application/json", "[]");

        var model = new IndexModel(factory, MockHttpHelper.NullLogger<IndexModel>())
            .WithPageContext();

        var result = await model.OnGetVehiclesJsonAsync();

        var json = Assert.IsType<JsonResult>(result);
        var list = Assert.IsAssignableFrom<IEnumerable<object>>(json.Value);
        Assert.Single(list);
    }

    [Fact]
    public async Task OnGetVehiclesJson_ApiFails_ReturnsEmptyArray()
    {
        var (handler, factory) = MockHttpHelper.Create();

        handler.When("*").Respond(HttpStatusCode.InternalServerError);

        var model = new IndexModel(factory, MockHttpHelper.NullLogger<IndexModel>())
            .WithPageContext();

        var result = await model.OnGetVehiclesJsonAsync();

        var json = Assert.IsType<JsonResult>(result);
        Assert.NotNull(json.Value);
    }

    // ──────────────────────────────────────────────────────────────
    // OnGetAgentPositions
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task OnGetAgentPositions_ReturnsAgentList()
    {
        var (handler, factory) = MockHttpHelper.Create();

        var agents = new[]
        {
            new { userId = Guid.NewGuid(), fullName = "Jean Martin", badgeNumber = "PM-001",
                  isLeader = true, latitude = 43.601, longitude = 1.441,
                  updatedAt = DateTime.UtcNow, patrolIndicatif = "Sierra 1" },
            new { userId = Guid.NewGuid(), fullName = "Sophie Durand", badgeNumber = "PM-002",
                  isLeader = false, latitude = 43.602, longitude = 1.442,
                  updatedAt = DateTime.UtcNow, patrolIndicatif = "Sierra 1" },
        };

        handler.When("/api/patrol/live-agents")
               .Respond("application/json", JsonSerializer.Serialize(agents));

        var model = new IndexModel(factory, MockHttpHelper.NullLogger<IndexModel>())
            .WithPageContext();

        var result = await model.OnGetAgentPositionsAsync();

        var json = Assert.IsType<JsonResult>(result);
        var list = Assert.IsAssignableFrom<IEnumerable<object>>(json.Value);
        Assert.Equal(2, list.Count());
    }

    [Fact]
    public async Task OnGetAgentPositions_ApiFails_ReturnsEmptyArray()
    {
        var (handler, factory) = MockHttpHelper.Create();

        handler.When("/api/patrol/live-agents").Respond(HttpStatusCode.InternalServerError);

        var model = new IndexModel(factory, MockHttpHelper.NullLogger<IndexModel>())
            .WithPageContext();

        var result = await model.OnGetAgentPositionsAsync();

        var json = Assert.IsType<JsonResult>(result);
        Assert.NotNull(json.Value);
    }

    // ──────────────────────────────────────────────────────────────
    // OnGetAsync (page init)
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task OnGetAsync_AllApisCalled_PageLoadsWithoutException()
    {
        var (handler, factory) = MockHttpHelper.Create();

        handler.When("/api/streets").Respond("application/json", "[]");
        handler.When("/api/vehicles").Respond("application/json", "[]");
        handler.When("/api/missions/active").Respond("application/json", "[]");
        handler.When("/api/geozones").Respond("application/json", "[]");

        var model = new IndexModel(factory, MockHttpHelper.NullLogger<IndexModel>())
            .WithPageContext();

        var exception = await Record.ExceptionAsync(() => model.OnGetAsync());

        Assert.Null(exception);
        Assert.Equal("[]", model.VehiclesJson);
        Assert.Equal("[]", model.StreetsJson);
    }
}
