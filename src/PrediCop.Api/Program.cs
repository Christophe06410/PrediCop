using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using PrediCop.Api.Hubs;
using PrediCop.Api.Middleware;
using PrediCop.Infrastructure.Extensions;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ---- Infrastructure (EF Core, repositories, services) ----
builder.Services.AddInfrastructure(builder.Configuration);

// ---- Controllers ----
builder.Services.AddControllers();

// ---- JWT Authentication ----
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"]!;

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.Zero
    };

    // Allow SignalR to receive JWT from query string
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                context.Token = accessToken;
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// ---- SignalR ----
builder.Services.AddSignalR();

// ---- CORS ----
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("PrediCopPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // required for SignalR
    });
});

// ---- OpenAPI (.NET 10 native) ----
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info.Title = "PrediCop API";
        document.Info.Version = "v1";
        document.Info.Description = "API SaaS de gestion de PrediCop";
        return Task.CompletedTask;
    });
});

// ---- Build ----
var app = builder.Build();

// ---- Pipeline ----
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "PrediCop API";
        options.AddHttpAuthentication("Bearer", auth =>
        {
            auth.Token = "votre-jwt-token-ici";
        });
    });
}

app.UseHttpsRedirection();

app.UseCors("PrediCopPolicy");

app.UseAuthentication();
app.UseTenantMiddleware();
app.UseAuthorization();

app.MapControllers();
app.MapHub<PoliceHub>("/hubs/police");

app.Run();
