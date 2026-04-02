using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TrumpBsAlert.Services;
using Xunit;

namespace TrumpBsAlert.Tests;

public class ExchangeRateServiceTests
{
    private readonly IAlertCoordinator _coordinator;
    private readonly ILogger<ExchangeRateService> _logger;

    public ExchangeRateServiceTests()
    {
        _coordinator = Substitute.For<IAlertCoordinator>();
        _logger = Substitute.For<ILogger<ExchangeRateService>>();
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

    private static HttpResponseMessage CreateRateResponse(decimal brlRate) =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new Dictionary<string, object>
            {
                ["USDBRL"] = new { bid = brlRate.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) }
            })
        };

    private ExchangeRateService CreateService(HttpMessageHandler handler)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://economia.awesomeapi.com.br/")
        };
        factory.CreateClient("live").Returns(client);

        return new ExchangeRateService(factory, _coordinator, _logger)
        {
            Interval = TimeSpan.FromMilliseconds(50),
            BaseCurrency = "USD",
            QuoteCurrency = "BRL"
        };
    }

    [Fact]
    public async Task RisingEdge_TriggersAlert()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(CreateRateResponse(6.50m)));

        var service = CreateService(handler);
        service.Threshold = 6.00m;

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);

        await Task.Delay(200);

        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);

        await _coordinator.Received(1).TriggerAlertAsync(6.50m);
    }

    [Fact]
    public async Task StaysAbove_DoesNotRetrigger()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(CreateRateResponse(6.50m)));

        var service = CreateService(handler);
        service.Threshold = 6.00m;

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);

        await Task.Delay(300);

        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);

        await _coordinator.Received(1).TriggerAlertAsync(Arg.Any<decimal>());
    }

    [Fact]
    public async Task DropsBelow_ThenRises_RetriggersAlert()
    {
        var callCount = 0;
        var handler = new MockHttpMessageHandler((_, _) =>
        {
            var count = Interlocked.Increment(ref callCount);
            var rate = count switch
            {
                1 => 6.50m,
                2 => 5.50m,
                _ => 6.50m
            };
            return Task.FromResult(CreateRateResponse(rate));
        });

        var service = CreateService(handler);
        service.Threshold = 6.00m;

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);

        await Task.Delay(400);

        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);

        await _coordinator.Received(2).TriggerAlertAsync(6.50m);
    }

    [Fact]
    public async Task NullThreshold_DoesNotAlert()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(CreateRateResponse(100.00m)));

        var service = CreateService(handler);
        service.Threshold = null;

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);

        await Task.Delay(200);

        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);

        await _coordinator.DidNotReceive().TriggerAlertAsync(Arg.Any<decimal>());
    }

    [Fact]
    public async Task FetchError_DoesNotAlert()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            throw new HttpRequestException("Network error"));

        var service = CreateService(handler);
        service.Threshold = 6.00m;

        string? errorMessage = null;
        service.FetchError += (_, msg) => errorMessage = msg;

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);

        await Task.Delay(200);

        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);

        await _coordinator.DidNotReceive().TriggerAlertAsync(Arg.Any<decimal>());
        Assert.NotNull(errorMessage);
        Assert.Contains("Network error", errorMessage);
    }

    [Fact]
    public async Task RateChanged_FiredOnSuccessfulFetch()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(CreateRateResponse(5.75m)));

        var service = CreateService(handler);
        service.Threshold = null;

        decimal? receivedRate = null;
        service.RateChanged += (_, rate) => receivedRate = rate;

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);

        await Task.Delay(200);

        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);

        Assert.NotNull(receivedRate);
        Assert.Equal(5.75m, receivedRate.Value);
    }
}
