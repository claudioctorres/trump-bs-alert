using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrumpBsAlert.Models;
using TrumpBsAlert.Services;

namespace TrumpBsAlert.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IExchangeRateService _exchangeRateService;
    private readonly IHistoricalRateService _historicalRateService;
    private readonly IAlertCoordinator _alertCoordinator;

    [ObservableProperty]
    public partial string CurrentRate { get; set; }

    [ObservableProperty]
    public partial string LastUpdated { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; }

    [ObservableProperty]
    public partial string StatusColor { get; set; }

    [ObservableProperty]
    public partial string ThresholdText { get; set; }

    [ObservableProperty]
    public partial string BaseCurrency { get; set; }

    [ObservableProperty]
    public partial string QuoteCurrency { get; set; }

    [ObservableProperty]
    public partial int SelectedIntervalIndex { get; set; }

    [ObservableProperty]
    public partial bool IsMinimizeToTray { get; set; }

    [ObservableProperty]
    public partial bool IsChartLoading { get; set; }

    [ObservableProperty]
    public partial string? ChartErrorMessage { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<DailyRate>? ChartData { get; set; }

    [ObservableProperty]
    public partial decimal? ChartThreshold { get; set; }

    public ObservableCollection<string> SessionHistory { get; } = new();

    public string[] IntervalOptions { get; } = ["30s", "1m", "5m"];

    public MainViewModel(
        IExchangeRateService exchangeRateService,
        IHistoricalRateService historicalRateService,
        IAlertCoordinator alertCoordinator)
    {
        _exchangeRateService = exchangeRateService;
        _historicalRateService = historicalRateService;
        _alertCoordinator = alertCoordinator;

        // Initialize properties
        CurrentRate = "--";
        LastUpdated = "--";
        ThresholdText = "";
        IsChartLoading = true;

        // Load persisted settings
        BaseCurrency = Preferences.Default.Get("base_currency", "USD");
        QuoteCurrency = Preferences.Default.Get("quote_currency", "BRL");

        var storedThreshold = Preferences.Default.Get("threshold", -1.0);
        if (storedThreshold > 0)
        {
            ThresholdText = storedThreshold.ToString("F4");
            _exchangeRateService.Threshold = (decimal)storedThreshold;
            ChartThreshold = (decimal)storedThreshold;
            StatusText = "Monitorando";
            StatusColor = "#00AA00";
        }
        else
        {
            StatusText = "Sem limite definido";
            StatusColor = "#FFA500";
        }

        var intervalSeconds = Preferences.Default.Get("interval_seconds", 60);
        SelectedIntervalIndex = intervalSeconds switch
        {
            30 => 0,
            300 => 2,
            _ => 1
        };

        IsMinimizeToTray = Preferences.Default.Get("minimize_to_tray", true);

        _exchangeRateService.BaseCurrency = BaseCurrency;
        _exchangeRateService.QuoteCurrency = QuoteCurrency;
        _exchangeRateService.Interval = TimeSpan.FromSeconds(intervalSeconds);

        // Subscribe to events
        _exchangeRateService.RateChanged += OnRateChanged;
        _exchangeRateService.FetchError += OnFetchError;
        _alertCoordinator.AlertTriggered += OnAlertTriggered;
        _alertCoordinator.AlertAcknowledged += OnAlertAcknowledged;
    }

    partial void OnIsMinimizeToTrayChanged(bool value)
    {
        Preferences.Default.Set("minimize_to_tray", value);
    }

    public async Task InitializeAsync()
    {
        await LoadChartDataAsync();
    }

    private void OnRateChanged(object? sender, decimal rate)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            CurrentRate = $"{rate:F4}";
            LastUpdated = DateTime.Now.ToString("HH:mm:ss");

            SessionHistory.Insert(0, $"{rate:F4} \u2014 {DateTime.Now:HH:mm:ss}");
            while (SessionHistory.Count > 20)
                SessionHistory.RemoveAt(SessionHistory.Count - 1);

            if (!_alertCoordinator.IsAlerting)
            {
                if (_exchangeRateService.Threshold is null)
                {
                    StatusText = "Sem limite definido";
                    StatusColor = "#FFA500";
                }
                else
                {
                    StatusText = "Monitorando";
                    StatusColor = "#00AA00";
                }
            }
        });
    }

    private void OnFetchError(object? sender, string error)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            StatusText = "Erro na \u00faltima busca";
            StatusColor = "#FF0000";
        });
    }

    private void OnAlertTriggered(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            StatusText = "Alerta ativo";
            StatusColor = "#FF0000";
        });
    }

    private void OnAlertAcknowledged(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            StatusText = _exchangeRateService.Threshold is null ? "Sem limite definido" : "Monitorando";
            StatusColor = _exchangeRateService.Threshold is null ? "#FFA500" : "#00AA00";
        });
    }

    [RelayCommand]
    private void SaveThreshold()
    {
        if (string.IsNullOrWhiteSpace(ThresholdText))
        {
            _exchangeRateService.Threshold = null;
            Preferences.Default.Remove("threshold");
            StatusText = "Sem limite definido";
            StatusColor = "#FFA500";
            ChartThreshold = null;
            return;
        }

        if (decimal.TryParse(ThresholdText, out var value) && value > 0)
        {
            _exchangeRateService.Threshold = value;
            Preferences.Default.Set("threshold", (double)value);
            StatusText = "Monitorando";
            StatusColor = "#00AA00";
            ChartThreshold = value;
        }
    }

    [RelayCommand]
    private async Task ChangeInterval()
    {
        var seconds = SelectedIntervalIndex switch
        {
            0 => 30,
            2 => 300,
            _ => 60
        };

        Preferences.Default.Set("interval_seconds", seconds);
        _exchangeRateService.Interval = TimeSpan.FromSeconds(seconds);

        await _exchangeRateService.StopAsync(CancellationToken.None);
        await _exchangeRateService.StartAsync(CancellationToken.None);
    }

    [RelayCommand]
    private async Task ChangeCurrencyPair()
    {
        var baseCur = BaseCurrency?.Trim().ToUpperInvariant();
        var quoteCur = QuoteCurrency?.Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(baseCur) || string.IsNullOrEmpty(quoteCur))
            return;

        BaseCurrency = baseCur;
        QuoteCurrency = quoteCur;

        Preferences.Default.Set("base_currency", baseCur);
        Preferences.Default.Set("quote_currency", quoteCur);

        _exchangeRateService.BaseCurrency = baseCur;
        _exchangeRateService.QuoteCurrency = quoteCur;

        await _exchangeRateService.StopAsync(CancellationToken.None);
        await _exchangeRateService.StartAsync(CancellationToken.None);
        await LoadChartDataAsync();
    }

    [RelayCommand]
    private async Task RetryLoadChart()
    {
        await LoadChartDataAsync();
    }

    private async Task LoadChartDataAsync()
    {
        IsChartLoading = true;
        ChartErrorMessage = null;

        try
        {
            var data = await _historicalRateService.GetLast30DaysAsync(
                BaseCurrency, QuoteCurrency);

            if (data.Count == 0)
            {
                ChartErrorMessage = "N\u00e3o foi poss\u00edvel carregar o hist\u00f3rico";
                return;
            }

            ChartData = data;
        }
        catch (Exception)
        {
            ChartErrorMessage = "N\u00e3o foi poss\u00edvel carregar o hist\u00f3rico";
        }
        finally
        {
            IsChartLoading = false;
        }
    }
}
