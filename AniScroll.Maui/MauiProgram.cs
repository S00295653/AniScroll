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

        builder.Services.AddScoped(sp => new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        });

        builder.Services.AddScoped<AniListService>();

        // Singleton so the list persists for the whole session across all components
        builder.Services.AddSingleton<UserListService>();

        return builder.Build();
    }
}