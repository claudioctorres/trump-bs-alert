using System.Net.Http.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TrumpBsAlert.Models;

namespace TrumpBsAlert.Services;

public class ExchangeRateService : IExchangeRateService, IHostedService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAlertCoordinator _coordinator;
    private readonly ILogger<ExchangeRateService> _logger;
    private CancellationTokenSource? _cts;
    private Task? _runningTask;
    private bool _isAlerting;
    private decimal? _threshold;

    public decimal? Threshold
    {
        get => _threshold;
        set
        {
            _threshold = value;
            _isAlerting = false;
        }
    }
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(60);
    public string BaseCurrency { get; set; } = "USD";
    public string QuoteCurrency { get; set; } = "BRL";

    public event EventHandler<decimal>? RateChanged;
    public event EventHandler<string>? FetchError;

    public ExchangeRateService(
        IHttpClientFactory httpClientFactory,
        IAlertCoordinator coordinator,
        ILogger<ExchangeRateService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _coordinator = coordinator;
        _logger = logger;

        _coordinator.AlertAcknowledged += (_, _) => _isAlerting = false;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _runningTask = RunPollingLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync();
            if (_runningTask is not null)
                await _runningTask;
            _cts.Dispose();
            _cts = null;
        }
    }

    private async Task RunPollingLoopAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Do a first fetch immediately
            await ExecuteTickAsync(stoppingToken);

            using var timer = new PeriodicTimer(Interval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ExecuteTickAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown — swallow the exception
        }
    }

    private async Task ExecuteTickAsync(CancellationToken stoppingToken)
    {
        try
        {
            var rate = await FetchRateAsync(stoppingToken);
            RateChanged?.Invoke(this, rate);

            if (Threshold is null) return;

            if (rate >= Threshold && !_isAlerting)
            {
                _isAlerting = true;
                await _coordinator.TriggerAlertAsync(rate);
            }

            if (rate < Threshold && _isAlerting)
                _isAlerting = false;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching exchange rate");
            FetchError?.Invoke(this, ex.Message);
        }
    }

    private async Task<decimal> FetchRateAsync(CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("live");
        var pairKey = $"{BaseCurrency}-{QuoteCurrency}";
        var response = await client.GetFromJsonAsync<Dictionary<string, AwesomeApiQuote>>(
            $"last/{pairKey}", ct)
            ?? throw new InvalidOperationException("Null response from exchange API");

        var lookupKey = $"{BaseCurrency}{QuoteCurrency}";
        if (!response.TryGetValue(lookupKey, out var quote))
            throw new KeyNotFoundException($"Pair {lookupKey} not found in response");

        return decimal.Parse(quote.Bid, System.Globalization.CultureInfo.InvariantCulture);
    }
}
