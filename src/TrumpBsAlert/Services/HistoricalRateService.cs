using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TrumpBsAlert.Models;

namespace TrumpBsAlert.Services;

public class HistoricalRateService : IHistoricalRateService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HistoricalRateService> _logger;

    public HistoricalRateService(
        IHttpClientFactory httpClientFactory,
        ILogger<HistoricalRateService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DailyRate>> GetLast30DaysAsync(
        string baseCurrency, string quoteCurrency, CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("historical");
            var to = DateOnly.FromDateTime(DateTime.UtcNow);
            var from = to.AddDays(-30);
            var url = $"https://api.frankfurter.app/{from:yyyy-MM-dd}..{to:yyyy-MM-dd}?from={baseCurrency}&to={quoteCurrency}";

            var response = await client.GetFromJsonAsync<JsonElement>(url, ct);
            var rates = response.GetProperty("rates");
            var result = new List<DailyRate>();

            foreach (var dateProperty in rates.EnumerateObject())
            {
                if (DateOnly.TryParse(dateProperty.Name, out var date))
                {
                    var closeValue = dateProperty.Value.GetProperty(quoteCurrency).GetDecimal();
                    result.Add(new DailyRate(date, closeValue));
                }
            }

            result.Sort((a, b) => a.Date.CompareTo(b.Date));
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching historical exchange rates");
            return [];
        }
    }
}
