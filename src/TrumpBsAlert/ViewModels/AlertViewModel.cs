using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrumpBsAlert.Services;

namespace TrumpBsAlert.ViewModels;

public partial class AlertViewModel : ObservableObject
{
    private readonly IAlertCoordinator _alertCoordinator;

    [ObservableProperty]
    public partial string RateText { get; set; }

    [ObservableProperty]
    public partial string TimestampText { get; set; }

    public AlertViewModel(IAlertCoordinator alertCoordinator)
    {
        _alertCoordinator = alertCoordinator;
        RateText = "";
        TimestampText = "";
    }

    public void SetAlertData(decimal rate, DateTime timestamp)
    {
        RateText = $"{rate:F4}";
        TimestampText = timestamp.ToString("HH:mm:ss dd/MM/yyyy");
    }

    [RelayCommand]
    private void Acknowledge()
    {
        _alertCoordinator.Acknowledge();
    }
}
