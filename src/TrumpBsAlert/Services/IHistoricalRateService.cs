using TrumpBsAlert.Models;

namespace TrumpBsAlert.Services;

public interface IHistoricalRateService
{
    Task<IReadOnlyList<DailyRate>> GetLast30DaysAsync(string baseCurrency, string quoteCurrency, CancellationToken ct = default);
}
