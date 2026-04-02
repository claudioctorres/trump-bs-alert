namespace TrumpBsAlert.Services;

public interface ITrayService
{
    void Initialize();
    void UpdateTooltip(string text);
    event EventHandler? ShowWindowRequested;
}
