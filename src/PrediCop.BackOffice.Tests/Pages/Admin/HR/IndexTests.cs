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

namespace PrediCop.BackOffice.Tests.Pages.Admin.HR;

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
    public async Task OnGetAsync_ApiReturnsLeaves_PopulatesLeavesList()
    {
        var (handler, factory) = MockHttpHelper.Create();

        var leaves = new List<LeaveResponse>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "Agent One", "PM-001", LeaveType.CongesPayes,
                DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today.AddDays(5)),
                LeaveStatus.Approved, DateTime.UtcNow, null, null, null, null),
            new(Guid.NewGuid(), Guid.NewGuid(), "Agent Two", "PM-002", LeaveType.RTT,
                DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today.AddDays(2)),
                LeaveStatus.Pending, DateTime.UtcNow, null, null, null, null),
        };

        handler.When("/api/users").Respond("application/json", "[]");
        handler.When("/api/hr/leaves").Respond("application/json", JsonSerializer.Serialize(leaves, JsonOpts));
        handler.When("/api/hr/schedules*").Respond("application/json", "[]");

        var model = new BackOffice.Pages.Admin.HR.IndexModel(factory, MockHttpHelper.NullLogger<BackOffice.Pages.Admin.HR.IndexModel>());
        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal(2, model.Leaves.Count);
    }

    [Fact]
    public async Task OnGetAsync_ApiFails_AllListsAreEmpty()
    {
        var (handler, factory) = MockHttpHelper.Create();

        handler.When("*").Respond(HttpStatusCode.InternalServerError);

        var model = new BackOffice.Pages.Admin.HR.IndexModel(factory, MockHttpHelper.NullLogger<BackOffice.Pages.Admin.HR.IndexModel>());
        var exception = await Record.ExceptionAsync(() => model.OnGetAsync(CancellationToken.None));

        Assert.Null(exception);
        Assert.Empty(model.Leaves);
        Assert.Empty(model.Schedules);
        Assert.Empty(model.Agents);
    }

    [Fact]
    public async Task OnGetAsync_PendingLeavesCount_IsCountedCorrectly()
    {
        var (handler, factory) = MockHttpHelper.Create();

        var leaves = new List<LeaveResponse>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "Agent One", "PM-001", LeaveType.CongesPayes,
                DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today.AddDays(5)),
                LeaveStatus.Pending, DateTime.UtcNow, null, null, null, null),
            new(Guid.NewGuid(), Guid.NewGuid(), "Agent Two", "PM-002", LeaveType.RTT,
                DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today.AddDays(2)),
                LeaveStatus.Pending, DateTime.UtcNow, null, null, null, null),
            new(Guid.NewGuid(), Guid.NewGuid(), "Agent Three", "PM-003", LeaveType.Maladie,
                DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today.AddDays(3)),
                LeaveStatus.Approved, DateTime.UtcNow, DateTime.UtcNow, "Manager", null, null),
        };

        handler.When("/api/users").Respond("application/json", "[]");
        handler.When("/api/hr/leaves").Respond("application/json", JsonSerializer.Serialize(leaves, JsonOpts));
        handler.When("/api/hr/schedules*").Respond("application/json", "[]");

        var model = new BackOffice.Pages.Admin.HR.IndexModel(factory, MockHttpHelper.NullLogger<BackOffice.Pages.Admin.HR.IndexModel>());
        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal(2, model.PendingLeavesCount);
    }

    [Fact]
    public async Task OnPostApproveLeaveAsync_Success_Redirects()
    {
        var (handler, factory) = MockHttpHelper.Create();
        var leaveId = Guid.NewGuid();

        handler.When(HttpMethod.Post, $"/api/hr/leaves/{leaveId}/approve")
               .Respond(HttpStatusCode.NoContent);

        var model = new BackOffice.Pages.Admin.HR.IndexModel(factory, MockHttpHelper.NullLogger<BackOffice.Pages.Admin.HR.IndexModel>())
        {
            TempData = CreateTempData()
        };

        var result = await model.OnPostApproveLeaveAsync(leaveId, CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
    }

    [Fact]
    public async Task OnPostRejectLeaveAsync_Success_Redirects()
    {
        var (handler, factory) = MockHttpHelper.Create();
        var leaveId = Guid.NewGuid();

        handler.When(HttpMethod.Post, $"/api/hr/leaves/{leaveId}/reject")
               .Respond(HttpStatusCode.NoContent);

        var model = new BackOffice.Pages.Admin.HR.IndexModel(factory, MockHttpHelper.NullLogger<BackOffice.Pages.Admin.HR.IndexModel>())
        {
            TempData = CreateTempData()
        };

        var result = await model.OnPostRejectLeaveAsync(leaveId, "Non justifié", CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
    }
}
