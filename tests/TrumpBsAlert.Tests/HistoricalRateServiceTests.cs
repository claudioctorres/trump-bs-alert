using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TrumpBsAlert.Services;
using Xunit;

namespace TrumpBsAlert.Tests;

public class HistoricalRateServiceTests
{
    private readonly ILogger<HistoricalRateService> _logger;

    public HistoricalRateServiceTests()
    {
        _logger = Substitute.For<ILogger<HistoricalRateService>>();
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => _handler(request, ct);
    }

    private HistoricalRateService CreateService(HttpMessageHandler handler)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("historical").Returns(new HttpClient(handler));

        return new HistoricalRateService(factory, _logger);
    }

    [Fact]
    public async Task GetLast30Days_ParsesResponseCorrectly()
    {
        var responseJson = new
        {
            @base = "USD",
            start_date = "2026-03-03",
            end_date = "2026-04-02",
            rates = new Dictionary<string, object>
            {
                ["2026-03-03"] = new Dictionary<string, decimal> { ["BRL"] = 5.1234m },
                ["2026-03-04"] = new Dictionary<string, decimal> { ["BRL"] = 5.2345m },
                ["2026-03-05"] = new Dictionary<string, decimal> { ["BRL"] = 5.3456m }
            }
        };

        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(responseJson)
            }));

        var service = CreateService(handler);

        var result = await service.GetLast30DaysAsync("USD", "BRL");

        Assert.Equal(3, result.Count);

        // Verify sorted by date ascending
        Assert.Equal(new DateOnly(2026, 3, 3), result[0].Date);
        Assert.Equal(new DateOnly(2026, 3, 4), result[1].Date);
        Assert.Equal(new DateOnly(2026, 3, 5), result[2].Date);

        // Verify rates
        Assert.Equal(5.1234m, result[0].Close);
        Assert.Equal(5.2345m, result[1].Close);
        Assert.Equal(5.3456m, result[2].Close);
    }

    [Fact]
    public async Task GetLast30Days_OnError_ReturnsEmptyList()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            throw new HttpRequestException("Network error"));

        var service = CreateService(handler);

        var result = await service.GetLast30DaysAsync("USD", "BRL");

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetLast30Days_CorrectUrlConstruction()
    {
        Uri? capturedUri = null;

        var responseJson = new
        {
            @base = "USD",
            start_date = "2026-03-03",
            end_date = "2026-04-02",
            rates = new Dictionary<string, object>()
        };

        var handler = new MockHttpMessageHandler((request, _) =>
        {
            capturedUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(responseJson)
            });
        });

        var service = CreateService(handler);

        await service.GetLast30DaysAsync("USD", "BRL");

        Assert.NotNull(capturedUri);
        var url = capturedUri!.ToString();

        // Verify it hits the Frankfurter API
        Assert.Contains("api.frankfurter.app", url);

        // Verify currencies are in the URL
        Assert.Contains("from=USD", url);
        Assert.Contains("to=BRL", url);

        // Verify the URL contains a date range pattern (yyyy-MM-dd..yyyy-MM-dd)
        Assert.Matches(@"\d{4}-\d{2}-\d{2}\.\.\d{4}-\d{2}-\d{2}", url);
    }

    [Fact]
    public async Task GetLast30Days_ResultsSortedByDateAscending()
    {
        // Return dates in non-sorted order to verify sorting
        var responseJson = new
        {
            @base = "USD",
            start_date = "2026-03-03",
            end_date = "2026-04-02",
            rates = new Dictionary<string, object>
            {
                ["2026-03-10"] = new Dictionary<string, decimal> { ["BRL"] = 5.30m },
                ["2026-03-03"] = new Dictionary<string, decimal> { ["BRL"] = 5.10m },
                ["2026-03-07"] = new Dictionary<string, decimal> { ["BRL"] = 5.20m }
            }
        };

        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(responseJson)
            }));

        var service = CreateService(handler);

        var result = await service.GetLast30DaysAsync("USD", "BRL");

        Assert.Equal(3, result.Count);
        Assert.True(result[0].Date < result[1].Date);
        Assert.True(result[1].Date < result[2].Date);
    }
}
