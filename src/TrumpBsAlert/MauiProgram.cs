using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using SkiaSharp.Views.Maui.Controls.Hosting;
using TrumpBsAlert.Pages;
using TrumpBsAlert.Services;
using TrumpBsAlert.ViewModels;

namespace TrumpBsAlert;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseSkiaSharp()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // HttpClients
        builder.Services.AddHttpClient("live", client =>
        {
            client.BaseAddress = new Uri("https://economia.awesomeapi.com.br/");
            client.Timeout = TimeSpan.FromSeconds(8);
        });
        builder.Services.AddHttpClient("historical", client =>
        {
            client.BaseAddress = new Uri("https://api.frankfurter.app/");
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        // Services
        builder.Services.AddSingleton<ISoundLoopService, SoundLoopService>();
        builder.Services.AddSingleton<IAlertCoordinator, AlertCoordinator>();
        builder.Services.AddSingleton<IHistoricalRateService, HistoricalRateService>();
        builder.Services.AddSingleton<ExchangeRateService>();
        builder.Services.AddSingleton<IExchangeRateService>(sp => sp.GetRequiredService<ExchangeRateService>());
        builder.Services.AddSingleton<ITrayService, TrayService>();

        // ViewModels
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<AlertViewModel>();

        // Pages
        builder.Services.AddSingleton<MainPage>();

#if WINDOWS
        builder.ConfigureLifecycleEvents(lifecycle =>
        {
            lifecycle.AddWindows(windows =>
            {
                windows.OnWindowCreated(window =>
                {
                    var handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
                    var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle);
                    var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(id);

                    appWindow.Closing += (s, e) =>
                    {
                        if (Preferences.Default.Get("minimize_to_tray", true))
                        {
                            e.Cancel = true;
                            Platforms.Windows.NativeHelper.HideWindow(handle);
                        }
                    };
                });
            });
        });
#endif

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
