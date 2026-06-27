using System.Net;
using Microsoft.AspNetCore.Mvc;
using PrediCop.BackOffice.Pages.Missions;
using PrediCop.BackOffice.Tests.Helpers;
using Xunit;

namespace PrediCop.BackOffice.Tests.Pages.Missions;

public class DetailsTests
{
    [Fact]
    public async Task OnPostCancelAsync_WithoutReason_RedirectsWithError()
    {
        var (_, factory) = MockHttpHelper.Create();
        var missionId = Guid.NewGuid();

        var model = new DetailsModel(factory, MockHttpHelper.NullLogger<DetailsModel>())
            .WithPageContext();

        var result = await model.OnPostCancelAsync(missionId, " ", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(missionId, redirect.RouteValues?["id"]);
        Assert.Equal("Le motif d'annulation est requis.", model.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task OnPostCancelAsync_ApiSuccess_RedirectsWithSuccess()
    {
        var (handler, factory) = MockHttpHelper.Create();
        var missionId = Guid.NewGuid();

        handler.Expect(HttpMethod.Post, $"https://api.test/api/missions/{missionId}/cancel")
               .Respond(HttpStatusCode.OK, "application/json", "{}");

        var model = new DetailsModel(factory, MockHttpHelper.NullLogger<DetailsModel>())
            .WithPageContext();

        var result = await model.OnPostCancelAsync(missionId, "Plus de besoin", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(missionId, redirect.RouteValues?["id"]);
        Assert.Equal("Mission annulée.", model.TempData["SuccessMessage"]);
    }
}
