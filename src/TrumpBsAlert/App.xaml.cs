using TrumpBsAlert.Pages;
using TrumpBsAlert.Services;
using TrumpBsAlert.ViewModels;

namespace TrumpBsAlert;

public partial class App : Application
{
    private readonly ExchangeRateService _exchangeRateService;
    private readonly ITrayService _trayService;
    private readonly IExchangeRateService _rateService;

    private readonly MainPage _mainPage;
    private Window? _mainWindow;

    public App(
        MainPage mainPage,
        ExchangeRateService exchangeRateService,
        ITrayService trayService,
        IExchangeRateService rateService)
    {
        InitializeComponent();

        _mainPage = mainPage;
        _exchangeRateService = exchangeRateService;
        _trayService = trayService;
        _rateService = rateService;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        _mainWindow = new Window(_mainPage)
        {
            Width = 400,
        };

        _mainWindow.Created += async (s, e) =>
        {
            _trayService.Initialize();

            _rateService.RateChanged += (_, rate) =>
            {
                var pair = $"{_rateService.BaseCurrency}/{_rateService.QuoteCurrency}";
                var tooltip = $"trump-bs-alert — {pair} = {rate:F4}";
                _trayService.UpdateTooltip(tooltip);
            };

            _trayService.ShowWindowRequested += (_, _) => ShowMainWindow();

            await _exchangeRateService.StartAsync(CancellationToken.None);
        };

        return _mainWindow;
    }

    private void ShowMainWindow()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_mainWindow is null) return;

#if WINDOWS
            if (_mainWindow.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
                Platforms.Windows.NativeHelper.ShowAndActivateWindow(hwnd);
                nativeWindow.Activate();
            }
#endif
        });
    }
}
