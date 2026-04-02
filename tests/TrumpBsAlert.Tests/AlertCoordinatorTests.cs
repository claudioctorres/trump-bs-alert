using Microsoft.Extensions.Logging;
using NSubstitute;
using TrumpBsAlert.Services;
using Xunit;

namespace TrumpBsAlert.Tests;

public class AlertCoordinatorTests
{
    private readonly ISoundLoopService _soundService;
    private readonly ILogger<AlertCoordinator> _logger;
    private readonly AlertCoordinator _coordinator;

    public AlertCoordinatorTests()
    {
        _soundService = Substitute.For<ISoundLoopService>();
        _logger = Substitute.For<ILogger<AlertCoordinator>>();
        _coordinator = new AlertCoordinator(_soundService, _logger);
    }

    [Fact]
    public void IsAlerting_InitiallyFalse()
    {
        Assert.False(_coordinator.IsAlerting);
    }

    [Fact]
    public void Acknowledge_WhenNotAlerting_DoesNothing()
    {
        // Should not throw and should not call Stop
        _coordinator.Acknowledge();

        _soundService.DidNotReceive().Stop();
        Assert.False(_coordinator.IsAlerting);
    }

    [Fact]
    public void Acknowledge_WhenNotAlerting_DoesNotFireEvent()
    {
        var eventFired = false;
        _coordinator.AlertAcknowledged += (_, _) => eventFired = true;

        _coordinator.Acknowledge();

        Assert.False(eventFired);
    }
}
