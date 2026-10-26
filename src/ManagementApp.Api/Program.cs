using ManagementApp.Api.Endpoints;
using ManagementApp.Api.Infrastructure;
using ManagementApp.Api.Services;
using ManagementApp.Application;
using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Infrastructure;
using ManagementApp.Infrastructure.Persistence;
using Scalar.AspNetCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApplicationExceptionHandler>();
builder.Services.AddAuthorization();
builder.Services.AddOpenApi();

var app = builder.Build();

await app.Services.InitialiseDatabaseAsync();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.WithTitle("Management App API"));
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// Serves the built web app from wwwroot. Vite fingerprints everything under /assets, so it can be cached forever.
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        if (context.Context.Request.Path.StartsWithSegments("/assets"))
        {
            context.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        }
    }
});

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapInvoiceEndpoints();
app.MapHealthChecks("/health");

// Unknown API routes return 404; every other route is handled by the client-side router.
app.Map("/api/{**path}", () => TypedResults.NotFound()).ExcludeFromDescription();
app.MapFallbackToFile("index.html", new StaticFileOptions
{
    OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache"
});

app.Run();

public partial class Program;
