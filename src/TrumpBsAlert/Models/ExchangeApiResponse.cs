using System.Text.Json.Serialization;

namespace TrumpBsAlert.Models;

public record ExchangeApiResponse(string Result, Dictionary<string, decimal> Rates);

public record AwesomeApiQuote(
    [property: JsonPropertyName("bid")] string Bid,
    [property: JsonPropertyName("high")] string High,
    [property: JsonPropertyName("low")] string Low,
    [property: JsonPropertyName("pctChange")] string PctChange);
