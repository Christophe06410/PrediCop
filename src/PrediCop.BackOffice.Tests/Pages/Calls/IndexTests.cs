using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.BackOffice.Models;
using PrediCop.BackOffice.Pages.Calls;
using PrediCop.BackOffice.Tests.Helpers;
using RichardSzalay.MockHttp;
using Xunit;

namespace PrediCop.BackOffice.Tests.Pages.Calls;

public class IndexTests
{
    private static string MakePagedResultJson(List<CallDto> items, int totalCount)
    {
        var result = new
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = 1,
            PageSize = 20
        };
        return JsonSerializer.Serialize(result);
    }

    [Fact]
    public async Task OnGetAsync_ApiReturnsCalls_PopulatesCallsList()
    {
        var (handler, factory) = MockHttpHelper.Create();

        var calls = new List<CallDto>
        {
            new() { Id = Guid.NewGuid(), Reference = "MC-001", Status = "Open" },
            new() { Id = Guid.NewGuid(), Reference = "MC-002", Status = "InProgress" },
            new() { Id = Guid.NewGuid(), Reference = "MC-003", Status = "Closed" }
        };

        handler.When("*").Respond("application/json", MakePagedResultJson(calls, 3));

        var model = new IndexModel(factory, MockHttpHelper.NullLogger<IndexModel>());
        await model.OnGetAsync();

        Assert.Equal(3, model.Calls.Count);
        Assert.Equal(3, model.TotalCount);
    }

    [Fact]
    public async Task OnGetAsync_ApiFails_ReturnsEmptyList()
    {
        var (handler, factory) = MockHttpHelper.Create();

        handler.When("*").Respond(HttpStatusCode.InternalServerError);

        var model = new IndexModel(factory, MockHttpHelper.NullLogger<IndexModel>())
            .WithPageContext();
        var exception = await Record.ExceptionAsync(() => model.OnGetAsync());

        Assert.Null(exception);
        Assert.Empty(model.Calls);
    }

    [Fact]
    public async Task OnGetAsync_WithFilters_BuildsCorrectUrl()
    {
        var (handler, factory) = MockHttpHelper.Create();

        string? capturedUrl = null;

        handler.When("*")
               .With(req =>
               {
                   capturedUrl = req.RequestUri?.PathAndQuery;
                   return true;
               })
               .Respond("application/json", MakePagedResultJson([], 0));

        var model = new IndexModel(factory, MockHttpHelper.NullLogger<IndexModel>())
        {
            FilterDate = "2026-05-20",
            FilterStatus = "Open"
        };

        await model.OnGetAsync();

        Assert.NotNull(capturedUrl);
        Assert.Contains("date=2026-05-20", capturedUrl);
        Assert.Contains("status=Open", capturedUrl);
    }

    [Fact]
    public async Task OnGetAsync_WithPriorityFilter_BuildsCorrectUrl()
    {
        var (handler, factory) = MockHttpHelper.Create();

        string? capturedUrl = null;

        handler.When("*")
               .With(req =>
               {
                   capturedUrl = req.RequestUri?.PathAndQuery;
                   return true;
               })
               .Respond("application/json", MakePagedResultJson([], 0));

        var model = new IndexModel(factory, MockHttpHelper.NullLogger<IndexModel>())
        {
            FilterPriority = "SOS"
        };

        await model.OnGetAsync();

        Assert.NotNull(capturedUrl);
        Assert.Contains("priority=SOS", capturedUrl);
    }

    [Fact]
    public async Task OnGetAsync_CallsWithPriority_HasCorrectPriorityValues()
    {
        var (handler, factory) = MockHttpHelper.Create();

        var calls = new List<CallDto>
        {
            new() { Id = Guid.NewGuid(), Reference = "MC-001", Status = "Open", Priority = "SOS" },
            new() { Id = Guid.NewGuid(), Reference = "MC-002", Status = "InProgress", Priority = "Urgent" }
        };

        handler.When("*").Respond("application/json", MakePagedResultJson(calls, 2));

        var model = new IndexModel(factory, MockHttpHelper.NullLogger<IndexModel>());
        await model.OnGetAsync();

        Assert.Equal(2, model.Calls.Count);
        Assert.Contains(model.Calls, c => c.Priority == "SOS");
        Assert.Contains(model.Calls, c => c.Priority == "Urgent");
    }
}
