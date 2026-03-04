using Microsoft.Extensions.Logging;
using AniScroll.Shared.Services;

namespace AniScroll.Maui;

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

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        // HttpClient simple, sans handler custom
        builder.Services.AddScoped(sp => new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30),
            // Forcer la résolution DNS correcte sur Android
            DefaultRequestHeaders = { { "Accept", "application/json" } }
        });

        builder.Services.AddScoped<AniListService>();
        builder.Services.AddScoped<AniListAuthService>();
        builder.Services.AddSingleton<UserListService>();

        return builder.Build();
    }
}