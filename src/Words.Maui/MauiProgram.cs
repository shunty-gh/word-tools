using Microsoft.Extensions.Logging;
using Words.Core;
using Words.Maui.Services;
using Words.Maui.ViewModels;

namespace Words.Maui;

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
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // The composition root, as in the CLI: the engine never decides where its entries
        // come from.
        builder.Services.AddSingleton<IPersonalWordStore, AppDataPersonalWordStore>();
        builder.Services.AddSingleton<LexiconService>();
        builder.Services.AddSingleton<LookupSettings>();

        builder.Services.AddSingleton<SearchViewModel>();
        builder.Services.AddTransient<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

        // Loading takes a few hundred milliseconds; start it now so it overlaps the first
        // frame rather than stalling the first search.
        app.Services.GetRequiredService<LexiconService>().BeginLoading();

        return app;
    }
}
