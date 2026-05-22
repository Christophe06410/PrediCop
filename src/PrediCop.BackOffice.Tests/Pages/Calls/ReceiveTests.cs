using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PrediCop.BackOffice.Models;
using PrediCop.BackOffice.Pages.Calls;
using PrediCop.BackOffice.Tests.Helpers;
using RichardSzalay.MockHttp;
using Xunit;

namespace PrediCop.BackOffice.Tests.Pages.Calls;

public class ReceiveTests
{
    private static string MakePagedResultJson(List<CallDto> items)
    {
        var result = new { Items = items, TotalCount = items.Count, PageNumber = 1, PageSize = 50 };
        return JsonSerializer.Serialize(result);
    }

    [Fact]
    public async Task OnGetAsync_LoadsPageCorrectly()
    {
        var (handler, factory) = MockHttpHelper.Create();

        // LoadTodayCallsAsync is called during OnGet
        handler.When("*").Respond("application/json", MakePagedResultJson([]));

        var model = new ReceiveModel(factory, MockHttpHelper.NullLogger<ReceiveModel>())
            .WithPageContext();
        var result = await model.OnGetAsync();

        Assert.IsType<PageResult>(result);
    }

    [Fact]
    public async Task OnPostCreateMissionAsync_ApiSuccess_RedirectsToIndex()
    {
        var (handler, factory) = MockHttpHelper.Create();

        var createdCall = new CallDto
        {
            Id = Guid.NewGuid(),
            Reference = "MC-2026-001",
            Status = "Open"
        };
        var callJson = JsonSerializer.Serialize(createdCall);

        // Step 1: POST /api/calls → 201 Created with CallDto body
        handler.Expect(HttpMethod.Post, "https://api.test/api/calls")
               .Respond(HttpStatusCode.Created, "application/json", callJson);

        // Step 2: POST /api/calls/{id}/create-mission → 200 OK
        handler.Expect(HttpMethod.Post, $"https://api.test/api/calls/{createdCall.Id}/create-mission")
               .Respond(HttpStatusCode.OK, "application/json", "{}");

        var model = new ReceiveModel(factory, MockHttpHelper.NullLogger<ReceiveModel>())
        {
            Input = new CreateCallDto
            {
                CallerName = "Jean Dupont",
                CallerPhone = "0600000001",
                IncidentCategory = "Tapage",
                IncidentDescription = "Tapage nocturne",
                IncidentAddress = "1 rue de la Paix"
            }
        };
        model.WithPageContext();

        var result = await model.OnPostCreateMissionAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Calls/Index", redirect.PageName);
    }

    [Fact]
    public async Task OnPostCreateMissionAsync_ApiFails_StaysOnPageWithError()
    {
        var (handler, factory) = MockHttpHelper.Create();

        // POST /api/calls → 400 Bad Request
        handler.When(HttpMethod.Post, "https://api.test/api/calls")
               .Respond(HttpStatusCode.BadRequest, "application/json", "{\"error\":\"invalid\"}");

        // LoadTodayCallsAsync fallback (called when staying on page)
        handler.When(HttpMethod.Get, "*")
               .Respond("application/json", MakePagedResultJson([]));

        var model = new ReceiveModel(factory, MockHttpHelper.NullLogger<ReceiveModel>())
        {
            Input = new CreateCallDto
            {
                CallerName = "Jean Dupont",
                CallerPhone = "0600000001",
                IncidentCategory = "Tapage",
                IncidentDescription = "Tapage nocturne",
                IncidentAddress = "1 rue de la Paix"
            }
        };
        model.WithPageContext();

        var result = await model.OnPostCreateMissionAsync();

        Assert.IsType<PageResult>(result);
        Assert.False(model.ModelState.IsValid);
    }

    [Fact]
    public async Task OnPostCreateMissionAsync_WithSOSPriority_SendsCorrectPriority()
    {
        var (handler, factory) = MockHttpHelper.Create();

        string? capturedBody = null;

        var createdCall = new CallDto
        {
            Id = Guid.NewGuid(),
            Reference = "MC-2026-SOS",
            Status = "Open",
            Priority = "SOS"
        };
        var callJson = JsonSerializer.Serialize(createdCall);

        handler.Expect(HttpMethod.Post, "https://api.test/api/calls")
               .With(req =>
               {
                   capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                   return true;
               })
               .Respond(HttpStatusCode.Created, "application/json", callJson);

        handler.Expect(HttpMethod.Post, $"https://api.test/api/calls/{createdCall.Id}/create-mission")
               .Respond(HttpStatusCode.OK, "application/json", "{}");

        var model = new ReceiveModel(factory, MockHttpHelper.NullLogger<ReceiveModel>())
        {
            Input = new CreateCallDto
            {
                CallerName = "Marie Martin",
                CallerPhone = "0600000002",
                IncidentCategory = "Bagarre",
                IncidentDescription = "Rixe en cours",
                IncidentAddress = "12 avenue de la Liberté",
                Priority = "SOS"
            }
        };
        model.WithPageContext();

        await model.OnPostCreateMissionAsync();

        Assert.NotNull(capturedBody);
        Assert.Contains("SOS", capturedBody, StringComparison.OrdinalIgnoreCase);
    }
}
