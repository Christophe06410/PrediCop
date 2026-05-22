using Microsoft.Playwright;
using Xunit;

namespace PrediCop.BackOffice.UITests.Infrastructure;

public abstract class PlaywrightBaseTest : IClassFixture<PlaywrightFixture>, IAsyncLifetime
{
    protected readonly PlaywrightFixture Fixture;

    public IPage Page { get; private set; } = null!;
    public IBrowserContext Context { get; private set; } = null!;

    protected string BaseUrl =>
        Environment.GetEnvironmentVariable("PREDICOP_BASE_URL") ?? "https://localhost:7218";

    protected string TestEmail =>
        Environment.GetEnvironmentVariable("PREDICOP_TEST_EMAIL") ?? "admin@predicop.local";

    protected string TestPassword =>
        Environment.GetEnvironmentVariable("PREDICOP_TEST_PASSWORD") ?? "Admin1234!";

    protected string TestCitySlug =>
        Environment.GetEnvironmentVariable("PREDICOP_TEST_CITY") ?? "test";

    protected PlaywrightBaseTest(PlaywrightFixture fixture)
    {
        Fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        Context = await Fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize { Width = 1280, Height = 800 }
        });
        Page = await Context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await Context.DisposeAsync();
    }

    protected async Task LoginAsync()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.FillAsync("[name='Email']", TestEmail);
        await Page.FillAsync("[name='Password']", TestPassword);

        // CitySlug : si c'est un input text, le remplir ; si c'est un select, le sélectionner
        var cityInput = Page.Locator("[name='CitySlug']");
        var tagName = await cityInput.EvaluateAsync<string>("el => el.tagName.toLowerCase()");
        if (tagName == "select")
            await cityInput.SelectOptionAsync(new SelectOptionValue { Value = TestCitySlug });
        else
            await cityInput.FillAsync(TestCitySlug);

        await Page.ClickAsync("button[type='submit']");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 15000 });
    }

    protected bool IsOnLoginPage() => Page.Url.Contains("/Account/Login");

    protected async Task AssertPageLoaded(string titleContains)
    {
        var title = await Page.TitleAsync();
        Assert.Contains(titleContains, title, StringComparison.OrdinalIgnoreCase);

        var hasServerError = await Page.Locator("div.text-danger").IsVisibleAsync();
        if (hasServerError)
        {
            var errorText = await Page.Locator("div.text-danger").InnerTextAsync();
            Assert.False(
                errorText.Contains("500") || errorText.Contains("Internal Server Error"),
                $"La page affiche une erreur HTTP 500 : {errorText}");
        }
    }
}
