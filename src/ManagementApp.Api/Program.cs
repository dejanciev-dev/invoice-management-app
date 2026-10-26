using ManagementApp.Api.Endpoints;
using ManagementApp.Api.Services;
using ManagementApp.Application;
using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Infrastructure;
using ManagementApp.Infrastructure.Persistence;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddAuthorization();

var app = builder.Build();

await app.Services.InitialiseDatabaseAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapInvoiceEndpoints();

app.Run();

public partial class Program;
