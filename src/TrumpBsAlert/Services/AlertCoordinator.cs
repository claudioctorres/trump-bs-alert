using Microsoft.Extensions.Logging;
using TrumpBsAlert.Pages;

namespace TrumpBsAlert.Services;

public class AlertCoordinator : IAlertCoordinator
{
    private readonly ISoundLoopService _soundService;
    private readonly ILogger<AlertCoordinator> _logger;
    private Window? _alertWindow;

    public bool IsAlerting { get; private set; }

    public event EventHandler? AlertTriggered;
    public event EventHandler? AlertAcknowledged;

    public AlertCoordinator(
        ISoundLoopService soundService,
        ILogger<AlertCoordinator> logger)
    {
        _soundService = soundService;
        _logger = logger;
    }

    public async Task TriggerAlertAsync(decimal rate)
    {
        if (IsAlerting) return; // idempotent

        IsAlerting = true;
        _soundService.Start();
        AlertTriggered?.Invoke(this, EventArgs.Empty);

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var alertPage = new AlertPage(rate, DateTime.Now, this);
            _alertWindow = new Window(alertPage)
            {
                Title = "ALERTA",
                Width = 420,
                Height = 290,
            };

            // Prevent closing while alerting
            _alertWindow.Destroying += (s, e) =>
            {
                // Can't actually cancel Window.Destroying in MAUI easily,
                // but the user must use the ACK button
            };

            Application.Current!.OpenWindow(_alertWindow);
        });
    }

    public void Acknowledge()
    {
        if (!IsAlerting) return;

        IsAlerting = false;
        _soundService.Stop();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_alertWindow is not null)
            {
                Application.Current?.CloseWindow(_alertWindow);
                _alertWindow = null;
            }
        });

        AlertAcknowledged?.Invoke(this, EventArgs.Empty);
        _logger.LogInformation("Alert acknowledged");
    }
}
