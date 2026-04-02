namespace TrumpBsAlert.Services;

public interface IExchangeRateService
{
    decimal? Threshold { get; set; }
    TimeSpan Interval { get; set; }
    string BaseCurrency { get; set; }
    string QuoteCurrency { get; set; }
    event EventHandler<decimal>? RateChanged;
    event EventHandler<string>? FetchError;
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}
