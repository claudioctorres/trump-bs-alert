using TrumpBsAlert.Services;
using TrumpBsAlert.ViewModels;

namespace TrumpBsAlert.Pages;

public partial class AlertPage : ContentPage
{
    public AlertPage(decimal rate, DateTime timestamp, IAlertCoordinator coordinator)
    {
        InitializeComponent();
        var viewModel = new AlertViewModel(coordinator);
        viewModel.SetAlertData(rate, timestamp);
        BindingContext = viewModel;
    }
}
