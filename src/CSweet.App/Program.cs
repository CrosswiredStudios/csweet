using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using CSweet.App;
using CSweet.App.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["CSweet:ApiBaseUrl"];
var httpBaseAddress = string.IsNullOrWhiteSpace(apiBaseUrl)
    ? builder.HostEnvironment.BaseAddress
    : apiBaseUrl;

if (!httpBaseAddress.EndsWith('/'))
{
    httpBaseAddress += "/";
}

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(httpBaseAddress) });
builder.Services.AddScoped<ISetupApiClient, SetupApiClient>();
builder.Services.AddScoped<ILlmProviderApiClient, LlmProviderApiClient>();
builder.Services.AddScoped<IOrganizationApiClient, OrganizationApiClient>();
builder.Services.AddScoped<IPlanningApiClient, PlanningApiClient>();

await builder.Build().RunAsync();
