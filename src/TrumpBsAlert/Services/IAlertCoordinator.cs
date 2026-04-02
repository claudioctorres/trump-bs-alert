namespace TrumpBsAlert.Services;

public interface IAlertCoordinator
{
    bool IsAlerting { get; }
    Task TriggerAlertAsync(decimal rate);
    void Acknowledge();
    event EventHandler? AlertTriggered;
    event EventHandler? AlertAcknowledged;
}
