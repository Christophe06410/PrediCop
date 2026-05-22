using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using PrediCop.BackOffice;
using PrediCop.BackOffice.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AddFolderApplicationModelConvention("/", m =>
        m.Filters.Add(new TypeFilterAttribute(typeof(JwtRequiredFilter))));
    options.Conventions.AllowAnonymousToFolder("/Public");
    options.Conventions.AllowAnonymousToFolder("/Subscription");
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(
            builder.Configuration.GetValue<int>("Authentication:CookieExpireHours", 8));
        options.SlidingExpiration = true;
    });

var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7229";

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<JwtSessionHandler>();
builder.Services.AddTransient<JwtRequiredFilter>();

builder.Services.AddHttpClient("PrediCopApi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
})
.AddHttpMessageHandler<JwtSessionHandler>();

builder.Services.AddHttpClient("PrediCopApiAnon", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
});

builder.Services.AddHttpClient("OsmTiles", c =>
{
    c.BaseAddress = new Uri("https://tile.openstreetmap.org");
    c.DefaultRequestHeaders.UserAgent.ParseAdd("PrediCop/1.0 (police-municipale-saas)");
    c.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddHttpClient("Anthropic", c =>
{
    c.BaseAddress = new Uri("https://api.anthropic.com");
    c.Timeout = TimeSpan.FromSeconds(45);
});

builder.Services.AddTransient<MapSnapshotService>();
builder.Services.AddTransient<TextAssistantService>();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

var pathBase = builder.Configuration["PathBase"];
if (!string.IsNullOrEmpty(pathBase))
    app.UsePathBase(pathBase);

app.UseHttpsRedirection();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

// Proxy group — forwards browser JS requests to the API with the session JWT
var proxy = app.MapGroup("/api/proxy").RequireAuthorization();

proxy.MapPost("/missions/{id:guid}/intervenants", async (
    Guid id, HttpContext ctx, IHttpClientFactory factory, CancellationToken ct) =>
{
    var client = factory.CreateClient("PrediCopApi");
    using var body = new StreamContent(ctx.Request.Body);
    body.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
    var r = await client.PostAsync($"/api/missions/{id}/intervenants", body, ct);
    ctx.Response.StatusCode = (int)r.StatusCode;
    ctx.Response.ContentType = r.Content.Headers.ContentType?.ToString() ?? "application/json";
    await r.Content.CopyToAsync(ctx.Response.Body, ct);
});

proxy.MapDelete("/missions/{id:guid}/intervenants/{iid:guid}", async (
    Guid id, Guid iid, HttpContext ctx, IHttpClientFactory factory, CancellationToken ct) =>
{
    var client = factory.CreateClient("PrediCopApi");
    var r = await client.DeleteAsync($"/api/missions/{id}/intervenants/{iid}", ct);
    ctx.Response.StatusCode = (int)r.StatusCode;
});

proxy.MapPost("/missions/{id:guid}/force-assign", async (
    Guid id, HttpContext ctx, IHttpClientFactory factory, CancellationToken ct) =>
{
    var client = factory.CreateClient("PrediCopApi");
    using var body = new StreamContent(ctx.Request.Body);
    body.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
    var r = await client.PostAsync($"/api/missions/{id}/force-assign", body, ct);
    ctx.Response.StatusCode = (int)r.StatusCode;
    ctx.Response.ContentType = r.Content.Headers.ContentType?.ToString() ?? "application/json";
    await r.Content.CopyToAsync(ctx.Response.Body, ct);
});

proxy.MapPost("/media", async (
    HttpContext ctx, IHttpClientFactory factory, CancellationToken ct) =>
{
    var client = factory.CreateClient("PrediCopApi");
    var multipart = new MultipartFormDataContent();
    foreach (var file in ctx.Request.Form.Files)
    {
        var sc = new StreamContent(file.OpenReadStream());
        sc.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
        multipart.Add(sc, "file", file.FileName);
    }
    foreach (var kv in ctx.Request.Form)
        multipart.Add(new StringContent(kv.Value.ToString()), kv.Key);
    var r = await client.PostAsync("/api/media", multipart, ct);
    ctx.Response.StatusCode = (int)r.StatusCode;
    ctx.Response.ContentType = r.Content.Headers.ContentType?.ToString() ?? "application/json";
    await r.Content.CopyToAsync(ctx.Response.Body, ct);
});

proxy.MapDelete("/media/{mediaId:guid}", async (
    Guid mediaId, HttpContext ctx, IHttpClientFactory factory, CancellationToken ct) =>
{
    var client = factory.CreateClient("PrediCopApi");
    var r = await client.DeleteAsync($"/api/media/{mediaId}", ct);
    ctx.Response.StatusCode = (int)r.StatusCode;
});

proxy.MapGet("/media/{mediaId:guid}/file", async (
    Guid mediaId, HttpContext ctx, IHttpClientFactory factory, CancellationToken ct) =>
{
    var client = factory.CreateClient("PrediCopApi");
    var r = await client.GetAsync($"/api/media/{mediaId}/file",
        HttpCompletionOption.ResponseHeadersRead, ct);
    ctx.Response.StatusCode = (int)r.StatusCode;
    if (r.IsSuccessStatusCode)
    {
        ctx.Response.ContentType = r.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
        await r.Content.CopyToAsync(ctx.Response.Body, ct);
    }
});

// ── Text assistant endpoints ──────────────────────────────────────────────────

app.MapPost("/text-assistant/correct", async (TextAssistantInput input, TextAssistantService svc, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(input.Text)) return Results.Ok(new { correctedText = "" });
    try
    {
        var corrected = await svc.CorrectSpellingAsync(input.Text, ct);
        return Results.Ok(new { correctedText = corrected });
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: 503);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message, statusCode: 502);
    }
}).RequireAuthorization();

app.MapPost("/text-assistant/improve", async (TextAssistantInput input, TextAssistantService svc, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(input.Text)) return Results.Ok(new { suggestions = Array.Empty<object>() });
    try
    {
        var suggestions = await svc.ImproveAsync(input.Text, ct);
        return Results.Ok(new { suggestions });
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: 503);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message, statusCode: 502);
    }
}).RequireAuthorization();

app.Run();

record TextAssistantInput(string Text);
