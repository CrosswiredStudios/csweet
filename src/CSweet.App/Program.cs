using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using CSweet.App;
using CSweet.UI.Services;
using MudBlazor.Services;

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

builder.Services.AddScoped<CookieAndAntiforgeryHandler>();
builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<CookieAndAntiforgeryHandler>();
    handler.InnerHandler = new HttpClientHandler();
    return new HttpClient(handler) { BaseAddress = new Uri(httpBaseAddress) };
});
builder.Services.AddCSweetApiClients();
builder.Services.AddMudServices();

await builder.Build().RunAsync();
