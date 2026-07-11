using CSweet.UI.Services;
using Microsoft.Extensions.Logging;

namespace CSweet.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(ApiBaseUrl) });
        builder.Services.AddCSweetApiClients();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

#if ANDROID
    private const string ApiBaseUrl = "http://10.0.2.2:5149/";
#else
    private const string ApiBaseUrl = "http://localhost:5149/";
#endif
}
