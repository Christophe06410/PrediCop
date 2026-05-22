using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RichardSzalay.MockHttp;

namespace PrediCop.BackOffice.Tests.Helpers;

public static class MockHttpHelper
{
    public static (MockHttpMessageHandler handler, IHttpClientFactory factory) Create(string baseAddress = "https://api.test")
    {
        var handler = new MockHttpMessageHandler();
        var client = handler.ToHttpClient();
        client.BaseAddress = new Uri(baseAddress);

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("PrediCopApi")).Returns(client);

        return (handler, factory.Object);
    }

    public static ILogger<T> NullLogger<T>() => Microsoft.Extensions.Logging.Abstractions.NullLogger<T>.Instance;

    /// <summary>
    /// Initialises TempData and ViewData on a PageModel so that tests work even when
    /// the page accesses TempData or ViewData in catch blocks.
    /// </summary>
    public static TPageModel WithPageContext<TPageModel>(this TPageModel model) where TPageModel : PageModel
    {
        var httpContext = new DefaultHttpContext();

        var tempDataProvider = new Mock<ITempDataProvider>();
        tempDataProvider
            .Setup(p => p.LoadTempData(It.IsAny<HttpContext>()))
            .Returns(new Dictionary<string, object?>());

        var tempData = new TempDataDictionary(httpContext, tempDataProvider.Object);

        var actionContext = new ActionContext(
            httpContext,
            new Microsoft.AspNetCore.Routing.RouteData(),
            new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor(),
            new ModelStateDictionary()
        );

        var pageContext = new PageContext(actionContext)
        {
            ViewData = new ViewDataDictionary(
                new EmptyModelMetadataProvider(),
                new ModelStateDictionary()
            )
        };

        model.PageContext = pageContext;
        model.TempData = tempData;

        return model;
    }
}
